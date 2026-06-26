using LopezAutoSales.Server.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace LopezAutoSales.Tests
{
    public class StorageTests
    {
        // PublicUrl only needs the options, not a live S3 client.
        private static R2ImageStorage Storage(string baseUrl) =>
            new R2ImageStorage(null, Options.Create(new ObjectStorageOptions { PublicBaseUrl = baseUrl }));

        [Theory]
        [InlineData("Images/foo.jpg")]            // already normalized
        [InlineData("Images\\foo.jpg")]           // legacy Windows separator
        [InlineData("/Images/foo.jpg")]           // leading slash
        public void PublicUrl_normalizes_key_separators_and_leading_slash(string key)
        {
            Assert.Equal("https://img.example.com/Images/foo.jpg", Storage("https://img.example.com").PublicUrl(key));
        }

        [Fact]
        public void PublicUrl_trims_trailing_slash_on_base()
        {
            Assert.Equal("https://img.example.com/Images/foo.jpg",
                Storage("https://img.example.com/").PublicUrl("Images/foo.jpg"));
        }
    }
}
