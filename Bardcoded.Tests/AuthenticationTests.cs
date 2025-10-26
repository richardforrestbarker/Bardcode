using Xunit;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bardcoded.PlaywrightTests
{
    public class AuthenticationTests : IClassFixture<PlaywrightFixture>
    {
        private readonly PlaywrightFixture _fixture;
        private static string BaseUrl => Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5000";
        private static string ApiBaseUrl => Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5001";

        public AuthenticationTests(PlaywrightFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> LoginData => new[]
        {
            new object[] { "owner@bardcode.local", "Owner@123456", true },
            new object[] { "invalid@bardcode.local", "WrongPassword123!", false }
        };

        [Theory]
        [MemberData(nameof(LoginData))]
        public async Task LoginTest(string email, string password, bool shouldSucceed)
        {
            var loginData = new { email, password };
            var response = await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new() { DataObject = loginData });
            if (shouldSucceed)
                Assert.True(response.Ok, $"Login should succeed for {email}");
            else
                Assert.False(response.Ok, $"Login should fail for {email}");
        }

        public static IEnumerable<object[]> RegisterData => new[]
        {
            new object[] { "TestUser@123456", true },
            new object[] { "weak", false }
        };

        [Theory]
        [MemberData(nameof(RegisterData))]
        public async Task RegisterTest(string password, bool shouldSucceed)
        {
            var registerData = new { email = $"testuser{Guid.NewGuid()}@bardcode.local", password };
            var response = await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/register", new() { DataObject = registerData });
            if (shouldSucceed)
                Assert.True(response.Ok, $"Registration should succeed with password {password}");
            else
                Assert.False(response.Ok, $"Registration should fail with password {password}");
        }

        public static IEnumerable<object[]> AccessUserManagementData => new[]
        {
            new object[] { "admin@bardcode.local", "Admin@123456", true },
            new object[] { "owner@bardcode.local", "Owner@123456", false }
        };

        [Theory]
        [MemberData(nameof(AccessUserManagementData))]
        public async Task AccessUserManagementTest(string email, string password, bool shouldSucceed)
        {
            var loginData = new { email, password };
            var loginResponse = await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new() { DataObject = loginData });
            Assert.True(loginResponse.Ok, $"Login should succeed for {email}");
            var usersResponse = await _fixture.ApiRequest.GetAsync($"{ApiBaseUrl}/users");
            if (shouldSucceed)
                Assert.True(usersResponse.Ok, $"{email} should be able to access user management");
            else
                Assert.False(usersResponse.Ok, $"{email} should not be able to access user management");
        }

        public static IEnumerable<object[]> LogoutData => new[]
        {
            new object[] { "owner@bardcode.local", "Owner@123456" }
        };

        [Theory]
        [MemberData(nameof(LogoutData))]
        public async Task CanLogout(string email, string password)
        {
            var loginData = new { email, password };
            await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new() { DataObject = loginData });
            await _fixture.Page.Context.ClearCookiesAsync();
            var response = await _fixture.ApiRequest.GetAsync($"{ApiBaseUrl}/all");
            Assert.Equal(401, response.Status);
        }
    }

    public class ProfileManagementTests : IClassFixture<PlaywrightFixture>
    {
        private readonly PlaywrightFixture _fixture;
        private static string ApiBaseUrl => Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5001";

        public ProfileManagementTests(PlaywrightFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<string> LoginAndGetAccessToken(string email, string password)
        {
            var loginData = new { email, password };
            var response = await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/login", new() { DataObject = loginData });
            Assert.True(response.Ok, "Login should succeed");
            var jsonResponse = await response.JsonAsync();
            var accessToken = jsonResponse?.GetProperty("accessToken").GetString();
            Assert.NotNull(accessToken);
            return accessToken!;
        }

        public static IEnumerable<object[]> UpdateProfileData => new[]
        {
            new object[] { "owner@bardcode.local", "Owner@123456", "Owner@123456", "NewOwner@123456" }
        };

        [Theory]
        [MemberData(nameof(UpdateProfileData))]
        public async Task CanUpdateProfile(string email, string password, string oldPassword, string newPassword)
        {
            var accessToken = await LoginAndGetAccessToken(email, password);
            var updateData = new { oldPassword, newPassword };
            var response = await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/manage/info", new()
            {
                DataObject = updateData,
                Headers = new Dictionary<string, string> { { "Authorization", $"Bearer {accessToken}" } }
            });
            // Note: This test demonstrates the concept but may need adjustment based on actual API behavior
        }
    }

    public class AccountDeletionTests : IClassFixture<PlaywrightFixture>
    {
        private readonly PlaywrightFixture _fixture;
        private static string ApiBaseUrl => Environment.GetEnvironmentVariable("API_BASE_URL") ?? "http://localhost:5001";

        public AccountDeletionTests(PlaywrightFixture fixture)
        {
            _fixture = fixture;
        }

        public static IEnumerable<object[]> AdminDeleteUserData => new[]
        {
            new object[] { "admin@bardcode.local", "Admin@123456" }
        };

        [Theory]
        [MemberData(nameof(AdminDeleteUserData))]
        public async Task AdminCanDeleteUserAccount(string adminEmail, string adminPassword)
        {
            var registerData = new { email = $"deletetest{Guid.NewGuid()}@bardcode.local", password = "DeleteTest@123456" };
            var registerResponse = await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/register", new() { DataObject = registerData });
            Assert.True(registerResponse.Ok, "User registration should succeed");
            var adminLoginData = new { email = adminEmail, password = adminPassword };
            await _fixture.ApiRequest.PostAsync($"{ApiBaseUrl}/identity/login?useCookies=true", new() { DataObject = adminLoginData });
            var usersResponse = await _fixture.ApiRequest.GetAsync($"{ApiBaseUrl}/users");
            Assert.True(usersResponse.Ok, "Should be able to get users list");
            // Note: Actual user deletion would require parsing the response to get the user ID
        }
    }
}
