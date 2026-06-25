using LopezAutoSales.Server.Data;
using Npgsql;
using Xunit;

namespace LopezAutoSales.Tests
{
    public class NpgsqlUrlTests
    {
        [Fact]
        public void Parses_full_url()
        {
            string cs = NpgsqlUrl.ToConnectionString("postgresql://bob:s3cret@db.example.com:6543/railway");
            NpgsqlConnectionStringBuilder b = new NpgsqlConnectionStringBuilder(cs);

            Assert.Equal("db.example.com", b.Host);
            Assert.Equal(6543, b.Port);
            Assert.Equal("railway", b.Database);
            Assert.Equal("bob", b.Username);
            Assert.Equal("s3cret", b.Password);
        }

        [Fact]
        public void Defaults_port_when_missing()
        {
            NpgsqlConnectionStringBuilder b = new NpgsqlConnectionStringBuilder(
                NpgsqlUrl.ToConnectionString("postgresql://u:p@localhost/app"));

            Assert.Equal(5432, b.Port);
        }

        [Fact]
        public void Url_decodes_password()
        {
            NpgsqlConnectionStringBuilder b = new NpgsqlConnectionStringBuilder(
                NpgsqlUrl.ToConnectionString("postgresql://u:p%40ss%3Aword@host:5432/db"));

            Assert.Equal("p@ss:word", b.Password);
        }
    }
}
