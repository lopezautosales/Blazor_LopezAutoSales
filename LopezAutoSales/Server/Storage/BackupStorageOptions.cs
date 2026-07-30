namespace LopezAutoSales.Server.Storage
{
    // Bound from the "Backup" configuration section. Credentials should come from user
    // secrets (dev) or environment variables (prod), never appsettings.json.
    //
    // Deliberately separate from ObjectStorageOptions. Car images live in a public
    // bucket; database backups contain customer names, addresses and payment histories
    // and belong in a private one. Separate credentials keep the two from being
    // conflated.
    //
    // Note the absence of PublicBaseUrl: a backup has no public URL by design.
    public class BackupStorageOptions
    {
        // Set Backup__Enabled=false to turn backups off (e.g. local smoke tests).
        public bool Enabled { get; set; } = true;

        // Cadence in hours; default weekly.
        public int IntervalHours { get; set; } = 168;

        // R2/S3 API endpoint, e.g. https://<accountid>.r2.cloudflarestorage.com
        public string ServiceUrl { get; set; }

        // MUST be a private bucket with no public access and no custom domain.
        public string Bucket { get; set; }

        public string AccessKey { get; set; }
        public string SecretKey { get; set; }

        // Backups are optional in the sense that the app must still boot without them
        // (smoke tests, local runs). They are NOT optional in the sense of falling back
        // to another bucket -- when this is false the backup service refuses to run.
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(ServiceUrl)
            && !string.IsNullOrWhiteSpace(Bucket)
            && !string.IsNullOrWhiteSpace(AccessKey)
            && !string.IsNullOrWhiteSpace(SecretKey);
    }
}
