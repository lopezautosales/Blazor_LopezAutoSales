using LopezAutoSales.Shared.Models;
using Microsoft.AspNetCore.Components.Authorization;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading.Tasks;

namespace LopezAutoSales.Client
{
    // Determines auth state by asking the server (api/auth/me) who the current
    // cookie belongs to, instead of decoding a JWT on the client.
    public class CookieAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _http;

        public CookieAuthenticationStateProvider(AuthHttp authHttp)
        {
            _http = authHttp.Client;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            ClaimsPrincipal anonymous = new ClaimsPrincipal(new ClaimsIdentity());
            try
            {
                CurrentUser user = await _http.GetFromJsonAsync<CurrentUser>("api/auth/me");
                if (user == null || !user.IsAuthenticated)
                    return new AuthenticationState(anonymous);

                List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Name, user.Name) };
                claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
                ClaimsIdentity identity = new ClaimsIdentity(claims, "cookie", ClaimTypes.Name, ClaimTypes.Role);
                return new AuthenticationState(new ClaimsPrincipal(identity));
            }
            catch
            {
                return new AuthenticationState(anonymous);
            }
        }

        // Returns null on success, or an error message to display.
        public async Task<string> LoginAsync(LoginRequest request)
        {
            HttpResponseMessage response = await _http.PostAsJsonAsync("api/auth/login", request);
            if (response.IsSuccessStatusCode)
            {
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return null;
            }
            string message = await response.Content.ReadAsStringAsync();
            return string.IsNullOrWhiteSpace(message) ? "Login failed." : message;
        }

        public async Task LogoutAsync()
        {
            await _http.PostAsync("api/auth/logout", null);
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}
