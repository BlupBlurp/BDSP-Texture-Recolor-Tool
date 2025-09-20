using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using BDSP.TextureRecolorTool.Models;
using Serilog;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for applying color randomization to textures using HSV color space manipulation
/// or advanced color replacement algorithms
/// </summary>
public class ColorRandomizationService
{
    private readonly ILogger _logger;
    private readonly Random _random;
    private readonly ColorAnalysisService? _colorAnalysisService;
    private readonly ColorReplacementService? _colorReplacementService;
    private readonly TypeColorPaletteService? _typeColorPaletteService;

    public ColorRandomizationService()
    {
        _logger = Log.ForContext<ColorRandomizationService>();
        _random = new Random();
    }

    public ColorRandomizationService(Random random)
    {
        _logger = Log.ForContext<ColorRandomizationService>();
        _random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// Constructor with advanced color replacement services
    /// </summary>
    public ColorRandomizationService(Random random, ColorAnalysisService? colorAnalysisService = null,
        ColorReplacementService? colorReplacementService = null, TypeColorPaletteService? typeColorPaletteService = null)
    {
        _logger = Log.ForContext<ColorRandomizationService>();
        _random = random ?? throw new ArgumentNullException(nameof(random));
        _colorAnalysisService = colorAnalysisService;
        _colorReplacementService = colorReplacementService;
        _typeColorPaletteService = typeColorPaletteService;
    }

    /// <summary>
    /// Generate consistent color parameters for a bundle
    /// </summary>
    /// <returns>Color parameters to be applied consistently across all textures in the bundle</returns>
    public BundleColorParameters GenerateBundleColorParameters()
    {
        // Generate consistent parameters per bundle (not per texture)
        // This ensures all textures in a Pokemon bundle have the same color transformation
        var hueShift = (_random.NextSingle() - 0.5f) * 2.0f; // Range: -1.0 to 1.0 (full hue wheel)
        var saturationVariation = 0.8f + _random.NextSingle() * 0.4f; // Range: 0.8 to 1.2

        _logger.Debug("Generated bundle color parameters - Hue shift: {HueShift:F3}, Saturation variation: {SaturationVariation:F3}",
            hueShift, saturationVariation);

        return new BundleColorParameters
        {
            HueShift = hueShift,
            SaturationVariation = saturationVariation
        };
    }

    /// <summary>
    /// Apply color randomization to an image using consistent bundle parameters
    /// </summary>
    /// <param name="sourceImage">Source image to modify</param>
    /// <param name="colorParams">Color parameters consistent for the bundle</param>
    /// <param name="textureName">Name of the texture being processed (for eye detection)</param>
    /// <returns>Modified image</returns>
    public Image<Rgba32> RandomizeTextureColor(Image<Rgba32> sourceImage, BundleColorParameters colorParams, string textureName = "")
    {
        bool isEyeTexture = IsEyeTexture(textureName);

        if (colorParams.IsTypeBased)
        {
            _logger.Debug("Applying type-based coloring - Type: {Type}, Algorithm: {Algorithm}",
                colorParams.PokemonType, colorParams.Algorithm);

            // Use advanced color replacement algorithm if available and requested
            if (colorParams.Algorithm == ColorAlgorithm.ColorReplacement &&
                _colorAnalysisService != null && _colorReplacementService != null && _typeColorPaletteService != null)
            {
                return ApplyAdvancedColorReplacement(sourceImage, colorParams, textureName);
            }
            else
            {
                // Fall back to HSV hue shifting
                return ApplyHsvColorTransformation(sourceImage, colorParams, textureName);
            }
        }
        else
        {
            _logger.Debug("Applying random coloring - HueShift: {HueShift}, SaturationVariation: {SaturationVariation}",
                colorParams.HueShift, colorParams.SaturationVariation);

            return ApplyHsvColorTransformation(sourceImage, colorParams, textureName);
        }
    }

    /// <summary>
    /// Apply advanced color replacement using color analysis and palette mapping
    /// </summary>
    private Image<Rgba32> ApplyAdvancedColorReplacement(Image<Rgba32> sourceImage, BundleColorParameters colorParams, string textureName)
    {
        if (_colorAnalysisService == null || _colorReplacementService == null || _typeColorPaletteService == null)
        {
            _logger.Warning("Advanced color replacement services not available, falling back to HSV transformation");
            return ApplyHsvColorTransformation(sourceImage, colorParams, textureName);
        }

        if (!colorParams.PokemonType.HasValue)
        {
            _logger.Warning("Pokemon type not specified for color replacement, falling back to HSV transformation");
            return ApplyHsvColorTransformation(sourceImage, colorParams, textureName);
        }

        _logger.Debug("Starting advanced color replacement for Pokemon type {Type}", colorParams.PokemonType);

        try
        {
            // Analyze the texture to understand its color composition
            var analysis = _colorAnalysisService.AnalyzeTexture(sourceImage, textureName);

            // Get the type palette
            var palette = _typeColorPaletteService.GetPaletteForType(colorParams.PokemonType.Value);

            // Use provided replacement parameters or create default ones
            var replacementParams = colorParams.ReplacementParameters ?? new ColorReplacementParameters();

            // Apply color replacement
            var result = _colorReplacementService.ReplaceColors(sourceImage, analysis, palette, replacementParams);

            _logger.Debug("Advanced color replacement completed successfully");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in advanced color replacement, falling back to HSV transformation");
            return ApplyHsvColorTransformation(sourceImage, colorParams, textureName);
        }
    }

    /// <summary>
    /// Apply HSV-based color transformation (original behavior)
    /// </summary>
    private Image<Rgba32> ApplyHsvColorTransformation(Image<Rgba32> sourceImage, BundleColorParameters colorParams, string textureName)
    {
        bool isEyeTexture = IsEyeTexture(textureName);

        if (colorParams.IsTypeBased)
        {
            _logger.Debug("Applying type-based HSV coloring - Type: {Type}, Target H:{Hue:F3} S:{Saturation:F3} V:{Value:F3}",
                colorParams.PokemonType, colorParams.HueShift, colorParams.SaturationVariation, colorParams.TargetValue);
        }
        else
        {
            _logger.Debug("Applying random HSV coloring - HueShift: {HueShift}, SaturationVariation: {SaturationVariation}",
                colorParams.HueShift, colorParams.SaturationVariation);
        }

        if (isEyeTexture)
        {
            _logger.Debug("Detected eye texture: {TextureName} - White colors will be preserved", textureName);
        }

        _logger.Debug("Input image: {Width}x{Height}", sourceImage.Width, sourceImage.Height);

        // Create a copy of the source image
        var modifiedImage = sourceImage.Clone();

        try
        {
            int pixelsProcessed = 0;
            int pixelsSkipped = 0;
            int whitePixelsPreserved = 0;
            int greyPixelsPreserved = 0;

            // For eye textures, we need to preserve grey shadows adjacent to white areas
            // This requires checking neighboring pixels, so we process differently
            if (isEyeTexture)
            {
                // Process eye textures with adjacency checking
                for (int y = 0; y < modifiedImage.Height; y++)
                {
                    for (int x = 0; x < modifiedImage.Width; x++)
                    {
                        var pixel = modifiedImage[x, y];

                        // Skip transparent pixels
                        if (pixel.A == 0)
                        {
                            pixelsSkipped++;
                            continue;
                        }

                        // Preserve white colors in eye textures
                        if (IsWhiteColor(pixel.R, pixel.G, pixel.B))
                        {
                            whitePixelsPreserved++;
                            continue; // Skip color transformation for white pixels
                        }

                        // Check for grey shadow colors in eye textures
                        if (isEyeTexture && IsGreyShadowColor(pixel.R, pixel.G, pixel.B))
                        {
                            greyPixelsPreserved++;
                            continue; // Skip color transformation for all grey shadows in eye textures
                        }

                        pixelsProcessed++;

                        // Convert RGB to HSV
                        RgbToHsv(pixel.R, pixel.G, pixel.B, out float h, out float s, out float v);

                        if (colorParams.IsTypeBased)
                        {
                            ApplyTypeBasedColoring(ref h, ref s, ref v, colorParams);
                        }
                        else
                        {
                            ApplyRandomColoring(ref h, ref s, ref v, colorParams);
                        }

                        // Convert back to RGB and update pixel
                        HsvToRgb(h, s, v, out byte r, out byte g, out byte b);
                        modifiedImage[x, y] = new Rgba32(r, g, b, pixel.A);
                    }
                }
            }
            else
            {
                // Process non-eye textures normally (faster path)
                modifiedImage.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var pixelRow = accessor.GetRowSpan(y);

                        for (int x = 0; x < pixelRow.Length; x++)
                        {
                            ref var pixel = ref pixelRow[x];

                            // Skip transparent pixels
                            if (pixel.A == 0)
                            {
                                pixelsSkipped++;
                                continue;
                            }

                            pixelsProcessed++;

                            pixelsProcessed++;

                            // Convert RGB to HSV
                            RgbToHsv(pixel.R, pixel.G, pixel.B, out float h, out float s, out float v);

                            if (colorParams.IsTypeBased)
                            {
                                // Type-based coloring: shift colors toward the target type color
                                ApplyTypeBasedColoring(ref h, ref s, ref v, colorParams);
                            }
                            else
                            {
                                // Random coloring: apply hue shift and saturation variation
                                ApplyRandomColoring(ref h, ref s, ref v, colorParams);
                            }

                            // Convert back to RGB
                            HsvToRgb(h, s, v, out byte r, out byte g, out byte b);

                            // Update pixel with new values, preserving alpha
                            pixel.R = r;
                            pixel.G = g;
                            pixel.B = b;
                            // Alpha remains unchanged
                        }
                    }
                });
            }

