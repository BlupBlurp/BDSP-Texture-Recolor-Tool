using SixLabors.ImageSharp.PixelFormats;

namespace BDSP.CSharp.Randomizer.Models;

/// <summary>
/// Comprehensive color palette for a Pokemon type with multiple color categories
/// </summary>
public class TypeColorPalette
{
    public PokemonType Type { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Primary color - the most dominant color that represents this type
    /// </summary>
    public ColorInfo Primary { get; set; } = new();

    /// <summary>
    /// Secondary color - supporting color that complements the primary
    /// </summary>
    public ColorInfo Secondary { get; set; } = new();

    /// <summary>
    /// Accent color - bright highlight color for details and energy
    /// </summary>
    public ColorInfo Accent { get; set; } = new();

    /// <summary>
    /// Dark color - for shadows, deep areas, and contrast
    /// </summary>
    public ColorInfo Dark { get; set; } = new();

    /// <summary>
    /// Light color - for highlights, bright areas, and shine
    /// </summary>
    public ColorInfo Light { get; set; } = new();

    /// <summary>
    /// Neutral color - for balanced areas and transitions
    /// </summary>
    public ColorInfo Neutral { get; set; } = new();
}

/// <summary>
/// Extended color information with multiple color space representations
/// </summary>
public class ColorInfo
{
    // RGB values (0-255)
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    // HSV values (0.0-1.0)
    public float Hue { get; set; }
    public float Saturation { get; set; }
    public float Value { get; set; }

    // LAB values for perceptual color matching
    public float L { get; set; } // Lightness (0-100)
    public float A { get; set; } // Green-Red axis (-128 to 127)
    public float B_Lab { get; set; } // Blue-Yellow axis (-128 to 127)

    // Convenience properties
    public Rgba32 AsRgba32 => new(R, G, B, 255);
    public string HexString => $"#{R:X2}{G:X2}{B:X2}";

    public ColorInfo() { }

    public ColorInfo(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
        UpdateDerivedValues();
    }

    /// <summary>
    /// Update HSV and LAB values when RGB changes
    /// </summary>
    public void UpdateDerivedValues()
    {
        // Convert RGB to HSV
        RgbToHsv(R, G, B, out float h, out float s, out float v);
        Hue = h;
        Saturation = s;
        Value = v;

        // Convert RGB to LAB (simplified conversion)
        RgbToLab(R, G, B, out float l, out float a, out float b);
        L = l;
        A = a;
        B_Lab = b;
    }

    private static void RgbToHsv(byte r, byte g, byte b, out float h, out float s, out float v)
    {
        float rf = r / 255.0f;
        float gf = g / 255.0f;
        float bf = b / 255.0f;

        float max = Math.Max(rf, Math.Max(gf, bf));
        float min = Math.Min(rf, Math.Min(gf, bf));
        float delta = max - min;

        // Value
        v = max;

        // Saturation
        s = max == 0 ? 0 : delta / max;

        // Hue
        if (delta == 0)
        {
            h = 0;
        }
        else if (max == rf)
        {
            h = ((gf - bf) / delta) / 6.0f;
        }
        else if (max == gf)
        {
            h = (2.0f + (bf - rf) / delta) / 6.0f;
        }
        else // max == bf
        {
            h = (4.0f + (rf - gf) / delta) / 6.0f;
        }

        if (h < 0) h += 1.0f;
    }

    private static void RgbToLab(byte r, byte g, byte b, out float l, out float a, out float lab_b)
    {
        // Simplified RGB to LAB conversion
        // Convert RGB to XYZ first (using sRGB color space)
        float rf = r / 255.0f;
        float gf = g / 255.0f;
        float bf = b / 255.0f;

        // Apply gamma correction
        rf = rf > 0.04045f ? MathF.Pow((rf + 0.055f) / 1.055f, 2.4f) : rf / 12.92f;
        gf = gf > 0.04045f ? MathF.Pow((gf + 0.055f) / 1.055f, 2.4f) : gf / 12.92f;
        bf = bf > 0.04045f ? MathF.Pow((bf + 0.055f) / 1.055f, 2.4f) : bf / 12.92f;

        // Convert to XYZ (Observer = 2°, Illuminant = D65)
        float x = (rf * 0.4124f + gf * 0.3576f + bf * 0.1805f);
        float y = (rf * 0.2126f + gf * 0.7152f + bf * 0.0722f);
        float z = (rf * 0.0193f + gf * 0.1192f + bf * 0.9505f);

        // Normalize for D65 illuminant
        x = x / 0.95047f;
        y = y / 1.00000f;
        z = z / 1.08883f;

        // Convert XYZ to LAB
        x = x > 0.008856f ? MathF.Pow(x, 1.0f / 3.0f) : (7.787f * x + 16.0f / 116.0f);
        y = y > 0.008856f ? MathF.Pow(y, 1.0f / 3.0f) : (7.787f * y + 16.0f / 116.0f);
        z = z > 0.008856f ? MathF.Pow(z, 1.0f / 3.0f) : (7.787f * z + 16.0f / 116.0f);

        l = (116.0f * y) - 16.0f;
        a = 500.0f * (x - y);
        lab_b = 200.0f * (y - z);
    }
}

/// <summary>
/// Results from analyzing the color composition of a texture
/// </summary>
public class ColorAnalysisResult
{
    /// <summary>
    /// The most prominent colors in the texture, sorted by frequency
    /// </summary>
    public List<DominantColor> DominantColors { get; set; } = new();

