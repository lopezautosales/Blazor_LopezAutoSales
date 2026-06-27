namespace LopezAutoSales.Server
{
    public static class Constants
    {
        // Width (px) requested for the inventory-grid card image via Cloudflare resizing.
        public const int ThumbnailSize = 400;

        // Width (px) for the vehicle-detail carousel + social preview image. Plenty for a
        // full-width photo on a phone/laptop without shipping the multi-MB original.
        public const int DetailSize = 1000;
    }
}
