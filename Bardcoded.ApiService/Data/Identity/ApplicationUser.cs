using Microsoft.AspNetCore.Identity;

namespace Bardcoded.ApiService.Data.Identity
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public string? Tagline { get; set; }
    }
}
