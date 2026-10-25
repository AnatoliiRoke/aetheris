using System.Text;
using Aetheris.Core.Security;

namespace Aetheris.Host.Admin;

/// <summary>
/// The command line of the server for the tasks that have no endpoint, such as creating the
/// first administrator.
/// </summary>
/// <remarks>
/// <c>dotnet run --project src/Aetheris.Host -- create-admin &lt;email&gt; &lt;role,role&gt;</c>.
/// The password is read from the console without echo, or from the
/// <c>AETHERIS_ADMIN_PASSWORD</c> environment variable when there is no console. The
/// authenticator key is printed once, to the console only, never to the log.
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
    /// The number of command line arguments of <see cref="CreateAdminCommand"/>, including the command.
    /// </summary>
    private const int CreateAdminArgumentCount = 3;

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

        if (args.Length != CreateAdminArgumentCount)
        {
            await Console.Error.WriteLineAsync(
                $"Usage: {CreateAdminCommand} <email> <roles>, roles from: {string.Join(", ", AdminRoles.All)}.")
                .ConfigureAwait(false);

            return 1;
        }

        string email = args[1];
        string[] roles = args[RolesArgumentIndex].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string password = Environment.GetEnvironmentVariable(PasswordVariable) ?? ReadPassword();

        await using AsyncServiceScope scope = services.CreateAsyncScope();
        AdminAccountService accounts = scope.ServiceProvider.GetRequiredService<AdminAccountService>();

        string key = await accounts.CreateAsync(email, password, email, roles).ConfigureAwait(false);

        Console.WriteLine("Administrator created. Add this key to an authenticator app:");
        Console.WriteLine($"otpauth://totp/Aetheris:{Uri.EscapeDataString(email)}?secret={key}&issuer=Aetheris");

        return 0;
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
