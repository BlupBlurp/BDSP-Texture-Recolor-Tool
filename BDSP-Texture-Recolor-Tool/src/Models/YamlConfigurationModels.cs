using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BDSP.TextureRecolorTool.Models;

/// <summary>
/// Root configuration model for the YAML color palette file
/// Contains all Pokemon type color definitions loaded from external config
/// </summary>
public class PokemonTypeColorConfiguration
{
    [YamlMember(Alias = "pokemon_types")]
    public Dictionary<string, YamlTypeColorPalette> PokemonTypes { get; set; } = new();

    /// <summary>
    /// Configuration metadata like version and description
    /// </summary>
    [YamlMember(Alias = "config_info")]
    public ConfigurationInfo? ConfigInfo { get; set; }
}

/// <summary>
/// YAML representation of a type color palette with hex colors
/// This matches the structure users will edit in the YAML file
/// </summary>
public class YamlTypeColorPalette
{
    /// <summary>
    /// Main color for this type - most prominent and recognizable
    /// </summary>
    [YamlMember(Alias = "primary")]
    public string Primary { get; set; } = "#FFFFFF";

    /// <summary>
    /// Secondary/supporting color that complements the primary
    /// </summary>
    [YamlMember(Alias = "secondary")]
    public string Secondary { get; set; } = "#FFFFFF";

    /// <summary>
    /// Accent color for highlights and energetic details
    /// </summary>
    [YamlMember(Alias = "accent")]
    public string Accent { get; set; } = "#FFFFFF";

    /// <summary>
    /// Dark color for shadows and deep areas
    /// </summary>
    [YamlMember(Alias = "dark")]
    public string Dark { get; set; } = "#000000";

    /// <summary>
    /// Light color for highlights and bright areas
    /// </summary>
    [YamlMember(Alias = "light")]
    public string Light { get; set; } = "#FFFFFF";

    /// <summary>
    /// Neutral color for balanced areas and transitions
    /// </summary>
    [YamlMember(Alias = "neutral")]
    public string Neutral { get; set; } = "#808080";

    /// <summary>
    /// HSV color for HueShift algorithm - single target color in HSV space
    /// </summary>
    [YamlMember(Alias = "hue_shift_hsv")]
    public YamlHsvColor? HueShiftHsv { get; set; }
}

/// <summary>
/// HSV color representation for HueShift algorithm
/// </summary>
public class YamlHsvColor
{
    /// <summary>
    /// Hue value (0.0 to 1.0) - color position on color wheel
    /// </summary>
    [YamlMember(Alias = "hue")]
    public float Hue { get; set; }

    /// <summary>
    /// Saturation value (0.0 to 1.0) - color intensity/purity
    /// </summary>
    [YamlMember(Alias = "saturation")]
    public float Saturation { get; set; }

    /// <summary>
    /// Value/brightness (0.0 to 1.0) - lightness of the color
    /// </summary>
    [YamlMember(Alias = "value")]
    public float Value { get; set; }

}

/// <summary>
/// Configuration metadata for version tracking and description
/// </summary>
public class ConfigurationInfo
{
    [YamlMember(Alias = "version")]
    public string Version { get; set; } = "1.0";

    [YamlMember(Alias = "description")]
    public string Description { get; set; } = "Pokemon Type Color Palette Configuration";

    [YamlMember(Alias = "last_modified")]
    public string? LastModified { get; set; }
}

/// <summary>
/// Static utility class for creating and parsing YAML configuration
/// </summary>
public static class YamlConfigurationHelper
{
    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Parse YAML configuration from a string
    /// </summary>
    /// <param name="yamlContent">YAML content as string</param>
    /// <returns>Parsed configuration or null if invalid</returns>
    public static PokemonTypeColorConfiguration? ParseConfiguration(string yamlContent)
    {
        try
        {
            return _deserializer.Deserialize<PokemonTypeColorConfiguration>(yamlContent);
        }
        catch
        {
            return null; // Let calling code handle the null case
        }
    }

    /// <summary>
    /// Serialize configuration to YAML string
    /// </summary>
    /// <param name="configuration">Configuration to serialize</param>
    /// <returns>YAML string representation</returns>
    public static string SerializeConfiguration(PokemonTypeColorConfiguration configuration)
    {
        return _serializer.Serialize(configuration);
    }

