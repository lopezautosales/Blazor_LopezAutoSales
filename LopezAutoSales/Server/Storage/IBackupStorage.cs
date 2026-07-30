using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Storage
{
    // Storage for database backups, backed by a PRIVATE bucket.
    //
    // Deliberately not IImageStorage: that interface points at the public image bucket,
    // which is the wrong home for customer data. This interface exposes no PublicUrl for
    // the same reason -- there is intentionally no supported way to produce a URL for a
    // backup.
    public interface IBackupStorage
    {
        // False when the Backup section is missing credentials. Callers must refuse to
        // run rather than fall back to any other store.
        bool IsConfigured { get; }

        Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);

        // All object keys under a prefix (e.g. "backups/"). Order is unspecified.
        Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default);
    }
}
