using System.Net;
using System.Net.Http.Json;
using Aetheris.Host.Admin;
using Aetheris.Host.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// A test administrator.
/// </summary>
/// <param name="Account">The e-mail address and the password.</param>
/// <param name="Key">The authenticator key.</param>
/// <param name="AdminId">The administrator account.</param>
public sealed record TestAdmin(TestAccount Account, string Key, string AdminId);

/// <summary>
/// Helpers that create administrators and sign them in the way the admin panel does
/// (task 00007).
/// </summary>
public static class TestAdmins
{
    /// <summary>
    /// Creates an administrator with one role.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="role">The role code.</param>
    /// <returns>The administrator.</returns>
    public static async Task<TestAdmin> CreateAsync(AetherisTestFactory factory, string role)
    {
        ArgumentNullException.ThrowIfNull(factory);

        string email = TestAccounts.NewEmail();

        using IServiceScope scope = factory.Services.CreateScope();
        string key = await scope.ServiceProvider.GetRequiredService<AdminAccountService>()
            .CreateAsync(email, TestAccounts.StrongPassword, AdminNames.Display(email), [role]);

        AetherisDbContext dbContext = scope.ServiceProvider.GetRequiredService<AetherisDbContext>();
        string adminId = await dbContext.AdminAccounts
            .Where(account => dbContext.Users.Any(user => user.Id == account.IdentityUserId && user.Email == email))
            .Select(account => account.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        return new TestAdmin(new TestAccount(email, TestAccounts.StrongPassword, string.Empty), key, adminId);
    }

    /// <summary>
    /// Posts the sign-in of an administrator.
    /// </summary>
    /// <param name="client">A browser client.</param>
    /// <param name="admin">The administrator.</param>
    /// <param name="code">The one-time code, or <see langword="null"/>.</param>
    /// <param name="password">Another password than the right one, or <see langword="null"/>.</param>
    /// <returns>The response.</returns>
    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, TestAdmin admin, string? code = null, string? password = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(admin);

        return client.PostAsJsonAsync(
            AdminEndpoints.Prefix + "/auth/login",
            new { email = admin.Account.Email, password = password ?? admin.Account.Password, code },
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Signs an administrator in and prepares the client for state-changing requests.
    /// </summary>
    /// <param name="factory">The test host.</param>
    /// <param name="admin">The administrator.</param>
    /// <returns>The signed-in client.</returns>
    public static async Task<HttpClient> SignInAsync(AetherisTestFactory factory, TestAdmin admin)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(admin);

        HttpClient client = factory.CreateBrowserClient();
        HttpResponseMessage response = await LoginAsync(client, admin, TestAccounts.ComputeTotp(admin.Key));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await TestAccounts.UseCsrfTokenAsync(client);

        return client;
    }
}
