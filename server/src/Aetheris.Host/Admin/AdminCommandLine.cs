using System.Text;
using Aetheris.Core.Security;

namespace Aetheris.Host.Admin;

/// <summary>
/// The command line of the server for the tasks that have no endpoint, such as creating the
/// first administrator.
/// </summary>
/// <remarks>
/// <c>dotnet run --project src/Aetheris.Host -- create-admin [&lt;email&gt;] [&lt;role,role&gt;]</c>.
/// A missing e-mail address is asked in the console; the roles default to the main role
/// <see cref="AdminRoles.Owner"/>. The password is read from the console without echo, or from
/// the <c>AETHERIS_ADMIN_PASSWORD</c> environment variable when there is no console. Nothing
/// of it is stored in the repository. The authenticator key is printed once, to the console
/// only, never to the log.
/// </remarks>
public static class AdminCommandLine
{
    /// <summary>
    /// The command that creates an administrator.
    /// </summary>
    public const string CreateAdminCommand = "create-admin";

    /// <summary>
    /// The environment variable that holds the password when there is no console.
    /// </summary>
    public const string PasswordVariable = "AETHERIS_ADMIN_PASSWORD";

    /// <summary>
    /// The largest number of command line arguments of <see cref="CreateAdminCommand"/>, including the command.
    /// </summary>
    private const int CreateAdminMaxArgumentCount = 3;

    /// <summary>
    /// The position of the role list in the arguments of <see cref="CreateAdminCommand"/>.
    /// </summary>
    private const int RolesArgumentIndex = 2;

    /// <summary>
    /// Checks whether the arguments name a command of this class.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns><see langword="true"/> when the server should run the command and exit.</returns>
    public static bool IsCommand(string[] args) =>
        args.Length > 0 && string.Equals(args[0], CreateAdminCommand, StringComparison.Ordinal);

    /// <summary>
    /// Runs the command.
    /// </summary>
    /// <param name="services">The application services.</param>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length > CreateAdminMaxArgumentCount)
        {
            await Console.Error.WriteLineAsync(
                $"Usage: {CreateAdminCommand} [<email>] [<roles>]; the roles are codes of the admin_role table, by default {AdminRoles.Owner}.")
                .ConfigureAwait(false);

            return 1;
        }

        string email = args.Length > 1 ? args[1] : ReadLine("E-mail: ");
        string[] roles = args.Length > RolesArgumentIndex
            ? args[RolesArgumentIndex].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [AdminRoles.Owner];
        string password = Environment.GetEnvironmentVariable(PasswordVariable) ?? ReadPassword();

        if (string.IsNullOrWhiteSpace(email))
        {
            await Console.Error.WriteLineAsync("The e-mail address is empty.").ConfigureAwait(false);

            return 1;
        }

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AdminAccountService accounts = scope.ServiceProvider.GetRequiredService<AdminAccountService>();

        // The admin panel shows the name in the journals, so it never holds the full address.
        string key = await accounts.CreateAsync(email, password, AdminNames.Display(email), roles).ConfigureAwait(false);

        Console.WriteLine("Administrator created. Sign in at /admin.");
        Console.WriteLine("When admin.require_totp is on, add this key to an authenticator app:");
        Console.WriteLine($"otpauth://totp/Aetheris:{Uri.EscapeDataString(email)}?secret={key}&issuer=Aetheris");

        return 0;
    }

    private static string ReadLine(string prompt)
    {
        Console.Write(prompt);

        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private static string ReadPassword()
    {
        Console.Write("Password: ");

        StringBuilder password = new();

        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            password.Append(key.KeyChar);
        }
    }
}
