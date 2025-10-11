namespace Connectly;

public static class StringExtensions
{ 
    /// <summary>
    /// This method checks if a string only contains printable ASCII characters (ASCII codes 32-126).
    /// </summary>
    /// <param name="input">The string that needs to be checked</param>
    /// <returns>True if the string only contains printable ASCII characters (space through tilde), false otherwise</returns>
    public static bool ContainsPrintableValidCharacters(this string input)
        => string.IsNullOrEmpty(input) || input.All(c => c >= 32 && c <= 126);
}