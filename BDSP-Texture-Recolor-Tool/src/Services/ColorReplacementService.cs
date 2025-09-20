using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using BDSP.TextureRecolorTool.Models;
using Serilog;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for intelligently replacing colors in textures based on color analysis and type palettes
/// This replaces the simple HSV hue shifting with sophisticated color mapping
/// </summary>
public class ColorReplacementService
{
    private readonly ILogger _logger;

    public ColorReplacementService()
    {
        _logger = Log.ForContext<ColorReplacementService>();
    }

    /// <summary>
    /// Replace colors in an image using intelligent mapping to type palette
    /// </summary>
    /// <param name="image">Source image to modify</param>
    /// <param name="analysis">Color analysis results from ColorAnalysisService</param>
    /// <param name="palette">Target type color palette</param>
    /// <param name="parameters">Replacement parameters for fine-tuning</param>
    /// <returns>Modified image with replaced colors</returns>
    public Image<Rgba32> ReplaceColors(Image<Rgba32> image, ColorAnalysisResult analysis,
        TypeColorPalette palette, ColorReplacementParameters? parameters = null)
    {
        parameters ??= new ColorReplacementParameters();

        _logger.Debug("Starting color replacement for {Type} type with {DominantColors} dominant colors",
            palette.Type, analysis.DominantColors.Count);

        var result = image.Clone();

        // Create color mapping from analysis to palette
        var colorMappings = CreateColorMappings(analysis, palette, parameters);

        _logger.Debug("Created {MappingCount} color mappings", colorMappings.Count);

        // Create preservation mask for areas to skip
        var preservationMask = CreatePreservationMask(image, analysis.PreservationAreas);

        int pixelsReplaced = 0;
        int pixelsPreserved = 0;
        int pixelsSkipped = 0;

        // Apply color replacements pixel by pixel
        for (int y = 0; y < result.Height; y++)
        {
            for (int x = 0; x < result.Width; x++)
            {
                var pixel = result[x, y];

                // Skip transparent pixels
                if (pixel.A == 0)
                {
                    pixelsSkipped++;
                    continue;
                }

                // Check if this pixel should be preserved
                if (preservationMask[x, y])
                {
                    pixelsPreserved++;
                    continue;
                }

                // Find the best color mapping for this pixel
                var bestMapping = FindBestColorMapping(pixel, colorMappings, parameters);

                if (bestMapping != null)
                {
                    var newColor = ApplyColorMapping(pixel, bestMapping, parameters);
                    result[x, y] = new Rgba32(newColor.R, newColor.G, newColor.B, pixel.A);
                    pixelsReplaced++;
                }
                else
                {
                    pixelsSkipped++;
                }
            }
        }

        _logger.Debug("Color replacement complete - Replaced: {Replaced}, Preserved: {Preserved}, Skipped: {Skipped}",
            pixelsReplaced, pixelsPreserved, pixelsSkipped);

        return result;
    }

    /// <summary>
    /// Create mappings from original color clusters to target palette colors
    /// </summary>
    private List<ColorMapping> CreateColorMappings(ColorAnalysisResult analysis, TypeColorPalette palette,
        ColorReplacementParameters parameters)
    {
        var mappings = new List<ColorMapping>();

        // Sort clusters by importance (frequency and role)
        var sortedClusters = analysis.ColorClusters
            .OrderByDescending(c => GetClusterImportance(c))
            .ToList();

        // Map each cluster to the most appropriate palette color
        var usedPaletteColors = new HashSet<ColorInfo>();

        foreach (var cluster in sortedClusters)
        {
            var targetColor = SelectBestPaletteColor(cluster, palette, usedPaletteColors);

            if (targetColor != null)
            {
                var mapping = new ColorMapping
                {
                    SourceCluster = cluster,
                    TargetColor = targetColor,
                    ReplacementStrength = CalculateReplacementStrength(cluster, parameters),
                    PreserveLuminance = ShouldPreserveLuminance(cluster),
                    PreserveSaturation = ShouldPreserveSaturation(cluster)
                };

                mappings.Add(mapping);
                usedPaletteColors.Add(targetColor);

                _logger.Debug("Mapped {Role} cluster ({Frequency:P1}) to {TargetColor} with strength {Strength:P1}",
                    cluster.Role, cluster.TotalFrequency, targetColor.HexString, mapping.ReplacementStrength);
            }
        }

        return mappings;
    }

