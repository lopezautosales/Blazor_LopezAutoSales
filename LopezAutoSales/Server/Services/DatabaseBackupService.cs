using LopezAutoSales.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Services
{
    // Periodically writes a logical (JSON) snapshot of the business tables to the object
    // store, giving the owner a restorable copy independent of the managed database's own
    // snapshots. Pure .NET — no pg_dump binary or client-version coupling.
    //
    // Restore (developer, rare): download the newest backups/lopez-*.json.gz from R2,
    // gunzip, and re-import the arrays into a fresh database. See docs/railway-deploy.md.
    public class DatabaseBackupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IImageStorage _storage;
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly TimeSpan _interval;
        private readonly bool _enabled;

        private static readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public DatabaseBackupService(IServiceProvider services, IImageStorage storage,
            IConfiguration config, ILogger<DatabaseBackupService> logger)
        {
            _services = services;
            _storage = storage;
            _logger = logger;
            _enabled = config.GetValue("Backup:Enabled", true);
            int hours = config.GetValue("Backup:IntervalHours", 24);
            _interval = TimeSpan.FromHours(hours < 1 ? 1 : hours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Database backups are disabled (Backup:Enabled=false).");
                return;
            }

            // Let startup migration/seeding settle before the first run.
            try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await BackupAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    // Never let a backup failure crash the host; just log and retry next cycle.
                    _logger.LogError(ex, "Database backup failed.");
                }
                try { await Task.Delay(_interval, stoppingToken); }
                catch (TaskCanceledException) { break; }
            }
        }

        private async Task BackupAsync(CancellationToken ct)
        {
            // BackgroundService is a singleton; the DbContext is scoped — resolve per run.
            using IServiceScope scope = _services.CreateScope();
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var snapshot = new
            {
                generatedAt = DateTime.Now,
                cars = await db.Cars.AsNoTracking().Include(x => x.Pictures).ToListAsync(ct),
                sales = await db.Sales.AsNoTracking()
                    .Include(x => x.Account).ThenInclude(a => a.Payments)
                    .Include(x => x.Car)
                    .Include(x => x.TradeIn)
                    .Include(x => x.Address)
                    .Include(x => x.Lienholder).ThenInclude(l => l.Address)
                    .ToListAsync(ct),
                lienholders = await db.Lienholders.AsNoTracking().Include(x => x.Address).ToListAsync(ct),
                userAccounts = await db.UserAccounts.AsNoTracking().ToListAsync(ct),
                auditLogs = await db.AuditLogs.AsNoTracking().ToListAsync(ct)
            };

            using MemoryStream ms = new MemoryStream();
            using (GZipStream gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                await JsonSerializer.SerializeAsync(gz, snapshot, _json, ct);
            ms.Position = 0;

            string key = $"backups/lopez-{DateTime.Now:yyyyMMdd-HHmmss}.json.gz";
            await _storage.SaveAsync(key, ms, "application/gzip", ct);
            _logger.LogInformation("Database backup written to {Key} ({Bytes} bytes).", key, ms.Length);
        }
    }
}
