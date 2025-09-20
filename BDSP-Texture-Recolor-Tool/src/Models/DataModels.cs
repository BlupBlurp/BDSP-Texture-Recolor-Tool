namespace BDSP.TextureRecolorTool.Models;

/// <summary>
/// Color transformation parameters that remain consistent across all textures in a bundle
/// This ensures all Pokemon textures maintain a coherent appearance
/// </summary>
public class BundleColorParameters
{
    /// <summary>
    /// Hue shift value (-1.0 to 1.0, representing full color wheel rotation for random mode)
    /// OR target hue (0.0 to 1.0) for type-based mode
    /// </summary>
    public float HueShift { get; set; }

    /// <summary>
    /// Saturation variation multiplier (0.8 to 1.2 for natural results in random mode)
    /// OR target saturation (0.0 to 1.0) for type-based mode
    /// </summary>
    public float SaturationVariation { get; set; }

    /// <summary>
    /// Target value/brightness for type-based mode (0.0 to 1.0)
    /// Not used in random mode
    /// </summary>
    public float? TargetValue { get; set; }

    /// <summary>
    /// Indicates whether this is type-based coloring (true) or random coloring (false)
    /// </summary>
    public bool IsTypeBased { get; set; }

    /// <summary>
    /// Pokemon type number (0-17) when using type-based coloring
    /// </summary>
    public int? PokemonType { get; set; }

    /// <summary>
    /// Color algorithm to use (HueShift or ColorReplacement)
    /// </summary>
    public ColorAlgorithm Algorithm { get; set; } = ColorAlgorithm.HueShift;

    /// <summary>
    /// Color replacement parameters for advanced algorithm
    /// </summary>
    public ColorReplacementParameters? ReplacementParameters { get; set; }
}

/// <summary>
/// Processing statistics for bundle operations
/// </summary>
public class ProcessingStatistics
{
    public int BundlesProcessed { get; set; }
    public int BundlesModified { get; set; }
    public int BundlesSkipped { get; set; }
    public int TexturesModified { get; set; }
    public int ErrorsEncountered { get; set; }
    public TimeSpan ProcessingTime { get; set; }

    public double SuccessRate => BundlesProcessed > 0 ? (double)BundlesModified / BundlesProcessed * 100.0 : 0.0;
    public double AverageTexturesPerBundle => BundlesModified > 0 ? (double)TexturesModified / BundlesModified : 0.0;
    public double ProcessingRate => ProcessingTime.TotalSeconds > 0 ? BundlesProcessed / ProcessingTime.TotalSeconds : 0.0;
}