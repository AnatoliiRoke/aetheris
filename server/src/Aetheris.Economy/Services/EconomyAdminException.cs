namespace Aetheris.Economy.Services;

/// <summary>
/// The error of the admin panel of the balances (task 00015).
/// </summary>
public sealed class EconomyAdminException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EconomyAdminException"/> class.
    /// </summary>
    /// <param name="code">The code of the error.</param>
    /// <param name="message">The message of the error.</param>
    public EconomyAdminException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Gets the code of the error.</summary>
    public string Code { get; }
}
