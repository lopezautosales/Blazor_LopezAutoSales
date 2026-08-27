using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Storage
{
    // S3-compatible image storage. Works against Cloudflare R2, AWS S3, MinIO, etc.
    public class R2ImageStorage : IImageStorage
    {
        private readonly IAmazonS3 _s3;
        private readonly ObjectStorageOptions _options;

        public R2ImageStorage(IAmazonS3 s3, IOptions<ObjectStorageOptions> options)
        {
            _s3 = s3;
            _options = options.Value;
        }

        // Stored keys historically used Windows separators ("Images\foo.jpg"); object
        // stores want forward slashes and no leading slash.
        private static string Normalize(string key) => key.Replace('\\', '/').TrimStart('/');

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = Normalize(key),
                InputStream = content,
                ContentType = contentType,
                // Cloudflare R2 implements neither streaming SigV4 payload signing
                // (STREAMING-AWS4-HMAC-SHA256-PAYLOAD) nor the SDK's default checksum;
                // send an unsigned payload with no added checksum instead.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                // Don't let the SDK dispose the caller's stream — the caller owns its
                // lifetime (e.g. the backup service reads ms.Length after this returns).
                AutoCloseStream = false
            };
            await _s3.PutObjectAsync(request, ct);
        }

        public async Task DeleteAsync(string key, CancellationToken ct = default)
        {
            await _s3.DeleteObjectAsync(_options.Bucket, Normalize(key), ct);
        }

        public string PublicUrl(string key) => $"{_options.PublicBaseUrl.TrimEnd('/')}/{Normalize(key)}";

        // Cloudflare serves a resized, format-optimized copy from the same zone via the
        // /cdn-cgi/image/ prefix; the original object is never modified.
        public string ResizedUrl(string key, int width)
            => $"{_options.PublicBaseUrl.TrimEnd('/')}/cdn-cgi/image/width={width},format=auto/{Normalize(key)}";
    }
}
