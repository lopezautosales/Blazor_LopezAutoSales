namespace LopezAutoSales.Server.Storage
{
    // Bound from the "ObjectStorage" configuration section. Credentials should come
    // from user secrets (dev) or environment variables (prod), never appsettings.json.
    public class ObjectStorageOptions
    {
        // R2/S3 API endpoint, e.g. https://<accountid>.r2.cloudflarestorage.com
        public string ServiceUrl { get; set; }
        public string Bucket { get; set; }
        // Public base URL images are served from, e.g. https://images.lopezautosales.com
        public string PublicBaseUrl { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
    }
}
