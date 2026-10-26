using System.Globalization;
using System.Text;

namespace Aetheris.Core.Admin;

/// <summary>
/// One page of an admin table: the page number, its size, the search text and the sort order
/// (task 00007, section 3.1).
/// </summary>
/// <param name="Page">The page number, from one.</param>
/// <param name="PageSize">The number of rows on a page.</param>
/// <param name="Search">The search text, or <see langword="null"/>.</param>
/// <param name="Sort">The sort column, or <see langword="null"/> for the default order.</param>
/// <param name="Descending">Whether the order is descending.</param>
public sealed record PageRequest(int Page, int PageSize, string? Search, string? Sort, bool Descending)
{
    /// <summary>
    /// Gets the number of rows before the page.
    /// </summary>
    public int Skip => (Page - 1) * PageSize;
}

/// <summary>
/// One page of rows of an admin table.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
/// <param name="Items">The rows of the page.</param>
/// <param name="Total">The number of rows that match the filters.</param>
/// <param name="Page">The page number.</param>
/// <param name="PageSize">The number of rows on a page.</param>
public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>
/// The limits of the admin tables, read from the configuration by the host.
/// </summary>
public interface IAdminQueryLimits
{
    /// <summary>
    /// Gets the largest number of rows returned at once.
    /// </summary>
    int MaxPageSize { get; }

    /// <summary>
    /// Gets the largest length of the search text.
    /// </summary>
    int SearchMaxLength { get; }

    /// <summary>
    /// Gets the smallest length of the reason of an administrator change.
    /// </summary>
    int ReasonMinLength { get; }

    /// <summary>
    /// Gets the largest length of the reason of an administrator change.
    /// </summary>
    int ReasonMaxLength { get; }
}

/// <summary>
/// The reason of an administrator change: mandatory, with its length from the configuration
/// (task 00007, decision 7; task 00009, decision 3).
/// </summary>
public static class AdminReason
{
    /// <summary>The error code of a missing reason.</summary>
    public const string Required = "required";

    /// <summary>The error code of a reason that is too short.</summary>
    public const string TooShort = "too_short";

    /// <summary>The error code of a reason that is too long.</summary>
    public const string TooLong = "too_long";

    /// <summary>
    /// Checks a reason.
    /// </summary>
    /// <param name="reason">The reason as it was sent.</param>
    /// <param name="limits">The limits from the configuration.</param>
    /// <returns>The error code, or <see langword="null"/> when the reason is accepted.</returns>
    public static string? Check(string? reason, IAdminQueryLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Required;
        }

        int length = reason.Trim().Length;

        if (length < limits.ReasonMinLength)
        {
            return TooShort;
        }

        return length > limits.ReasonMaxLength ? TooLong : null;
    }
}

/// <summary>
/// Builds a <see cref="PageRequest"/> from the query string of an admin table.
/// </summary>
public static class AdminPaging
{
    /// <summary>
    /// The value of the direction parameter that asks for a descending order.
    /// </summary>
    public const string DescendingDirection = "desc";

    /// <summary>
    /// The error code of a search text that is too long.
    /// </summary>
    public const string SearchTooLong = "search_too_long";

    /// <summary>
    /// The escape character of the patterns built by <see cref="ContainsPattern"/>.
    /// </summary>
    public const string LikeEscape = "\\";

    /// <summary>
    /// Builds a page request. A missing or too large page size becomes the largest one, a
    /// missing or too small page becomes the first one.
    /// </summary>
    /// <param name="page">The requested page.</param>
    /// <param name="pageSize">The requested page size.</param>
    /// <param name="search">The search text.</param>
    /// <param name="sort">The sort column.</param>
    /// <param name="direction">The direction, <see cref="DescendingDirection"/> for a descending order.</param>
    /// <param name="limits">The limits from the configuration.</param>
    /// <returns>The request, or <see langword="null"/> when the search text is too long.</returns>
    public static PageRequest? Create(
        int? page,
        int? pageSize,
        string? search,
        string? sort,
        string? direction,
        IAdminQueryLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        string? text = string.IsNullOrWhiteSpace(search) ? null : search.Trim();

        if (text is not null && text.Length > limits.SearchMaxLength)
        {
            return null;
        }

        int size = pageSize is null or < 1 ? limits.MaxPageSize : Math.Min(pageSize.Value, limits.MaxPageSize);

        return new PageRequest(
            page is null or < 1 ? 1 : page.Value,
            Math.Max(1, size),
            text,
            string.IsNullOrWhiteSpace(sort) ? null : sort.Trim(),
            string.Equals(direction, DescendingDirection, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Builds a case-insensitive <c>LIKE</c> pattern that finds the text anywhere, with the
    /// wildcards of the text escaped by <see cref="LikeEscape"/>.
    /// </summary>
    /// <param name="search">The search text.</param>
    /// <returns>The pattern.</returns>
    public static string ContainsPattern(string search)
    {
        ArgumentNullException.ThrowIfNull(search);

        StringBuilder pattern = new("%");

        foreach (char character in search)
        {
            if (character is '%' or '_' or '\\')
            {
                pattern.Append(LikeEscape);
            }

            pattern.Append(character);
        }

        return pattern.Append('%').ToString();
    }

    /// <summary>
    /// Reads the search text as a whole number, for the tables that can be searched by an identifier.
    /// </summary>
    /// <param name="search">The search text.</param>
    /// <returns>The number, or <see langword="null"/> when the text is not one.</returns>
    public static long? AsNumber(string? search) =>
        long.TryParse(search, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) ? number : null;
}
