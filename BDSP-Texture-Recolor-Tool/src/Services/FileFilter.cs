using System.Text.RegularExpressions;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for filtering and discovering Pokemon bundle files
/// </summary>
public class FileFilter
{
    /// <summary>
    /// Primary pattern for Pokemon bundle files: pm####_##_##
    /// </summary>
    public static readonly string PrimaryPattern = @"^pm\d{4}_\d{2}_\d{2}$";

    /// <summary>
    /// Fallback pattern for Pokemon bundle files: pm####_##
    /// </summary>
    public static readonly string FallbackPattern = @"^pm\d{4}_\d{2}$";

    private readonly Regex _primaryPattern;
    private readonly Regex _fallbackPattern;

    public FileFilter()
    {
        _primaryPattern = new Regex(PrimaryPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        _fallbackPattern = new Regex(FallbackPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Find all Pokemon bundle files in the specified directory
    /// Processes both pm####_##_## and pm####_## files independently
    /// </summary>
    /// <param name="inputPath">Directory to search</param>
    /// <returns>List of matching file paths</returns>
    public List<string> FindPokemonBundles(string inputPath)
    {
        if (!Directory.Exists(inputPath))
        {
            return new List<string>();
        }

        var allFiles = Directory.GetFiles(inputPath, "*", SearchOption.TopDirectoryOnly);
        var matchingBundles = new List<string>();

        foreach (var filePath in allFiles)
        {
            var fileName = Path.GetFileName(filePath);

            // Include both primary (pm####_##_##) and fallback (pm####_##) files
            if (IsPrimaryPokemonBundle(fileName) || IsFallbackPokemonBundle(fileName))
            {
                matchingBundles.Add(filePath);
            }
        }

        // Sort for consistent processing order
        matchingBundles.Sort();
        return matchingBundles;
    }

    /// <summary>
    /// Check if a filename matches the primary Pokemon bundle pattern (pm####_##_##)
    /// </summary>
    /// <param name="fileName">File name to check</param>
    /// <returns>True if matches primary pattern</returns>
    public bool IsPrimaryPokemonBundle(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return _primaryPattern.IsMatch(fileName);
    }

    /// <summary>
    /// Check if a filename matches the fallback Pokemon bundle pattern (pm####_##)
    /// </summary>
    /// <param name="fileName">File name to check</param>
    /// <returns>True if matches fallback pattern</returns>
    public bool IsFallbackPokemonBundle(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        return _fallbackPattern.IsMatch(fileName);
    }

    /// <summary>
    /// Check if a filename matches either Pokemon bundle pattern
    /// </summary>
    /// <param name="fileName">File name to check</param>
    /// <returns>True if matches any pattern</returns>
    public bool IsValidPokemonBundle(string fileName)
    {
        return IsPrimaryPokemonBundle(fileName) || IsFallbackPokemonBundle(fileName);
    }

    /// <summary>
    /// Extract base pattern (pm####_##) from filename
    /// </summary>
    /// <param name="fileName">File name to extract from</param>
    /// <returns>Base pattern or null if not a fallback file</returns>
    private string? ExtractBasePattern(string fileName)
    {
        if (IsFallbackPokemonBundle(fileName))
        {
            return fileName; // The entire filename is the base pattern for fallback files
        }
        return null;
    }

    /// <summary>
    /// Extract bundle ID from Pokemon bundle filename
    /// </summary>
    /// <param name="fileName">Pokemon bundle filename</param>
    /// <returns>Bundle ID (e.g., "0001" from "pm0001_00_00") or null if invalid</returns>
    public string? GetBundleId(string fileName)
    {
        if (!IsValidPokemonBundle(fileName))
            return null;

        // Extract the numeric part after "pm"
        var match = Regex.Match(fileName, @"^pm(\d{4})_\d{2}_\d{2}$");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extract variant information from Pokemon bundle filename
    /// </summary>
    /// <param name="fileName">Pokemon bundle filename</param>
    /// <returns>Variant info (e.g., "00_00" from "pm0001_00_00") or null if invalid</returns>
    public string? GetVariantInfo(string fileName)
    {
        if (!IsValidPokemonBundle(fileName))
            return null;

        // Extract the variant part after the bundle ID
        var match = Regex.Match(fileName, @"^pm\d{4}_(\d{2}_\d{2})$");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Get statistics about Pokemon bundles in a directory
    /// </summary>
    /// <param name="inputPath">Directory to analyze</param>
    /// <returns>Statistics about found bundles</returns>
    public BundleDiscoveryStats GetBundleStats(string inputPath)
    {
        var stats = new BundleDiscoveryStats();

        if (!Directory.Exists(inputPath))
        {
            return stats;
        }

        var allFiles = Directory.GetFiles(inputPath, "*", SearchOption.TopDirectoryOnly);
        stats.TotalFiles = allFiles.Length;

        // Use the same logic as FindPokemonBundles for consistency
        var pokemonBundles = FindPokemonBundles(inputPath);
        stats.MatchingBundles = pokemonBundles.Count;
        stats.BundleFiles.AddRange(pokemonBundles);

        return stats;
    }
}

/// <summary>
/// Statistics about Pokemon bundle discovery
/// </summary>
public class BundleDiscoveryStats
{
    public int TotalFiles { get; set; } = 0;
    public int MatchingBundles { get; set; } = 0;
    public List<string> BundleFiles { get; set; } = new List<string>();

    public double MatchPercentage => TotalFiles > 0 ? (double)MatchingBundles / TotalFiles * 100 : 0;
}
