using Bardcoded.ApiService.Data.Identity;
using Bardcoded.ApiService.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bardcoded.ApiService.Controllers
{
    [Route("/users")]
    [ApiController]
    [Produces("application/json")]
    [ConditionalAuthorize("UserManagement")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<UsersController> _logger;

        public UsersController(UserManager<ApplicationUser> userManager, ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>
        /// Gets all users.
        /// </summary>
        /// <returns>A list of all users</returns>
        /// <response code="200">The list of users.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<UserDto>), 200)]
        public async Task<IResult> GetAllUsers()
        {
            var users = await _userManager.Users
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    UserName = u.UserName!,
                    Email = u.Email!,
                    Tagline = u.Tagline
                })
                .ToListAsync();

            return Results.Ok(users);
        }

        /// <summary>
        /// Gets a user by ID.
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>The user details</returns>
        /// <response code="200">The user.</response>
        /// <response code="404">If the user is not found.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserDto), 200)]
        [ProducesResponseType(404)]
        public async Task<IResult> GetUser(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return Results.NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            
            return Results.Ok(new UserDto
            {
                Id = user.Id,
                UserName = user.UserName!,
                Email = user.Email!,
                Tagline = user.Tagline,
                Roles = roles.ToList()
            });
        }

        /// <summary>
        /// Deletes a user.
        /// </summary>
        /// <param name="id">The user ID</param>
        /// <returns>No content if successful</returns>
        /// <response code="204">User deleted successfully.</response>
        /// <response code="404">If the user is not found.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IResult> DeleteUser(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return Results.NotFound();
            }

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} deleted successfully", id);
                return Results.NoContent();
            }

            return Results.BadRequest(new { Errors = result.Errors.Select(e => e.Description) });
        }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public required string UserName { get; set; }
        public required string Email { get; set; }
        public string? Tagline { get; set; }
        public List<string>? Roles { get; set; }
    }
}