    /// <summary>
    /// Convert a YamlTypeColorPalette to TypeColorPalette with error handling
    /// </summary>
    /// <param name="yamlPalette">YAML palette to convert</param>
    /// <param name="pokemonType">Pokemon type for this palette</param>
    /// <returns>Converted TypeColorPalette or null if conversion fails</returns>
    public static TypeColorPalette? ConvertToTypeColorPalette(YamlTypeColorPalette yamlPalette, PokemonType pokemonType)
    {
        try
        {
            var palette = new TypeColorPalette
            {
                Type = pokemonType,
                Name = pokemonType.ToString()  // Use type name since comments provide description
            };

            // Convert each hex color with validation
            if (TryParseHexColor(yamlPalette.Primary, out var primary))
                palette.Primary = primary;
            else
                return null;

            if (TryParseHexColor(yamlPalette.Secondary, out var secondary))
                palette.Secondary = secondary;
            else
                return null;

            if (TryParseHexColor(yamlPalette.Accent, out var accent))
                palette.Accent = accent;
            else
                return null;

            if (TryParseHexColor(yamlPalette.Dark, out var dark))
                palette.Dark = dark;
            else
                return null;

            if (TryParseHexColor(yamlPalette.Light, out var light))
                palette.Light = light;
            else
                return null;

            if (TryParseHexColor(yamlPalette.Neutral, out var neutral))
                palette.Neutral = neutral;
            else
                return null;

            return palette;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Try to parse a hex color string into a ColorInfo object
    /// Supports formats: #RRGGBB, #RGB, RRGGBB, RGB
    /// </summary>
    /// <param name="hexColor">Hex color string</param>
    /// <param name="colorInfo">Resulting ColorInfo if successful</param>
    /// <returns>True if parsing succeeded</returns>
    public static bool TryParseHexColor(string hexColor, out ColorInfo colorInfo)
    {
        colorInfo = new ColorInfo();

        if (string.IsNullOrWhiteSpace(hexColor))
            return false;

        // Remove # if present and trim whitespace
        string cleanHex = hexColor.Trim().TrimStart('#');

        try
        {
            byte r, g, b;

            if (cleanHex.Length == 6)
            {
                // Full format: RRGGBB
                r = Convert.ToByte(cleanHex.Substring(0, 2), 16);
                g = Convert.ToByte(cleanHex.Substring(2, 2), 16);
                b = Convert.ToByte(cleanHex.Substring(4, 2), 16);
            }
            else if (cleanHex.Length == 3)
            {
                // Short format: RGB -> RRGGBB
                r = Convert.ToByte(cleanHex.Substring(0, 1) + cleanHex.Substring(0, 1), 16);
                g = Convert.ToByte(cleanHex.Substring(1, 1) + cleanHex.Substring(1, 1), 16);
                b = Convert.ToByte(cleanHex.Substring(2, 1) + cleanHex.Substring(2, 1), 16);
            }
            else
            {
                return false; // Invalid length
            }

            colorInfo = new ColorInfo(r, g, b);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Convert TypeColorPalette back to YamlTypeColorPalette for serialization
    /// </summary>
    /// <param name="palette">TypeColorPalette to convert</param>
    /// <param name="hsvColor">Optional HSV color for HueShift algorithm</param>
    /// <returns>YamlTypeColorPalette representation</returns>
    public static YamlTypeColorPalette ConvertToYamlPalette(TypeColorPalette palette, TypeColorInfo? hsvColor = null)
    {
        var yamlPalette = new YamlTypeColorPalette
        {
            Primary = palette.Primary.HexString,
            Secondary = palette.Secondary.HexString,
            Accent = palette.Accent.HexString,
            Dark = palette.Dark.HexString,
            Light = palette.Light.HexString,
            Neutral = palette.Neutral.HexString
        };

        // Add HSV color if provided
        if (hsvColor != null)
        {
            yamlPalette.HueShiftHsv = new YamlHsvColor
            {
                Hue = hsvColor.Hue,
                Saturation = hsvColor.Saturation,
                Value = hsvColor.Value
            };
        }

        return yamlPalette;
    }

    /// <summary>
    /// Extract TypeColorInfo (HSV) from YamlTypeColorPalette
    /// </summary>
    /// <param name="yamlPalette">YAML palette containing HSV data</param>
    /// <param name="pokemonType">Pokemon type for this palette</param>
    /// <returns>TypeColorInfo for HueShift algorithm or null if not available</returns>
    public static TypeColorInfo? ExtractHsvColor(YamlTypeColorPalette yamlPalette, PokemonType pokemonType)
    {
        if (yamlPalette.HueShiftHsv == null)
            return null;

        return new TypeColorInfo
        {
            Type = pokemonType,
            Name = pokemonType.ToString(),  // Use type name since comments provide description
            Hue = Math.Clamp(yamlPalette.HueShiftHsv.Hue, 0.0f, 1.0f),
            Saturation = Math.Clamp(yamlPalette.HueShiftHsv.Saturation, 0.0f, 1.0f),
            Value = Math.Clamp(yamlPalette.HueShiftHsv.Value, 0.0f, 1.0f)
        };
    }
}