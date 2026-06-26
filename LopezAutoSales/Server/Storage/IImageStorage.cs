using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Storage
{
    // Abstraction over the object store holding car images. Keys are relative paths
    // (e.g. "Images/foo.jpg"); PublicUrl turns a key into a browser-reachable URL.
    public interface IImageStorage
    {
        Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default);

        Task<Stream> OpenReadAsync(string key, CancellationToken ct = default);

        Task DeleteAsync(string key, CancellationToken ct = default);

        Task<bool> ExistsAsync(string key, CancellationToken ct = default);

        // All object keys under a prefix (e.g. "backups/"). Order is unspecified.
        Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default);

        string PublicUrl(string key);
    }
}
