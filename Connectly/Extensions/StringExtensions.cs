namespace Connectly;

public static class StringExtensions
{ 
    /// <summary>
    /// This method checks if a string only contains ASCII characters excluding control characters.
    /// </summary>
    /// <param name="input">The string that needs to be checked</param>
    /// <returns>True if the string only contains ASCII characters without control characters, false otherwise</returns>
    public static bool ContainsOnlyValidCharacters(this string input)
        => string.IsNullOrEmpty(input) || input.All(c => c >= 32 && c <= 126);
}