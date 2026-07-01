using Amazon.Runtime;
using Amazon.S3;
using LopezAutoSales.Server.Configuration;
using LopezAutoSales.Server.Models;
using LopezAutoSales.Server.Storage;
using LopezAutoSales.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace LopezAutoSales.Server
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IConfiguration configuration, IWebHostEnvironment env)
        {
            Configuration = configuration;
            _env = env;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(GetConnectionString()));

            // Persist the Data Protection key ring (encrypts the auth cookie) to Postgres so
            // it survives redeploys. On Railway's ephemeral filesystem the default on-disk key
            // store is regenerated each deploy, which invalidates the admin's auth cookie.
            services.AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>();

            // Behind Railway's edge proxy (TLS terminates there): honor X-Forwarded-*
            // so the app sees the real scheme/host and issues Secure cookies correctly.
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                // Railway's edge proxy IP isn't stable, so we can't pin KnownProxies.
                // Only honor a single hop and rely on the host being reachable solely
                // through that proxy; SecurePolicy=Always below prevents a spoofed
                // X-Forwarded-Proto from downgrading the auth cookie.
                options.ForwardLimit = 1;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
            });

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    // Default complexity (digit/upper/lower/symbol) plus a longer floor;
                    // the single admin should use a long passphrase via Admin__Password.
                    options.Password.RequiredLength = 8;
                    // Make the brute-force policy explicit instead of relying on defaults.
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                })
                .AddDefaultTokenProviders()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            // Cookie auth: the Blazor client and the API share the Identity application
            // cookie. For API calls, return 401/403 status codes instead of redirecting
            // to the login HTML page (which a SPA can't follow).
            services.ConfigureApplicationCookie(options =>
            {
                // The scaffolded Identity UI is removed; point any stray redirect at the
                // Blazor login instead of the (now non-existent) /Identity/Account/Login.
                options.LoginPath = "/app/login";
                options.AccessDeniedPath = "/app/login";
                // Harden the auth cookie. SameSite=Lax blocks cross-site POST (CSRF) for
                // this cookie-auth API; force Secure in production (behind HTTPS) while
                // allowing plain-HTTP local dev.
                options.Cookie.SecurePolicy = _env.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = System.TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return Task.CompletedTask;
                    }
                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

            // Use the framework's System.Text.Json; IgnoreCycles handles the EF entity
            // navigation loops the way Newtonsoft's ReferenceLoopHandling.Ignore did.
            services.AddControllersWithViews().AddJsonOptions(o =>
                o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
            services.AddRazorPages();

            services.Configure<IdentityOptions>(options =>
                options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier);

            // Throttle credential endpoints (brute-force / DoS) — Identity lockout alone
            // doesn't cap request rate. Both policies partition by client IP (via
            // ForwardedHeaders behind Railway's proxy) so one caller can't exhaust the
            // window for everyone — a plain named FixedWindowLimiter shares ONE global
            // bucket, which would let any visitor lock out all others.
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("auth", PerIpFixedWindow(permitLimit: 8));
                // Cap public account-lookup attempts on /pay to slow enumeration, per IP.
                options.AddPolicy("pay-lookup", PerIpFixedWindow(permitLimit: 6));
                // A bare 429 renders as a blank browser error page — unacceptable for a
                // customer mid-payment on /pay. API callers still get the plain status.
                options.OnRejected = async (context, token) =>
                {
                    if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
                        return;
                    context.HttpContext.Response.ContentType = "text/html; charset=utf-8";
                    await context.HttpContext.Response.WriteAsync(
                        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
                        "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
                        "<title>Too many attempts</title></head>" +
                        "<body style=\"font-family:system-ui,sans-serif;max-width:32rem;margin:4rem auto;padding:0 1rem;text-align:center\">" +
                        "<h1 style=\"font-size:1.5rem\">Too many attempts</h1>" +
                        "<p>Please wait a minute and try again.</p>" +
                        "<p>Need help? Call <a href=\"tel:16202086250\">(620) 208-6250</a>.</p>" +
                        "<p><a href=\"/pay\">Back to Make a Payment</a></p>" +
                        "</body></html>", token);
                };
            });

            // Liveness/readiness for Railway + uptime monitors (incl. DB connectivity).
            services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();

            // Object storage (Cloudflare R2 / S3) for car images. Fail fast at startup if
            // it's misconfigured rather than on the first image request.
            services.AddOptions<ObjectStorageOptions>()
                .Bind(Configuration.GetSection("ObjectStorage"))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceUrl) && !string.IsNullOrWhiteSpace(o.Bucket)
                    && !string.IsNullOrWhiteSpace(o.PublicBaseUrl),
                    "ObjectStorage ServiceUrl, Bucket and PublicBaseUrl are required.")
                .ValidateOnStart();
            services.AddSingleton<IAmazonS3>(sp =>
            {
                ObjectStorageOptions o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
                AmazonS3Config config = new AmazonS3Config
                {
                    ServiceURL = o.ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = "auto",
                    // Cloudflare R2 doesn't implement the AWS SDK's newer streaming checksum
                    // trailer (STREAMING-AWS4-HMAC-SHA256-PAYLOAD-TRAILER), so uploads fail with
                    // 501 Not Implemented. Only send/validate checksums when the op requires it.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                };
                return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
            });
            services.AddSingleton<IImageStorage, R2ImageStorage>();

            // Stripe (online note payments) is OPTIONAL — the app must still run without it
            // (e.g. local smoke tests). So we bind the section but deliberately do NOT
            // ValidateOnStart: the checkout service exposes IsConfigured and the public /pay
            // page degrades gracefully when keys are unset. Real TEST keys come from
            // user-secrets (dev) / env vars (prod): Stripe__SecretKey, Stripe__PublishableKey,
            // Stripe__WebhookSecret. appsettings.json ships empty placeholders only.
            services.AddOptions<StripeOptions>().Bind(Configuration.GetSection("Stripe"));
            services.AddSingleton<Services.IStripeCheckoutService, Services.StripeCheckoutService>();

            // Periodic owner-controlled DB backups to the object store (disable with
            // Backup__Enabled=false, e.g. for local smoke tests with fake R2 creds).
            services.AddHostedService<Services.DatabaseBackupService>();
        }

        // A 1-minute fixed window partitioned by client IP. Unknown IPs (shouldn't happen
        // behind the proxy) share a single "unknown" bucket.
        private static Func<HttpContext, RateLimitPartition<string>> PerIpFixedWindow(int permitLimit)
            => httpContext => RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            // Baseline security headers (CSP omitted to avoid breaking the Blazor WASM
            // bootstrap; X-Frame-Options handles the clickjacking case).
            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                await next();
            });

            // Apply pending EF migrations and seed the admin user on startup.
            using (IServiceScope scope = app.ApplicationServices.CreateScope())
            {
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
                SeedUsers(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseWebAssemblyDebugging();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseBlazorFrameworkFiles();
            app.UseStaticFiles();

            app.UseSerilogRequestLogging();
            app.UseRouting();
            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health").AllowAnonymous();
                endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapFallbackToFile("index.html");
            });
        }

        // Railway injects DATABASE_URL in URL form; Npgsql needs key/value form.
        // Falls back to the configured DefaultConnection (local dev).
        private string GetConnectionString()
        {
            string url = System.Environment.GetEnvironmentVariable("DATABASE_URL");
            return string.IsNullOrWhiteSpace(url)
                ? Configuration.GetConnectionString("DefaultConnection")
                : Data.NpgsqlUrl.ToConnectionString(url);
        }

        public void SeedUsers(UserManager<ApplicationUser> userManager)
        {
            if (userManager.FindByEmailAsync(Dealership.Email).Result == null)
            {
                ApplicationUser user = new ApplicationUser
                {
                    UserName = Dealership.Email,
                    Email = Dealership.Email,
                    PhoneNumber = Dealership.Phone
                };
                string password = Configuration.GetValue<string>("Admin:Password");
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("Admin:Password (env Admin__Password) is not set — cannot seed the admin user.");

                IdentityResult result = userManager.CreateAsync(user, password).Result;
                if (!result.Succeeded)
                    throw new InvalidOperationException("Failed to seed the admin user: " + string.Join("; ", result.Errors.Select(e => e.Description)));

                userManager.AddToRoleAsync(user, "Admin").Wait();
            }
        }
    }
}