    /// <summary>
    /// Calculate importance score for color cluster mapping priority
    /// </summary>
    private static float GetClusterImportance(ColorCluster cluster)
    {
        float baseScore = cluster.TotalFrequency;

        // Role-based importance multipliers
        float roleMultiplier = cluster.Role switch
        {
            ColorRole.Primary => 1.0f,
            ColorRole.Secondary => 0.8f,
            ColorRole.Accent => 0.6f,
            ColorRole.Neutral => 0.4f,
            ColorRole.Shadow => 0.3f,
            ColorRole.Highlight => 0.3f,
            ColorRole.Detail => 0.2f,
            _ => 0.5f
        };

        return baseScore * roleMultiplier;
    }

    /// <summary>
    /// Select the best palette color for a given cluster
    /// </summary>
    private ColorInfo? SelectBestPaletteColor(ColorCluster cluster, TypeColorPalette palette,
        HashSet<ColorInfo> usedColors)
    {
        var availableColors = new List<(ColorInfo color, float score)>();

        // Score each palette color based on cluster characteristics
        ScorePaletteColor(palette.Primary, cluster, availableColors, usedColors, 1.0f);
        ScorePaletteColor(palette.Secondary, cluster, availableColors, usedColors, 0.9f);
        ScorePaletteColor(palette.Accent, cluster, availableColors, usedColors, 0.8f);
        ScorePaletteColor(palette.Dark, cluster, availableColors, usedColors, 0.7f);
        ScorePaletteColor(palette.Light, cluster, availableColors, usedColors, 0.7f);
        ScorePaletteColor(palette.Neutral, cluster, availableColors, usedColors, 0.6f);

        // Return the highest scoring color
        return availableColors.OrderByDescending(x => x.score).FirstOrDefault().color;
    }

    /// <summary>
    /// Score a palette color's suitability for a cluster
    /// </summary>
    private void ScorePaletteColor(ColorInfo paletteColor, ColorCluster cluster,
        List<(ColorInfo color, float score)> availableColors, HashSet<ColorInfo> usedColors, float baseScore)
    {
        if (usedColors.Contains(paletteColor))
        {
            baseScore *= 0.3f; // Heavily penalize reusing colors
        }

        // Role-based scoring
        float roleScore = GetRoleCompatibilityScore(cluster.Role, paletteColor);

        // Luminance similarity bonus
        float luminanceScore = 1.0f - Math.Abs(cluster.RepresentativeColor.Value - paletteColor.Value);

        // Saturation similarity bonus for non-neutral roles
        float saturationScore = cluster.Role == ColorRole.Neutral ? 1.0f :
            1.0f - Math.Abs(cluster.RepresentativeColor.Saturation - paletteColor.Saturation) * 0.5f;

        float totalScore = baseScore * roleScore * luminanceScore * saturationScore;
        availableColors.Add((paletteColor, totalScore));
    }

    /// <summary>
    /// Get compatibility score between cluster role and palette color characteristics
    /// </summary>
    private static float GetRoleCompatibilityScore(ColorRole role, ColorInfo paletteColor)
    {
        return role switch
        {
            ColorRole.Primary => 1.0f, // Primary colors can map to any palette color
            ColorRole.Secondary => 0.9f,
            ColorRole.Shadow when paletteColor.Value < 0.4f => 1.2f, // Prefer dark colors for shadows
            ColorRole.Highlight when paletteColor.Value > 0.7f => 1.2f, // Prefer light colors for highlights
            ColorRole.Accent when paletteColor.Saturation > 0.6f => 1.1f, // Prefer saturated colors for accents
            ColorRole.Neutral when paletteColor.Saturation < 0.4f => 1.1f, // Prefer desaturated colors for neutrals
            _ => 0.8f
        };
    }

