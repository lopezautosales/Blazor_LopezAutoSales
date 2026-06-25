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
        private static readonly AuthenticationState Anonymous =
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        private readonly HttpClient _http;
        private AuthenticationState _lastKnown;

        public CookieAuthenticationStateProvider(AuthHttp authHttp)
        {
            _http = authHttp.Client;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                CurrentUser user = await _http.GetFromJsonAsync<CurrentUser>("api/auth/me");
                AuthenticationState state;
                if (user == null || !user.IsAuthenticated)
                {
                    state = Anonymous;
                }
                else
                {
                    List<Claim> claims = new List<Claim> { new Claim(ClaimTypes.Name, user.Name) };
                    claims.AddRange(user.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
                    ClaimsIdentity identity = new ClaimsIdentity(claims, "cookie", ClaimTypes.Name, ClaimTypes.Role);
                    state = new AuthenticationState(new ClaimsPrincipal(identity));
                }
                _lastKnown = state;
                return state;
            }
            catch
            {
                // Transient failure (network/5xx) — don't forcibly sign the user out;
                // reuse the last known state if we have one.
                return _lastKnown ?? Anonymous;
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
            try { await _http.PostAsync("api/auth/logout", null); } catch { }
            // Reflect signed-out immediately, even if the server call hiccuped.
            _lastKnown = Anonymous;
            NotifyAuthenticationStateChanged(Task.FromResult(Anonymous));
        }
    }
}
