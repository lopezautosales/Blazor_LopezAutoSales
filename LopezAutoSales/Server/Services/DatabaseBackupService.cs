using LopezAutoSales.Server.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
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
    // Writes through IBackupStorage, which is backed by a private bucket. Snapshots hold
    // customer names, addresses and payment histories, so they must never be written
    // through IImageStorage — that bucket is public by design. If backup storage is
    // unconfigured this service refuses to run rather than fall back to another store.
    //
    // Restore (developer, rare): download the newest backups/lopez-*.json.gz from the
    // private bucket, gunzip, and re-import the arrays into a fresh database.
    public class DatabaseBackupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IBackupStorage _storage;
        private readonly ILogger<DatabaseBackupService> _logger;
        private readonly TimeSpan _interval;
        private readonly bool _enabled;

        private static readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public DatabaseBackupService(IServiceProvider services, IBackupStorage storage,
            IOptions<BackupStorageOptions> options, ILogger<DatabaseBackupService> logger)
        {
            _services = services;
            _storage = storage;
            _logger = logger;
            BackupStorageOptions o = options.Value;
            _enabled = o.Enabled;
            // Default cadence is weekly (168h); override with Backup__IntervalHours.
            _interval = TimeSpan.FromHours(o.IntervalHours < 1 ? 1 : o.IntervalHours);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_enabled)
            {
                _logger.LogInformation("Database backups are disabled (Backup__Enabled=false).");
                return;
            }

            // Fail closed: a missing Backup section means no backups at all, never a
            // fallback to any other store.
            if (!_storage.IsConfigured)
            {
                _logger.LogError("Database backups are NOT running: the Backup section is missing "
                    + "Backup__ServiceUrl / Backup__Bucket / Backup__AccessKey / Backup__SecretKey.");
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

        // backups/lopez-yyyyMMdd-HHmmss.json.gz — the timestamp is the generation time.
        private const string Prefix = "backups/lopez-";
        private const string Suffix = ".json.gz";
        private const string Stamp = "yyyyMMdd-HHmmss";

        // Newest backup's generation time parsed from its key name, or null if none exist.
        private async Task<DateTime?> LatestBackupTimeAsync(CancellationToken ct)
        {
            DateTime? latest = null;
            foreach (string key in await _storage.ListKeysAsync(Prefix, ct))
            {
                string name = key.Replace('\\', '/');
                if (!name.StartsWith(Prefix) || !name.EndsWith(Suffix))
                    continue;
                string stamp = name.Substring(Prefix.Length, name.Length - Prefix.Length - Suffix.Length);
                if (DateTime.TryParseExact(stamp, Stamp, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTime when) && (latest is null || when > latest))
                    latest = when;
            }
            return latest;
        }

        private async Task BackupAsync(CancellationToken ct)
        {
            // Skip if a backup already exists from within the current interval — keeps
            // frequent restarts from each spawning a near-duplicate snapshot.
            DateTime? latest = await LatestBackupTimeAsync(ct);
            if (latest is DateTime t && DateTime.Now - t < _interval)
            {
                _logger.LogInformation("Skipping backup; most recent ({Latest:s}) is younger than the {Interval} interval.",
                    t, _interval);
                return;
            }

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