    /// <summary>
    /// Calculate replacement strength based on cluster and parameters
    /// </summary>
    private static float CalculateReplacementStrength(ColorCluster cluster, ColorReplacementParameters parameters)
    {
        float baseStrength = parameters.ReplacementStrength;

        // Adjust based on cluster role
        float roleMultiplier = cluster.Role switch
        {
            ColorRole.Primary => 1.0f,
            ColorRole.Secondary => 0.9f,
            ColorRole.Accent => 1.1f, // Slightly stronger for accents to make them pop
            ColorRole.Shadow => 0.8f, // Gentler on shadows to preserve depth
            ColorRole.Highlight => 0.8f, // Gentler on highlights to preserve shine
            ColorRole.Neutral => 0.6f, // Much gentler on neutrals to preserve structure
            ColorRole.Detail => 0.7f,
            _ => 0.8f
        };

        return Math.Clamp(baseStrength * roleMultiplier, 0.0f, 1.0f);
    }

    /// <summary>
    /// Determine if luminance should be preserved for this cluster
    /// </summary>
    private static bool ShouldPreserveLuminance(ColorCluster cluster)
    {
        // Always preserve luminance for shadows and highlights to maintain depth
        return cluster.Role is ColorRole.Shadow or ColorRole.Highlight or ColorRole.Detail;
    }

    /// <summary>
    /// Determine if saturation should be preserved for this cluster
    /// </summary>
    private static bool ShouldPreserveSaturation(ColorCluster cluster)
    {
        // Preserve saturation for neutral colors to maintain their neutral character
        return cluster.Role is ColorRole.Neutral or ColorRole.Detail;
    }

    /// <summary>
    /// Create a preservation mask indicating which pixels should not be changed
    /// </summary>
    private bool[,] CreatePreservationMask(Image<Rgba32> image, List<PreservationArea> preservationAreas)
    {
        var mask = new bool[image.Width, image.Height];

        foreach (var area in preservationAreas)
        {
            foreach (var pixel in area.Pixels)
            {
                if (pixel.X >= 0 && pixel.X < image.Width && pixel.Y >= 0 && pixel.Y < image.Height)
                {
                    mask[pixel.X, pixel.Y] = true;
                }
            }
        }

        return mask;
    }

    /// <summary>
    /// Find the best color mapping for a specific pixel
    /// </summary>
    private ColorMapping? FindBestColorMapping(Rgba32 pixel, List<ColorMapping> mappings,
        ColorReplacementParameters parameters)
    {
        var pixelColor = new ColorInfo(pixel.R, pixel.G, pixel.B);
        ColorMapping? bestMapping = null;
        float bestDistance = float.MaxValue;

        foreach (var mapping in mappings)
        {
            // Check if this pixel belongs to this mapping's cluster
            foreach (var clusterColor in mapping.SourceCluster.Colors)
            {
                float distance = CalculateColorDistance(pixelColor, clusterColor, parameters.ColorSpace);

                if (distance < bestDistance && distance <= parameters.MinimumColorDistance * 255)
                {
                    bestDistance = distance;
                    bestMapping = mapping;
                }
            }
        }

        return bestMapping;
    }

