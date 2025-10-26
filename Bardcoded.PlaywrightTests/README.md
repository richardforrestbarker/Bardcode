# Bardcode Playwright Tests

This project contains end-to-end tests for the Bardcode application using Playwright.

## Prerequisites

Before running the tests, you need to install Playwright browsers:

```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

Or on Linux/macOS:

```bash
pwsh bin/Debug/net9.0/playwright.ps1 install
```

If you don't have PowerShell installed, you can use the Playwright CLI:

```bash
playwright install
```

## Running the Tests

First, ensure the Bardcode application is running:

```bash
# In the Bardcode root directory
dotnet run --project Bardcoded.AppHost/Bardcoded.AppHost.csproj
```

Then, in a separate terminal, run the tests:

```bash
# Run all tests
dotnet test Bardcoded.PlaywrightTests

# Run specific test
dotnet test Bardcoded.PlaywrightTests --filter "FullyQualifiedName~CanLoginWithValidCredentials"

# Run tests in headed mode (shows browser)
HEADED=1 dotnet test Bardcoded.PlaywrightTests
```

## Test Coverage

The Playwright tests cover the following authentication and authorization flows:

### Authentication Tests
- **Login**: Tests successful login with valid credentials
- **Invalid Login**: Tests that login fails with invalid credentials
- **Registration**: Tests creating a new user account
- **Weak Password**: Tests that registration fails with passwords that don't meet requirements
- **User Management Access**: Tests that admin users can access user management endpoints
- **Logout**: Tests logout functionality

### Profile Management Tests
- **Update Profile**: Tests updating user profile information
- **Change Password**: Tests password change functionality

### Account Deletion Tests
- **Delete Account**: Tests that admins can delete user accounts

## Configuration

Update the base URLs in the test files to match your environment:

```csharp
private const string BaseUrl = "http://localhost:5000"; // Frontend URL
private const string ApiBaseUrl = "http://localhost:5001"; // API URL
```

## Test Structure

Tests are organized into three main fixtures:
- `AuthenticationTests`: Core authentication functionality
- `ProfileManagementTests`: User profile and password management
- `AccountDeletionTests`: Account deletion functionality

## Notes

- Tests use the default admin and owner accounts configured in the application
- Some tests create temporary test users that may persist in the database
- The tests demonstrate the authentication API but may need adjustments based on your specific deployment configuration
- For CI/CD environments, ensure Playwright browsers are installed as part of the build process

## Troubleshooting

If tests fail:

1. Ensure the application is running and accessible at the configured URLs
2. Verify the default admin/owner accounts exist with the configured passwords
3. Check that Playwright browsers are installed
4. Review test output for specific error messages
5. Run tests in headed mode to see what's happening in the browser
