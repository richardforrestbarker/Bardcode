using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;

namespace Bardcoded.ApiService.Authorization
{
    /// <summary>
    /// Authorization attribute that only enforces authorization when the AuthNZ feature flag is enabled.
    /// When AuthNZ is disabled, this attribute does nothing and allows all requests through.
    /// </summary>
    public class ConditionalAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string? _policy;
        private readonly string[]? _roles;

        public ConditionalAuthorizeAttribute()
        {
        }

        public ConditionalAuthorizeAttribute(string policy)
        {
            _policy = policy;
        }

        public ConditionalAuthorizeAttribute(params string[] roles)
        {
            _roles = roles;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var authNZEnabled = configuration.GetValue<bool>("Application:Features:AuthNZ", false);

            if (!authNZEnabled)
            {
                // AuthNZ is disabled, allow all requests
                return;
            }

            // AuthNZ is enabled, perform authorization check
            var authorizationService = context.HttpContext.RequestServices.GetRequiredService<IAuthorizationService>();
            var user = context.HttpContext.User;

            // Check if user is authenticated
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            // Check policy if specified
            if (!string.IsNullOrEmpty(_policy))
            {
                var policyResult = authorizationService.AuthorizeAsync(user, _policy).Result;
                if (!policyResult.Succeeded)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            // Check roles if specified
            if (_roles != null && _roles.Length > 0)
            {
                var hasRole = false;
                foreach (var role in _roles)
                {
                    if (user.IsInRole(role))
                    {
                        hasRole = true;
                        break;
                    }
                }

                if (!hasRole)
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }
        }
    }
}
