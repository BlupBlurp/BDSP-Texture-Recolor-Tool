using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using BDSP.CSharp.Randomizer.Models;
using Serilog;

namespace BDSP.CSharp.Randomizer.Services;

/// <summary>
/// Service for analyzing texture colors to identify dominant colors, clusters, and preservation areas
/// This enables intelligent color replacement instead of simple hue shifting
/// </summary>
public class ColorAnalysisService
{
    private readonly ILogger _logger;

    public ColorAnalysisService()
    {
        _logger = Log.ForContext<ColorAnalysisService>();
    }

    /// <summary>
    /// Analyze a texture to identify color composition and characteristics
    /// </summary>
    /// <param name="image">Image to analyze</param>
    /// <param name="textureName">Name of texture for context (eye detection, etc.)</param>
    /// <returns>Comprehensive color analysis results</returns>
    public ColorAnalysisResult AnalyzeTexture(Image<Rgba32> image, string textureName = "")
    {
        _logger.Debug("Starting color analysis for texture: {TextureName} ({Width}x{Height})",
            textureName, image.Width, image.Height);

        var result = new ColorAnalysisResult();
        var colorFrequency = new Dictionary<uint, ColorData>();
        int totalPixels = 0;
        int opaquePixels = 0;

        // First pass: Count color frequencies and gather basic statistics
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                totalPixels++;

                // Skip transparent pixels
                if (pixel.A == 0)
                    continue;

                opaquePixels++;

                // Convert to analysis color key (reduced precision for clustering)
                uint colorKey = CreateColorKey(pixel.R, pixel.G, pixel.B);

                if (!colorFrequency.ContainsKey(colorKey))
                {
                    colorFrequency[colorKey] = new ColorData
                    {
                        Color = new ColorInfo(pixel.R, pixel.G, pixel.B),
                        SamplePixels = new List<System.Drawing.Point>()
                    };
                }

                colorFrequency[colorKey].Count++;

                // Store sample pixel locations (limited to avoid memory issues)
                if (colorFrequency[colorKey].SamplePixels.Count < 10)
                {
                    colorFrequency[colorKey].SamplePixels.Add(new System.Drawing.Point(x, y));
                }
            }
        }

        _logger.Debug("Found {UniqueColors} unique colors in {OpaquePixels} opaque pixels",
            colorFrequency.Count, opaquePixels);

        // Calculate frequencies and identify dominant colors
        var dominantColors = new List<DominantColor>();
        float totalLuminance = 0f;
        float totalSaturation = 0f;

        foreach (var kvp in colorFrequency.OrderByDescending(x => x.Value.Count))
        {
            var colorData = kvp.Value;
            float frequency = (float)colorData.Count / opaquePixels;

            // Only consider colors that appear in at least 0.5% of pixels
            if (frequency < 0.005f && dominantColors.Count >= 10)
                break;

            var dominantColor = new DominantColor
            {
                Color = colorData.Color,
                Frequency = frequency,
                Role = DetermineColorRole(colorData.Color, frequency),
                AverageLuminance = colorData.Color.Value,
                SamplePixels = colorData.SamplePixels
            };

            dominantColors.Add(dominantColor);
            totalLuminance += dominantColor.Color.Value * frequency;
            totalSaturation += dominantColor.Color.Saturation * frequency;
        }

        result.DominantColors = dominantColors;

        // Create color clusters by grouping similar colors
        result.ColorClusters = CreateColorClusters(dominantColors);

        // Calculate color statistics
        result.Statistics = new ColorStatistics
        {
            AverageLuminance = totalLuminance,
            SaturationLevel = totalSaturation,
            UniqueColorCount = colorFrequency.Count,
            ColorComplexity = (float)colorFrequency.Count / opaquePixels,
            HasHighContrast = CalculateContrast(dominantColors) > 0.6f,
            HasGradients = DetectGradients(image)
        };

        // Identify preservation areas (eyes, whites, important details)
        result.PreservationAreas = IdentifyPreservationAreas(image, textureName, dominantColors);

        _logger.Debug("Analysis complete - {DominantColorCount} dominant colors, {ClusterCount} clusters, {PreservationCount} preservation areas",
            result.DominantColors.Count, result.ColorClusters.Count, result.PreservationAreas.Count);

        return result;
    }

    /// <summary>
    /// Create a color key for clustering similar colors together
    /// Reduces precision to group similar shades
    /// </summary>
    private static uint CreateColorKey(byte r, byte g, byte b)
    {
        // Reduce precision by rounding to nearest 8 to cluster similar colors
        const int precision = 8;
        r = (byte)((r / precision) * precision);
        g = (byte)((g / precision) * precision);
        b = (byte)((b / precision) * precision);

        return ((uint)r << 16) | ((uint)g << 8) | b;
    }

    /// <summary>
    /// Determine the role of a color based on its characteristics and frequency
    /// </summary>
    private static ColorRole DetermineColorRole(ColorInfo color, float frequency)
    {
        // Very bright colors are likely highlights
        if (color.Value > 0.9f && color.Saturation < 0.3f)
            return ColorRole.Highlight;

        // Very dark colors are likely shadows
        if (color.Value < 0.2f)
            return ColorRole.Shadow;

        // High frequency colors are primary or secondary
        if (frequency > 0.15f) // More than 15% of pixels
            return ColorRole.Primary;

        if (frequency > 0.05f) // More than 5% of pixels
            return ColorRole.Secondary;

        // High saturation, low frequency colors are likely accents
        if (color.Saturation > 0.7f)
            return ColorRole.Accent;

        // Low saturation colors are likely neutral
        if (color.Saturation < 0.3f)
            return ColorRole.Neutral;

        return ColorRole.Detail;
    }

    /// <summary>
    /// Group similar dominant colors into clusters for more effective replacement
    /// </summary>
    private List<ColorCluster> CreateColorClusters(List<DominantColor> dominantColors)
    {
        var clusters = new List<ColorCluster>();
        var processed = new HashSet<int>();

        for (int i = 0; i < dominantColors.Count; i++)
        {
            if (processed.Contains(i))
                continue;

            var cluster = new ColorCluster
            {
                RepresentativeColor = dominantColors[i].Color,
                Colors = new List<ColorInfo> { dominantColors[i].Color },
                TotalFrequency = dominantColors[i].Frequency,
                Role = dominantColors[i].Role
            };

            processed.Add(i);

            // Find similar colors to add to this cluster
            for (int j = i + 1; j < dominantColors.Count; j++)
            {
                if (processed.Contains(j))
                    continue;

                // Calculate color distance in LAB space for perceptual accuracy
                float distance = CalculateLabDistance(dominantColors[i].Color, dominantColors[j].Color);

                // Colors are similar if they're within a threshold distance
                if (distance < 20.0f) // LAB distance threshold
                {
                    cluster.Colors.Add(dominantColors[j].Color);
                    cluster.TotalFrequency += dominantColors[j].Frequency;
                    processed.Add(j);
                }
            }

            // Calculate luminance variance within cluster
            if (cluster.Colors.Count > 1)
            {
                float avgLuminance = cluster.Colors.Average(c => c.Value);
                cluster.LuminanceVariance = cluster.Colors.Average(c => Math.Abs(c.Value - avgLuminance));
            }

            clusters.Add(cluster);
        }

        return clusters.OrderByDescending(c => c.TotalFrequency).ToList();
    }

    /// <summary>
    /// Calculate contrast level in the image based on dominant colors
    /// </summary>
    private static float CalculateContrast(List<DominantColor> dominantColors)
    {
        if (dominantColors.Count < 2)
            return 0f;

        float maxLuminance = dominantColors.Max(c => c.Color.Value);
        float minLuminance = dominantColors.Min(c => c.Color.Value);

        return maxLuminance - minLuminance;
    }

    /// <summary>
    /// Detect if the image has gradients (smooth color transitions)
    /// </summary>
    private static bool DetectGradients(Image<Rgba32> image)
    {
        int gradientPixels = 0;
        int totalChecked = 0;

        // Sample every 4th pixel to avoid performance issues
        for (int y = 0; y < image.Height; y += 4)
        {
            for (int x = 0; x < image.Width; x += 4)
            {
                if (x + 1 < image.Width && y + 1 < image.Height)
                {
                    var pixel1 = image[x, y];
                    var pixel2 = image[x + 1, y];
                    var pixel3 = image[x, y + 1];

                    totalChecked++;

                    // Check if adjacent pixels have gradual color changes
                    if (IsGradualColorChange(pixel1, pixel2) || IsGradualColorChange(pixel1, pixel3))
                    {
                        gradientPixels++;
                    }
                }
            }
        }

        return totalChecked > 0 && (float)gradientPixels / totalChecked > 0.3f;
    }

    /// <summary>
    /// Check if two adjacent pixels represent a gradual color change (gradient)
    /// </summary>
    private static bool IsGradualColorChange(Rgba32 pixel1, Rgba32 pixel2)
    {
        // Skip transparent pixels
        if (pixel1.A == 0 || pixel2.A == 0)
            return false;

        // Calculate RGB differences
        int rDiff = Math.Abs(pixel1.R - pixel2.R);
        int gDiff = Math.Abs(pixel1.G - pixel2.G);
        int bDiff = Math.Abs(pixel1.B - pixel2.B);

        // Gradual change if differences are small but not identical
        int maxDiff = Math.Max(rDiff, Math.Max(gDiff, bDiff));
        return maxDiff > 2 && maxDiff < 20;
    }

    /// <summary>
    /// Identify areas that should be preserved during color replacement
    /// </summary>
    private List<PreservationArea> IdentifyPreservationAreas(Image<Rgba32> image, string textureName, List<DominantColor> dominantColors)
    {
        var preservationAreas = new List<PreservationArea>();
        bool isEyeTexture = IsEyeTexture(textureName);

        // Eye preservation areas
        if (isEyeTexture)
        {
            var eyeAreas = IdentifyEyePreservationAreas(image);
            preservationAreas.AddRange(eyeAreas);
        }

        // White/light preservation areas (teeth, claws, etc.)
        var whiteAreas = IdentifyWhitePreservationAreas(image);
        preservationAreas.AddRange(whiteAreas);

        // Neutral color preservation (structure colors)
        var neutralAreas = IdentifyNeutralPreservationAreas(dominantColors);
        preservationAreas.AddRange(neutralAreas);

        return preservationAreas;
    }

    /// <summary>
    /// Identify eye-specific preservation areas (whites and adjacent greys)
    /// </summary>
    private List<PreservationArea> IdentifyEyePreservationAreas(Image<Rgba32> image)
    {
        var areas = new List<PreservationArea>();
        var whitePixels = new List<System.Drawing.Point>();
        var greyPixels = new List<System.Drawing.Point>();

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];

                if (pixel.A == 0) continue;

                if (IsWhiteColor(pixel.R, pixel.G, pixel.B))
                {
                    whitePixels.Add(new System.Drawing.Point(x, y));
                }
                else if (IsGreyShadowColor(pixel.R, pixel.G, pixel.B))
                {
                    greyPixels.Add(new System.Drawing.Point(x, y));
                }
            }
        }

        if (whitePixels.Count > 0)
        {
            areas.Add(new PreservationArea
            {
                Type = PreservationType.Eye,
                Pixels = whitePixels,
                RepresentativeColor = new ColorInfo(240, 240, 240),
                Reason = "Eye white areas"
            });
        }

        if (greyPixels.Count > 0)
        {
            areas.Add(new PreservationArea
            {
                Type = PreservationType.Eye,
                Pixels = greyPixels,
                RepresentativeColor = new ColorInfo(180, 180, 180),
                Reason = "Eye shadow areas"
            });
        }

        return areas;
    }

    /// <summary>
    /// Identify white/light areas that should be preserved (teeth, claws, etc.)
    /// </summary>
    private List<PreservationArea> IdentifyWhitePreservationAreas(Image<Rgba32> image)
    {
        var whitePixels = new List<System.Drawing.Point>();

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];

                if (pixel.A == 0) continue;

                if (IsWhiteColor(pixel.R, pixel.G, pixel.B))
                {
                    whitePixels.Add(new System.Drawing.Point(x, y));
                }
            }
        }

        if (whitePixels.Count > 10) // Only preserve if significant number of white pixels
        {
            return new List<PreservationArea>
            {
                new PreservationArea
                {
                    Type = PreservationType.Teeth,
                    Pixels = whitePixels,
                    RepresentativeColor = new ColorInfo(240, 240, 240),
                    Reason = "White structural elements"
                }
            };
        }

        return new List<PreservationArea>();
    }

    /// <summary>
    /// Identify neutral colors that provide important structural information
    /// </summary>
    private List<PreservationArea> IdentifyNeutralPreservationAreas(List<DominantColor> dominantColors)
    {
        var neutralAreas = new List<PreservationArea>();

        foreach (var color in dominantColors.Where(c => c.Role == ColorRole.Neutral && c.Frequency > 0.02f))
        {
            neutralAreas.Add(new PreservationArea
            {
                Type = PreservationType.Neutral,
                Pixels = color.SamplePixels,
                RepresentativeColor = color.Color,
                Reason = "Structural neutral color"
            });
        }

        return neutralAreas;
    }

    /// <summary>
    /// Calculate perceptual color distance in LAB color space
    /// </summary>
    private static float CalculateLabDistance(ColorInfo color1, ColorInfo color2)
    {
        float deltaL = color1.L - color2.L;
        float deltaA = color1.A - color2.A;
        float deltaB = color1.B_Lab - color2.B_Lab;

        return MathF.Sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB);
    }

    /// <summary>
    /// Check if a texture is an eye/iris texture based on its name
    /// </summary>
    private static bool IsEyeTexture(string textureName)
    {
        if (string.IsNullOrEmpty(textureName))
            return false;

        var lowerName = textureName.ToLowerInvariant();
        return lowerName.Contains("eye") || lowerName.Contains("iris");
    }

    /// <summary>
    /// Check if a color is considered "white" that should be preserved
    /// </summary>
    private static bool IsWhiteColor(byte r, byte g, byte b)
    {
        const int targetWhite = 225; // Slightly lower threshold for analysis
        return r >= targetWhite && g >= targetWhite && b >= targetWhite;
    }

    /// <summary>
    /// Check if a color is considered a "grey shadow" that should be preserved in eye textures
    /// </summary>
    private static bool IsGreyShadowColor(byte r, byte g, byte b)
    {
        const int minGrey = 150;
        const int maxGrey = 240;
        const int tolerance = 20;

        bool inRange = r >= minGrey && r <= maxGrey &&
                       g >= minGrey && g <= maxGrey &&
                       b >= minGrey && b <= maxGrey;

        if (!inRange) return false;

        int maxComponent = Math.Max(r, Math.Max(g, b));
        int minComponent = Math.Min(r, Math.Min(g, b));

        return (maxComponent - minComponent) <= tolerance;
    }

    /// <summary>
    /// Internal class for tracking color data during analysis
    /// </summary>
    private class ColorData
    {
        public ColorInfo Color { get; set; } = new();
        public int Count { get; set; }
        public List<System.Drawing.Point> SamplePixels { get; set; } = new();
    }
}