using Aetheris.Core.Text;

namespace Aetheris.Tests.Configuration;

/// <summary>
/// Tests of the definition of allowed characters that the database keeps for the nickname and
/// the name of the home body (task 00006, decision 7).
/// </summary>
public sealed class AllowedCharactersTests
{
    /// <summary>
    /// Ranges, single characters and the dash at the end are read as written.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="text">The checked text.</param>
    /// <param name="expected">Whether the text is allowed.</param>
    [Theory]
    [InlineData("A-Za-zЀ-ӿ0-9 _-", "Їжак_1 Ok-2", true)]
    [InlineData("A-Za-zЀ-ӿ0-9 _-", "Кам'яна", false)]
    [InlineData("A-Za-zЀ-ӿ0-9 _'’-", "Кам'яна", true)]
    [InlineData("A-Za-zЀ-ӿ0-9 _'’-", "Кам’яна", true)]
    [InlineData("A-Za-zЀ-ӿ0-9 _'’-", "Кам\"яна", false)]
    [InlineData("-a-z", "a-b", true)]
    [InlineData("a-z", "a-b", false)]
    [InlineData("z-a", "b", false)]
    [InlineData("", "a", false)]
    [InlineData("a-z", "", false)]
    [InlineData("a-z", "a😀", false)]
    [InlineData("a-z😀", "a😀", true)]
    public void A_definition_is_read_as_written(string definition, string text, bool expected)
    {
        Assert.Equal(expected, AllowedCharacters.Parse(definition).IsMatch(text));
    }
}
