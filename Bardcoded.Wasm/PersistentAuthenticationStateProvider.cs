using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace Bardcoded.Wasm
{
    /// <summary>
    /// Authentication state provider that uses cookie-based authentication from the server.
    /// This is a client-side implementation that relies on the server to maintain authentication state.
    /// </summary>
    public class PersistentAuthenticationStateProvider : AuthenticationStateProvider
    {
        private static readonly Task<AuthenticationState> _unauthenticatedTask =
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // In a Blazor WebAssembly app with cookie authentication,
            // the authentication state is maintained by the server.
            // The client relies on HTTP-only cookies set by the server.
            // We return an unauthenticated state here because the actual
            // authentication check happens on the server side with each API call.
            return _unauthenticatedTask;
        }
    }
}
