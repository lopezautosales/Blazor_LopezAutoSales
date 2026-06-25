using Amazon.Runtime;
using Amazon.S3;
using LopezAutoSales.Server.Models;
using LopezAutoSales.Server.Storage;
using LopezAutoSales.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Security.Claims;
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

            services.AddControllersWithViews().AddNewtonsoftJson(x => x.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
            services.AddRazorPages();

            services.Configure<IdentityOptions>(options =>
                options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier);

            // Object storage (Cloudflare R2 / S3) for car images.
            services.Configure<ObjectStorageOptions>(Configuration.GetSection("ObjectStorage"));
            services.AddSingleton<IAmazonS3>(sp =>
            {
                ObjectStorageOptions o = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;
                AmazonS3Config config = new AmazonS3Config
                {
                    ServiceURL = o.ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = "auto"
                };
                return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
            });
            services.AddSingleton<IImageStorage, R2ImageStorage>();
        }

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
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
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

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
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
                IdentityResult result = userManager.CreateAsync(user, password).Result;

                if (result.Succeeded)
                {
                    userManager.AddToRoleAsync(user, "Admin").Wait();
                }
            }
        }
    }
}
