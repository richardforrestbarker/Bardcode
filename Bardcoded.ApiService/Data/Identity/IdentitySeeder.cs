using Microsoft.AspNetCore.Identity;

namespace Bardcoded.ApiService.Data.Identity
{
    public class IdentitySeeder
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<IdentitySeeder> _logger;

        public IdentitySeeder(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IConfiguration configuration,
            ILogger<IdentitySeeder> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            // Create roles
            await CreateRoleIfNotExists("Owner");
            await CreateRoleIfNotExists("Admin");

            // Get default passwords from configuration
            var ownerPassword = _configuration["Identity:DefaultUsers:Owner:Password"] ?? "Owner@123456";
            var adminPassword = _configuration["Identity:DefaultUsers:Admin:Password"] ?? "Admin@123456";

            // Create owner user
            await CreateUserIfNotExists(
                "owner",
                "owner@bardcode.local",
                ownerPassword,
                "Owner",
                "Owner account for system administration");

            // Create admin user
            await CreateUserIfNotExists(
                "admin",
                "admin@bardcode.local",
                adminPassword,
                "Admin",
                "Admin account for user management");
        }

        private async Task CreateRoleIfNotExists(string roleName)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole<Guid> { Name = roleName });
                if (result.Succeeded)
                {
                    _logger.LogInformation("Role {RoleName} created successfully", roleName);
                }
                else
                {
                    _logger.LogError("Failed to create role {RoleName}: {Errors}", 
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }

        private async Task CreateUserIfNotExists(
            string userName, 
            string email, 
            string password, 
            string role,
            string? tagline = null)
        {
            var existingUser = await _userManager.FindByNameAsync(userName);
            if (existingUser == null)
            {
                var user = new ApplicationUser
                {
                    UserName = userName,
                    Email = email,
                    EmailConfirmed = true,
                    Tagline = tagline
                };

                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User {UserName} created successfully", userName);
                    
                    var roleResult = await _userManager.AddToRoleAsync(user, role);
                    if (roleResult.Succeeded)
                    {
                        _logger.LogInformation("User {UserName} added to role {RoleName}", userName, role);
                    }
                    else
                    {
                        _logger.LogError("Failed to add user {UserName} to role {RoleName}: {Errors}", 
                            userName, role, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                    }
                }
                else
                {
                    _logger.LogError("Failed to create user {UserName}: {Errors}", 
                        userName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                _logger.LogInformation("User {UserName} already exists", userName);
            }
        }
    }
}