            if (isEyeTexture && (whitePixelsPreserved > 0 || greyPixelsPreserved > 0))
            {
                _logger.Debug("Color processing complete - Pixels processed: {Processed}, Pixels skipped: {Skipped}, White pixels preserved: {WhitePreserved}, Grey shadow pixels preserved: {GreyPreserved}",
                    pixelsProcessed, pixelsSkipped, whitePixelsPreserved, greyPixelsPreserved);
            }
            else if (isEyeTexture)
            {
                _logger.Debug("Color processing complete - Pixels processed: {Processed}, Pixels skipped: {Skipped}",
                    pixelsProcessed, pixelsSkipped);
            }
            else
            {
                _logger.Debug("Color processing complete - Pixels processed: {Processed}, Pixels skipped: {Skipped}",
                    pixelsProcessed, pixelsSkipped);
            }

            _logger.Debug("Color transformation applied successfully");
            return modifiedImage;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error applying color transformation");
            modifiedImage.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Apply type-based coloring transformation
    /// </summary>
    private void ApplyTypeBasedColoring(ref float h, ref float s, ref float v, BundleColorParameters colorParams)
    {
        // For type-based coloring, we want to shift all colors toward the target type color
        // while preserving the relative luminance and some color variation

        // Preserve the original luminance for natural-looking results
        float originalLuminance = v;

        // Blend the hue toward the target type hue, but don't completely replace it
        // This allows for some color variation while still being recognizably the type color
        float hueBlendFactor = 0.7f; // 70% toward target hue, 30% original
        h = BlendHue(h, colorParams.HueShift, hueBlendFactor);

        // Adjust saturation toward target, but maintain some of the original character
        float saturationBlendFactor = 0.6f; // 60% toward target saturation
        s = s * (1 - saturationBlendFactor) + colorParams.SaturationVariation * saturationBlendFactor;
        s = Math.Clamp(s, 0.0f, 1.0f);

        // Adjust value/brightness toward target, preserving some original luminance
        if (colorParams.TargetValue.HasValue)
        {
            float valueBlendFactor = 0.5f; // 50% toward target value
            v = originalLuminance * (1 - valueBlendFactor) + colorParams.TargetValue.Value * valueBlendFactor;
            v = Math.Clamp(v, 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Apply random coloring transformation (original behavior)
    /// </summary>
    private void ApplyRandomColoring(ref float h, ref float s, ref float v, BundleColorParameters colorParams)
    {
        // Apply hue shift (wrap around at 360 degrees)
        h = (h + colorParams.HueShift) % 1.0f;
        if (h < 0) h += 1.0f;

        // Apply saturation variation (clamp to valid range)
        s = Math.Clamp(s * colorParams.SaturationVariation, 0.0f, 1.0f);
    }

    /// <summary>
    /// Blend two hue values, accounting for the circular nature of hue
    /// </summary>
    private float BlendHue(float hue1, float hue2, float blendFactor)
    {
        // Calculate the shortest angular distance between hues
        float diff = hue2 - hue1;

        // Normalize the difference to [-0.5, 0.5] range
        if (diff > 0.5f) diff -= 1.0f;
        if (diff < -0.5f) diff += 1.0f;

        // Blend and wrap the result
        float result = hue1 + diff * blendFactor;
        if (result < 0) result += 1.0f;
        if (result >= 1.0f) result -= 1.0f;

        return result;
    }

    /// <summary>
    /// Convert RGB values to HSV color space
    /// </summary>
    /// <param name="r">Red component (0-255)</param>
    /// <param name="g">Green component (0-255)</param>
    /// <param name="b">Blue component (0-255)</param>
    /// <param name="h">Hue output (0-1)</param>
    /// <param name="s">Saturation output (0-1)</param>
    /// <param name="v">Value output (0-1)</param>
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

    /// <summary>
    /// Convert HSV values to RGB color space
    /// </summary>
    /// <param name="h">Hue (0-1)</param>
    /// <param name="s">Saturation (0-1)</param>
    /// <param name="v">Value (0-1)</param>
    /// <param name="r">Red output (0-255)</param>
    /// <param name="g">Green output (0-255)</param>
    /// <param name="b">Blue output (0-255)</param>
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
    /// Check if a texture is an eye/iris texture based on its name
    /// </summary>
    /// <param name="textureName">Name of the texture</param>
    /// <returns>True if this is an eye/iris texture</returns>
    private static bool IsEyeTexture(string textureName)
    {
        if (string.IsNullOrEmpty(textureName))
            return false;

        // Check for eye/iris indicators in texture name (case-insensitive)
        var lowerName = textureName.ToLowerInvariant();
        return lowerName.Contains("eye") || lowerName.Contains("iris");
    }

    /// <summary>
    /// Check if a color is considered "white" that should be preserved in eye textures
    /// </summary>
    /// <param name="r">Red component (0-255)</param>
    /// <param name="g">Green component (0-255)</param>
    /// <param name="b">Blue component (0-255)</param>
    /// <returns>True if the color should be preserved as white</returns>
    private static bool IsWhiteColor(byte r, byte g, byte b)
    {
        // Target white color #f0f0f0 (240, 240, 240)
        // Allow for some tolerance to account for compression artifacts
        const int targetWhite = 240;
        const int tolerance = 15; // Allow RGB values from 225 to 255

        return r >= (targetWhite - tolerance) &&
               g >= (targetWhite - tolerance) &&
               b >= (targetWhite - tolerance);
    }

    /// <summary>
    /// Check if a color is considered a "grey shadow" that should be preserved in eye textures when adjacent to white
    /// </summary>
    /// <param name="r">Red component (0-255)</param>
    /// <param name="g">Green component (0-255)</param>
    /// <param name="b">Blue component (0-255)</param>
    /// <returns>True if the color is a grey shadow that could be preserved</returns>
    private static bool IsGreyShadowColor(byte r, byte g, byte b)
    {
        // Expanded grey shadow range to capture more of the shadow area
        // Covers darker shadows (~150) to lighter greys (~240)
        const int minGrey = 150; // Darker shadow tones
        const int maxGrey = 240; // Lighter grey tones (but not white)
        const int tolerance = 20; // Allow more variance for shadow gradients

        // Check if all components are in the grey range
        bool inRange = r >= minGrey && r <= maxGrey &&
                       g >= minGrey && g <= maxGrey &&
                       b >= minGrey && b <= maxGrey;

        if (!inRange) return false;

        // Check that the color is relatively neutral (not too much difference between R, G, B)
        int maxComponent = Math.Max(r, Math.Max(g, b));
        int minComponent = Math.Min(r, Math.Min(g, b));

        return (maxComponent - minComponent) <= tolerance;
    }

    /// <summary>
    /// Check if a pixel is adjacent to a white pixel that would be preserved
    /// </summary>
    /// <param name="image">The image to check</param>
    /// <param name="x">X coordinate of the pixel</param>
    /// <param name="y">Y coordinate of the pixel</param>
    /// <returns>True if the pixel is adjacent to a white pixel</returns>
    private static bool IsAdjacentToWhitePixel(Image<Rgba32> image, int x, int y)
    {
        // Check the 8 surrounding pixels (3x3 grid minus center)
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                // Skip the center pixel (the one we're checking for)
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                // Check bounds
                if (nx >= 0 && nx < image.Width && ny >= 0 && ny < image.Height)
                {
                    var neighborPixel = image[nx, ny];

                    // If the neighbor is white (and not transparent), this grey pixel should be preserved
                    if (neighborPixel.A > 0 && IsWhiteColor(neighborPixel.R, neighborPixel.G, neighborPixel.B))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}
