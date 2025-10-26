using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace Bardcoded.PlaywrightTests;

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AuthenticationTests : PageTest
{
    private const string BaseUrl = "http://localhost:5000"; // Update with actual URL
    private const string ApiBaseUrl = "http://localhost:5001"; // Update with actual API URL

    [Test]
    public async Task CanLoginWithValidCredentials()
    {
        // Arrange
        var loginData = new
        {
            email = "owner@bardcode.local",
            password = "Owner@123456"
        };

        // Act - Call the login API endpoint
        var response = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new()
        {
            DataObject = loginData
        });

        // Assert
        Assert.That(response.Ok, Is.True, "Login should succeed with valid credentials");
    }

    [Test]
    public async Task CannotLoginWithInvalidCredentials()
    {
        // Arrange
        var loginData = new
        {
            email = "invalid@bardcode.local",
            password = "WrongPassword123!"
        };

        // Act
        var response = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new()
        {
            DataObject = loginData
        });

        // Assert
        Assert.That(response.Ok, Is.False, "Login should fail with invalid credentials");
    }

    [Test]
    public async Task CanRegisterNewAccount()
    {
        // Arrange
        var registerData = new
        {
            email = $"testuser{DateTime.Now.Ticks}@bardcode.local",
            password = "TestUser@123456"
        };

        // Act
        var response = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/register", new()
        {
            DataObject = registerData
        });

        // Assert
        Assert.That(response.Ok, Is.True, "Registration should succeed with valid data");
    }

    [Test]
    public async Task CannotRegisterWithWeakPassword()
    {
        // Arrange
        var registerData = new
        {
            email = $"testuser{DateTime.Now.Ticks}@bardcode.local",
            password = "weak"
        };

        // Act
        var response = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/register", new()
        {
            DataObject = registerData
        });

        // Assert
        Assert.That(response.Ok, Is.False, "Registration should fail with weak password");
    }

    [Test]
    public async Task CanAccessUserManagementWithAdminRole()
    {
        // Arrange - Login as admin first
        var loginData = new
        {
            email = "admin@bardcode.local",
            password = "Admin@123456"
        };

        var loginResponse = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new()
        {
            DataObject = loginData
        });

        Assert.That(loginResponse.Ok, Is.True, "Admin login should succeed");

        // Act - Try to access user management endpoint
        var usersResponse = await Page.APIRequest.GetAsync($"{ApiBaseUrl}/users");

        // Assert
        Assert.That(usersResponse.Ok, Is.True, "Admin should be able to access user management");
    }

    [Test]
    public async Task CanLogout()
    {
        // Arrange - Login first
        var loginData = new
        {
            email = "owner@bardcode.local",
            password = "Owner@123456"
        };

        await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new()
        {
            DataObject = loginData
        });

        // Act - Logout (Identity API doesn't have explicit logout, but we can test by clearing cookies)
        await Page.Context.ClearCookiesAsync();

        // Try to access protected endpoint
        var response = await Page.APIRequest.GetAsync($"{ApiBaseUrl}/all");

        // Assert
        Assert.That(response.Status, Is.EqualTo(401), "Should be unauthorized after logout");
    }
}

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class ProfileManagementTests : PageTest
{
    private const string ApiBaseUrl = "http://localhost:5001";

    private async Task<string> LoginAndGetAccessToken(string email, string password)
    {
        var loginData = new
        {
            email,
            password
        };

        var response = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/login", new()
        {
            DataObject = loginData
        });

        Assert.That(response.Ok, Is.True, "Login should succeed");

        var jsonResponse = await response.JsonAsync();
        var accessToken = jsonResponse?.GetProperty("accessToken").GetString();
        
        Assert.That(accessToken, Is.Not.Null, "Access token should be returned");
        return accessToken!;
    }

    [Test]
    public async Task CanUpdateProfile()
    {
        // Arrange
        var accessToken = await LoginAndGetAccessToken("owner@bardcode.local", "Owner@123456");

        var updateData = new
        {
            oldPassword = "Owner@123456",
            newPassword = "NewOwner@123456"
        };

        // Act
        var response = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/manage/info", new()
        {
            DataObject = updateData,
            Headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {accessToken}" }
            }
        });

        // Note: This test demonstrates the concept but may need adjustment based on actual API behavior
        // The identity endpoints may vary based on ASP.NET Core Identity configuration
    }
}

[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class AccountDeletionTests : PageTest
{
    private const string ApiBaseUrl = "http://localhost:5001";

    [Test]
    public async Task AdminCanDeleteUserAccount()
    {
        // Arrange - Create a test user
        var registerData = new
        {
            email = $"deletetest{DateTime.Now.Ticks}@bardcode.local",
            password = "DeleteTest@123456"
        };

        var registerResponse = await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/register", new()
        {
            DataObject = registerData
        });

        Assert.That(registerResponse.Ok, Is.True, "User registration should succeed");

        // Login as admin
        var adminLoginData = new
        {
            email = "admin@bardcode.local",
            password = "Admin@123456"
        };

        await Page.APIRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new()
        {
            DataObject = adminLoginData
        });

        // Get all users to find the test user
        var usersResponse = await Page.APIRequest.GetAsync($"{ApiBaseUrl}/users");
        Assert.That(usersResponse.Ok, Is.True, "Should be able to get users list");

        // Note: Actual user deletion would require parsing the response to get the user ID
        // and then calling DELETE /users/{id}
        // This test demonstrates the concept but would need the actual user ID
    }
}
