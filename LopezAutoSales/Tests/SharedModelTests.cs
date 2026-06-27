using LopezAutoSales.Shared.Models;
using Xunit;

namespace LopezAutoSales.Tests
{
    public class SharedModelTests
    {
        [Fact]
        public void Address_formats_full_and_area()
        {
            Address address = new Address { Street = "515 Albert St", City = "Emporia", State = "Kansas", ZIP = "66801" };

            Assert.Equal("515 Albert St, Emporia, Kansas, 66801", address.ToString());
            Assert.Equal("Emporia, Kansas, 66801", address.Area());
        }
    }
}
