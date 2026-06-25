# Car images on Cloudflare R2

Images are no longer stored on the server filesystem. They live in an S3-compatible
object store (Cloudflare R2). The app uploads/deletes objects via `IImageStorage`
(`Server/Storage/`) and the public site serves them from `PublicBaseUrl`.

## One-time setup

1. **Create the bucket** in the Cloudflare dashboard → R2 → *Create bucket*
   (e.g. `lopezautosales`).
2. **Enable public access** for the bucket — either:
   - Connect a **custom domain** (recommended), e.g. `images.lopezautosales.com`, or
   - Enable the **r2.dev** development URL (fine for testing; rate-limited, not for prod).
   This URL becomes `ObjectStorage:PublicBaseUrl`.
3. **Create an API token**: R2 → *Manage API Tokens* → *Create* with
   **Object Read & Write** on the bucket. Note the **Access Key ID**, **Secret Access
   Key**, and your **account ID** (the S3 endpoint is
   `https://<accountid>.r2.cloudflarestorage.com`).

## Configuration

`appsettings.json` holds the non-secret values; put **credentials** in user secrets
(dev) or environment variables (prod). Never commit keys.

```jsonc
"ObjectStorage": {
  "ServiceUrl": "https://<accountid>.r2.cloudflarestorage.com",
  "Bucket": "lopezautosales",
  "PublicBaseUrl": "https://images.lopezautosales.com"
}
```

Dev (user secrets), run from `LopezAutoSales/Server`:

```bash
dotnet user-secrets set "ObjectStorage:AccessKey" "<access-key-id>"
dotnet user-secrets set "ObjectStorage:SecretKey" "<secret-access-key>"
```

Prod (env vars — note the `__` section separator):

```
ObjectStorage__AccessKey=<access-key-id>
ObjectStorage__SecretKey=<secret-access-key>
```

## Migrating existing images (run once)

The current production images live under `wwwroot/Images/` on SmarterASP. Download that
folder (FTP) to a local `./Images`, then copy it to the bucket **preserving the
`Images/` prefix** so the keys match what's stored in the database:

Using the AWS CLI:

```bash
aws s3 sync ./Images s3://lopezautosales/Images \
  --endpoint-url https://<accountid>.r2.cloudflarestorage.com
```

Or rclone (configure an `r2` remote first):

```bash
rclone copy ./Images r2:lopezautosales/Images
```

Existing DB rows store keys like `Images\foo.jpg`; the app normalizes the backslash to
`/` when building URLs and S3 keys, so they resolve to `Images/foo.jpg` in the bucket —
no database changes needed.
