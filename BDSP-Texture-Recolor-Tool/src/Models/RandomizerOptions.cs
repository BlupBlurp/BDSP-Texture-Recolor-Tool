namespace BDSP.TextureRecolorTool.Models;

/// <summary>
/// Operation mode selection
/// </summary>
public enum OperationMode
{
    /// <summary>
    /// Process textures in-place (original behavior)
    /// </summary>
    Process,

    /// <summary>
    /// Export textures to PNG files organized by bundle
    /// </summary>
    Export,

    /// <summary>
    /// Import PNG files and reinsert into bundles
    /// </summary>
    Import
}

/// <summary>
/// Randomization mode selection
/// </summary>
public enum RandomizationMode
{
    /// <summary>
    /// Random hue shift (original behavior)
    /// </summary>
    Random,

    /// <summary>
    /// Type-based coloring using Pokemon types
    /// </summary>
    TypeBased
}

/// <summary>
/// Color algorithm selection for type-based mode
/// </summary>
public enum ColorAlgorithm
{
    /// <summary>
    /// Simple HSV hue shifting (current behavior)
    /// </summary>
    HueShift,

    /// <summary>
    /// Advanced color replacement with palette mapping
    /// </summary>
    ColorReplacement
}

/// <summary>
/// Texture compression format selection for reinsertion
/// </summary>
public enum TextureCompressionFormat
{
    /// <summary>
    /// Uncompressed RGBA32 format (default, compatible with all textures)
    /// Fast processing but large file sizes (~5x larger than compressed)
    /// </summary>
    RGBA32,

    /// <summary>
    /// BC7 compression format (high quality, good for most textures)
    /// Slower processing but excellent quality/size ratio for color textures
    /// </summary>
    BC7

    // Future formats can be added here:
    // BC1, BC3, BC4, BC5, BC6H, ETC2, ASTC, etc.
    // Each format should be implemented in TextureCompressionService
}

/// <summary>
/// Configuration options for the BDSP texture randomizer
/// </summary>
public class RandomizerOptions
{
    /// <summary>
    /// Input directory containing Pokemon bundle files
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Output directory for modified bundle files
    /// </summary>
    public string OutputPath { get; set; } = string.Empty;

    /// <summary>
    /// Random seed for reproducible results (optional)
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// Maximum number of bundles to process (for testing, optional)
    /// </summary>
    public int? MaxBundles { get; set; }

    /// <summary>
    /// Enable verbose logging output
    /// </summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// File pattern to match Pokemon bundles (default: pm####_##_##)
    /// </summary>
    public string FilePattern { get; set; } = @"^pm\d{4}_\d{2}_\d{2}$";

    /// <summary>
    /// Operation mode: Process, Export, or Import
    /// </summary>
    public OperationMode Operation { get; set; } = OperationMode.Process;

    /// <summary>
    /// Randomization mode: Random or TypeBased
    /// </summary>
    public RandomizationMode Mode { get; set; } = RandomizationMode.TypeBased;

    /// <summary>
    /// Color algorithm for type-based mode: HueShift or ColorReplacement
    /// </summary>
    public ColorAlgorithm Algorithm { get; set; } = ColorAlgorithm.ColorReplacement;

    /// <summary>
    /// Texture compression format for reinsertion: RGBA32 or BC7
    /// </summary>
    public TextureCompressionFormat CompressionFormat { get; set; } = TextureCompressionFormat.RGBA32;

    /// <summary>
    /// Path to PersonalTable.json file for type-based coloring
    /// </summary>
    public string PokemonDataPath { get; set; } = string.Empty;

    /// <summary>
    /// Path to directory containing PNG textures (for Import operations)
    /// </summary>
    public string TexturesPath { get; set; } = string.Empty;
}