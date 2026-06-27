using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LopezAutoSales.Client
{
    // Ensures the browser sends the Identity authentication cookie with every
    // request to the server (same-origin), replacing the old bearer-token handler.
    public class CookieHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
