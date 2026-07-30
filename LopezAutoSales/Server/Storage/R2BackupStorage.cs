using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LopezAutoSales.Server.Storage
{
    // S3-compatible backup storage (Cloudflare R2). Builds its own S3 client from the
    // Backup section rather than reusing the registered IAmazonS3, which carries the
    // image bucket's credentials -- the two never share a client, bucket, or token.
    public class R2BackupStorage : IBackupStorage, IDisposable
    {
        private readonly BackupStorageOptions _options;
        private readonly IAmazonS3 _s3;

        public R2BackupStorage(IOptions<BackupStorageOptions> options)
        {
            _options = options.Value;
            // Only build a client when configured: the app must still boot with the
            // Backup section empty (local runs, smoke tests).
            if (_options.IsConfigured)
            {
                AmazonS3Config config = new AmazonS3Config
                {
                    ServiceURL = _options.ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = "auto",
                    // R2 doesn't implement the SDK's newer streaming checksum trailer, so
                    // uploads fail with 501 unless checksums are only sent when required.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
                };
                _s3 = new AmazonS3Client(new BasicAWSCredentials(_options.AccessKey, _options.SecretKey), config);
            }
        }

        public bool IsConfigured => _options.IsConfigured;

        private static string Normalize(string key) => key.Replace('\\', '/').TrimStart('/');

        private IAmazonS3 Client => _s3
            ?? throw new InvalidOperationException("Backup storage is not configured; check IsConfigured before use.");

        public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken ct = default)
        {
            PutObjectRequest request = new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = Normalize(key),
                InputStream = content,
                ContentType = contentType,
                // R2 implements neither streaming SigV4 payload signing nor the SDK's
                // default checksum; send an unsigned payload with no added checksum.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                // The caller owns the stream's lifetime (it reads Length afterwards).
                AutoCloseStream = false
            };
            await Client.PutObjectAsync(request, ct);
        }

        public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct = default)
        {
            List<string> keys = new List<string>();
            ListObjectsV2Request request = new ListObjectsV2Request
            {
                BucketName = _options.Bucket,
                Prefix = Normalize(prefix)
            };
            ListObjectsV2Response response;
            do
            {
                response = await Client.ListObjectsV2Async(request, ct);
                foreach (S3Object obj in response.S3Objects)
                    keys.Add(obj.Key);
                request.ContinuationToken = response.NextContinuationToken;
            } while (response.IsTruncated == true);
            return keys;
        }

        public void Dispose() => _s3?.Dispose();
    }
}