    /// <summary>
    /// Color clusters representing similar colors grouped together
    /// </summary>
    public List<ColorCluster> ColorClusters { get; set; } = new();

    /// <summary>
    /// Overall color statistics for the texture
    /// </summary>
    public ColorStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Areas of the texture to preserve (detected whites, neutrals, etc.)
    /// </summary>
    public List<PreservationArea> PreservationAreas { get; set; } = new();
}

/// <summary>
/// A dominant color found in the texture with its frequency and characteristics
/// </summary>
public class DominantColor
{
    public ColorInfo Color { get; set; } = new();
    public float Frequency { get; set; } // Percentage of pixels (0.0-1.0)
    public ColorRole Role { get; set; } = ColorRole.Primary;
    public float AverageLuminance { get; set; } // Average luminance of pixels with this color
    public List<System.Drawing.Point> SamplePixels { get; set; } = new(); // Sample pixel locations
}

/// <summary>
/// A cluster of similar colors that should be treated as a group
/// </summary>
public class ColorCluster
{
    public ColorInfo RepresentativeColor { get; set; } = new();
    public List<ColorInfo> Colors { get; set; } = new();
    public float TotalFrequency { get; set; }
    public ColorRole Role { get; set; } = ColorRole.Primary;
    public float LuminanceVariance { get; set; } // How much luminance varies within cluster
}

/// <summary>
/// Statistical information about the texture's color composition
/// </summary>
public class ColorStatistics
{
    public float AverageLuminance { get; set; }
    public float LuminanceVariance { get; set; }
    public float ColorComplexity { get; set; } // Number of unique colors / total pixels
    public float SaturationLevel { get; set; } // Average saturation
    public bool HasHighContrast { get; set; }
    public bool HasGradients { get; set; }
    public int UniqueColorCount { get; set; }
}

/// <summary>
/// Area of texture to preserve during color replacement
/// </summary>
public class PreservationArea
{
    public PreservationType Type { get; set; }
    public List<System.Drawing.Point> Pixels { get; set; } = new();
    public ColorInfo RepresentativeColor { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Role that a color plays in the texture
/// </summary>
public enum ColorRole
{
    Primary,    // Main color of the Pokemon
    Secondary,  // Supporting color
    Accent,     // Small highlight areas
    Shadow,     // Dark/shadow areas
    Highlight,  // Bright/light areas
    Neutral,    // Neutral/transitional colors
    Detail      // Fine details that should be preserved
}

/// <summary>
/// Type of area to preserve during color replacement
/// </summary>
public enum PreservationType
{
    Eye,        // Eye/iris areas (whites and adjacent greys)
    Teeth,      // Teeth and claws (whites)
    Shine,      // Metallic shine and highlights
    Shadow,     // Important shadow details
    Neutral,    // Neutral colors that provide structure
    Detail      // Fine details that would be lost
}

/// <summary>
/// Parameters for color replacement algorithm
/// </summary>
public class ColorReplacementParameters
{
    /// <summary>
    /// How aggressively to replace colors (0.0 = no change, 1.0 = complete replacement)
    /// </summary>
    public float ReplacementStrength { get; set; } = 0.8f;

    /// <summary>
    /// How much to preserve original luminance relationships (0.0 = ignore, 1.0 = preserve completely)
    /// </summary>
    public float LuminancePreservation { get; set; } = 0.7f;

    /// <summary>
    /// How much to preserve saturation relationships (0.0 = ignore, 1.0 = preserve completely)
    /// </summary>
    public float SaturationPreservation { get; set; } = 0.5f;

    /// <summary>
    /// Minimum color distance for replacement (colors too similar to target won't be changed)
    /// </summary>
    public float MinimumColorDistance { get; set; } = 0.1f;

    /// <summary>
    /// Whether to preserve gradients and smooth transitions
    /// </summary>
    public bool PreserveGradients { get; set; } = true;

    /// <summary>
    /// Color space to use for color distance calculations
    /// </summary>
    public ColorSpace ColorSpace { get; set; } = ColorSpace.LAB;
}

/// <summary>
/// Color space options for color distance calculations
/// </summary>
public enum ColorSpace
{
    RGB,    // Simple RGB distance (fast but less perceptual)
    HSV,    // HSV distance (good for hue-based operations)
    LAB     // CIELAB distance (perceptually accurate but slower)
}