    /// <summary>
    /// Apply a color mapping to a specific pixel
    /// </summary>
    private ColorInfo ApplyColorMapping(Rgba32 sourcePixel, ColorMapping mapping,
        ColorReplacementParameters parameters)
    {
        var sourceColor = new ColorInfo(sourcePixel.R, sourcePixel.G, sourcePixel.B);
        var targetColor = mapping.TargetColor;
        float strength = mapping.ReplacementStrength;

        // Start with target color
        float newH = targetColor.Hue;
        float newS = targetColor.Saturation;
        float newV = targetColor.Value;

        // Preserve luminance if requested
        if (mapping.PreserveLuminance || parameters.LuminancePreservation > 0)
        {
            float preservationAmount = mapping.PreserveLuminance ? 1.0f : parameters.LuminancePreservation;
            newV = targetColor.Value * (1 - preservationAmount) + sourceColor.Value * preservationAmount;
        }

        // Preserve saturation if requested
        if (mapping.PreserveSaturation || parameters.SaturationPreservation > 0)
        {
            float preservationAmount = mapping.PreserveSaturation ? 1.0f : parameters.SaturationPreservation;
            newS = targetColor.Saturation * (1 - preservationAmount) + sourceColor.Saturation * preservationAmount;
        }

        // Apply replacement strength (blend between original and target)
        newH = sourceColor.Hue * (1 - strength) + newH * strength;
        newS = Math.Clamp(sourceColor.Saturation * (1 - strength) + newS * strength, 0.0f, 1.0f);
        newV = Math.Clamp(sourceColor.Value * (1 - strength) + newV * strength, 0.0f, 1.0f);

        // Convert back to RGB
        HsvToRgb(newH, newS, newV, out byte r, out byte g, out byte b);
        return new ColorInfo(r, g, b);
    }

    /// <summary>
    /// Calculate distance between two colors in specified color space
    /// </summary>
    private static float CalculateColorDistance(ColorInfo color1, ColorInfo color2, ColorSpace colorSpace)
    {
        return colorSpace switch
        {
            ColorSpace.RGB => CalculateRgbDistance(color1, color2),
            ColorSpace.HSV => CalculateHsvDistance(color1, color2),
            ColorSpace.LAB => CalculateLabDistance(color1, color2),
            _ => CalculateRgbDistance(color1, color2)
        };
    }

    private static float CalculateRgbDistance(ColorInfo color1, ColorInfo color2)
    {
        float dr = color1.R - color2.R;
        float dg = color1.G - color2.G;
        float db = color1.B - color2.B;
        return MathF.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static float CalculateHsvDistance(ColorInfo color1, ColorInfo color2)
    {
        float dh = Math.Min(Math.Abs(color1.Hue - color2.Hue), 1.0f - Math.Abs(color1.Hue - color2.Hue));
        float ds = color1.Saturation - color2.Saturation;
        float dv = color1.Value - color2.Value;
        return MathF.Sqrt(dh * dh + ds * ds + dv * dv);
    }

    private static float CalculateLabDistance(ColorInfo color1, ColorInfo color2)
    {
        float dl = color1.L - color2.L;
        float da = color1.A - color2.A;
        float db = color1.B_Lab - color2.B_Lab;
        return MathF.Sqrt(dl * dl + da * da + db * db);
    }

    /// <summary>
    /// Convert HSV to RGB
    /// </summary>
    private static void HsvToRgb(float h, float s, float v, out byte r, out byte g, out byte b)
    {
        float c = v * s;
        float x = c * (1 - Math.Abs(((h * 6) % 2) - 1));
        float m = v - c;

        float rf, gf, bf;

        if (h < 1.0f / 6.0f)
        {
            rf = c; gf = x; bf = 0;
        }
        else if (h < 2.0f / 6.0f)
        {
            rf = x; gf = c; bf = 0;
        }
        else if (h < 3.0f / 6.0f)
        {
            rf = 0; gf = c; bf = x;
        }
        else if (h < 4.0f / 6.0f)
        {
            rf = 0; gf = x; bf = c;
        }
        else if (h < 5.0f / 6.0f)
        {
            rf = x; gf = 0; bf = c;
        }
        else
        {
            rf = c; gf = 0; bf = x;
        }

        r = (byte)Math.Round((rf + m) * 255);
        g = (byte)Math.Round((gf + m) * 255);
        b = (byte)Math.Round((bf + m) * 255);
    }

    /// <summary>
    /// Internal class representing a color mapping from source cluster to target color
    /// </summary>
    private class ColorMapping
    {
        public ColorCluster SourceCluster { get; set; } = new();
        public ColorInfo TargetColor { get; set; } = new();
        public float ReplacementStrength { get; set; }
        public bool PreserveLuminance { get; set; }
        public bool PreserveSaturation { get; set; }
    }
}
