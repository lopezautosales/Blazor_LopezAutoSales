using Npgsql;
using System;

namespace LopezAutoSales.Server.Data
{
    public static class NpgsqlUrl
    {
        // Converts a URL-form DATABASE_URL (postgresql://user:pass@host:port/db, as
        // Railway provides) into an Npgsql key/value connection string.
        public static string ToConnectionString(string url)
        {
            Uri uri = new Uri(url);
            string[] userInfo = uri.UserInfo.Split(':');
            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
                Database = uri.AbsolutePath.TrimStart('/'),
                // Railway's internal network doesn't offer TLS; Prefer encrypts when
                // available without breaking the internal connection (Require would).
                SslMode = SslMode.Prefer
            }.ConnectionString;
        }
    }
}
