using BDSP.TextureRecolorTool.Models;
using Serilog;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for mapping Pokemon types to colors and generating color parameters
/// Now supports loading HSV colors from external YAML configuration with fallback to hardcoded defaults
/// </summary>
public class TypeColorMappingService
{
    private readonly ILogger _logger;
    private readonly Dictionary<PokemonType, TypeColorInfo> _typeColors;
    private readonly ColorPaletteConfigurationService? _configurationService;

    public TypeColorMappingService(ColorPaletteConfigurationService? configurationService = null)
    {
        _logger = Log.ForContext<TypeColorMappingService>();
        _configurationService = configurationService;
        _typeColors = InitializeTypeColorMappings();

        _logger.Information("Initialized type color mappings for {Count} Pokemon types {Source}",
            _typeColors.Count, _configurationService != null ? "(YAML + fallback)" : "(hardcoded)");
    }

    /// <summary>
    /// Initialize the color mappings for all Pokemon types
    /// First attempts to load from YAML configuration, then falls back to hardcoded defaults
    /// </summary>
    private Dictionary<PokemonType, TypeColorInfo> InitializeTypeColorMappings()
    {
        var mappings = new Dictionary<PokemonType, TypeColorInfo>();

        // Try to load from YAML configuration first
        Dictionary<PokemonType, TypeColorInfo>? yamlMappings = null;
        if (_configurationService != null)
        {
            try
            {
                yamlMappings = LoadHsvColorsFromYaml();
                if (yamlMappings?.Count > 0)
                {
                    _logger.Debug("Loaded {Count} HSV colors from YAML configuration", yamlMappings.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to load HSV colors from YAML, using hardcoded defaults");
            }
        }

        // Initialize with YAML or hardcoded values for each type
        var allTypes = Enum.GetValues<PokemonType>();
        foreach (var type in allTypes)
        {
            if (yamlMappings?.TryGetValue(type, out var yamlColor) == true)
            {
                mappings[type] = yamlColor;
                _logger.Debug("Using YAML HSV color for {Type}: {Name}", type, yamlColor.Name);
            }
            else
            {
                // Fallback to hardcoded defaults
                mappings[type] = GetHardcodedTypeColor(type);
                _logger.Debug("Using hardcoded HSV color for {Type}", type);
            }
        }

        return mappings;
    }

    /// <summary>
    /// Load HSV colors from YAML configuration using the ExtractHsvColor helper
    /// </summary>
    private Dictionary<PokemonType, TypeColorInfo>? LoadHsvColorsFromYaml()
    {
        if (_configurationService == null)
            return null;

        // Get the raw YAML configuration that includes HSV data
        var yamlConfigTask = _configurationService.LoadRawYamlConfigurationAsync();
        yamlConfigTask.Wait(); // TODO: make this method async to avoid blocking
        var yamlConfig = yamlConfigTask.Result;

        if (yamlConfig?.PokemonTypes == null)
            return null;

        var hsvMappings = new Dictionary<PokemonType, TypeColorInfo>();

        foreach (var kvp in yamlConfig.PokemonTypes)
        {
            if (Enum.TryParse<PokemonType>(kvp.Key, true, out var pokemonType))
            {
                var hsvColor = YamlConfigurationHelper.ExtractHsvColor(kvp.Value, pokemonType);
                if (hsvColor != null)
                {
                    hsvMappings[pokemonType] = hsvColor;
                }
            }
        }

        return hsvMappings.Count > 0 ? hsvMappings : null;
    }

    /// <summary>
    /// Get hardcoded HSV color values as fallback when YAML is not available
    /// </summary>
    private TypeColorInfo GetHardcodedTypeColor(PokemonType type)
    {
        return type switch
        {
            PokemonType.Normal => CreateTypeColor(PokemonType.Normal, "White", 0.0f, 0.0f, 0.95f),
            PokemonType.Fighting => CreateTypeColor(PokemonType.Fighting, "Orange", 0.083f, 0.8f, 0.9f),
            PokemonType.Flying => CreateTypeColor(PokemonType.Flying, "Sky Blue", 0.55f, 0.6f, 0.85f),
            PokemonType.Poison => CreateTypeColor(PokemonType.Poison, "Purple", 0.83f, 0.7f, 0.7f),
            PokemonType.Ground => CreateTypeColor(PokemonType.Ground, "Brown", 0.083f, 0.8f, 0.4f),
            PokemonType.Rock => CreateTypeColor(PokemonType.Rock, "Olive Green", 0.17f, 0.6f, 0.5f),
            PokemonType.Bug => CreateTypeColor(PokemonType.Bug, "Lime Green", 0.25f, 0.8f, 0.8f),
            PokemonType.Ghost => CreateTypeColor(PokemonType.Ghost, "Indigo", 0.75f, 0.8f, 0.5f),
            PokemonType.Steel => CreateTypeColor(PokemonType.Steel, "Silver Gray", 0.0f, 0.1f, 0.7f),
            PokemonType.Fire => CreateTypeColor(PokemonType.Fire, "Red", 0.0f, 0.9f, 0.9f),
            PokemonType.Water => CreateTypeColor(PokemonType.Water, "Deep Blue", 0.67f, 0.8f, 0.6f),
            PokemonType.Grass => CreateTypeColor(PokemonType.Grass, "Forest Green", 0.33f, 0.8f, 0.4f),
            PokemonType.Electric => CreateTypeColor(PokemonType.Electric, "Yellow", 0.17f, 0.9f, 0.95f),
            PokemonType.Psychic => CreateTypeColor(PokemonType.Psychic, "Magenta Pink", 0.92f, 0.7f, 0.8f),
            PokemonType.Ice => CreateTypeColor(PokemonType.Ice, "Cyan", 0.5f, 0.7f, 0.9f),
            PokemonType.Dragon => CreateTypeColor(PokemonType.Dragon, "Navy Blue", 0.67f, 0.9f, 0.4f),
            PokemonType.Dark => CreateTypeColor(PokemonType.Dark, "Black", 0.0f, 0.0f, 0.15f),
            PokemonType.Fairy => CreateTypeColor(PokemonType.Fairy, "Light Pink", 0.92f, 0.4f, 0.95f),
            _ => CreateTypeColor(PokemonType.Normal, "Unknown", 0.0f, 0.0f, 0.95f)
        };
    }

    /// <summary>
    /// Create a TypeColorInfo object with HSV values
    /// </summary>
    private TypeColorInfo CreateTypeColor(PokemonType type, string name, float hue, float saturation, float value)
    {
        return new TypeColorInfo
        {
            Type = type,
            Name = name,
            Hue = hue,
            Saturation = saturation,
            Value = value
        };
    }

    /// <summary>
    /// Generate bundle color parameters based on Pokemon type
    /// </summary>
    /// <param name="pokemonType">Pokemon type (0-17)</param>
    /// <param name="algorithm">Color algorithm to use</param>
    /// <returns>Color parameters for the bundle</returns>
    public BundleColorParameters GenerateTypeBasedColorParameters(int pokemonType, ColorAlgorithm algorithm = ColorAlgorithm.HueShift)
    {
        if (!Enum.IsDefined(typeof(PokemonType), pokemonType))
        {
            _logger.Warning("Invalid Pokemon type: {Type}, using Normal type as fallback", pokemonType);
            pokemonType = (int)PokemonType.Normal;
        }

        var type = (PokemonType)pokemonType;
        var typeColor = _typeColors[type];

        _logger.Debug("Generating color parameters for {TypeName} type - H:{Hue:F3} S:{Saturation:F3} V:{Value:F3}",
            typeColor.Name, typeColor.Hue, typeColor.Saturation, typeColor.Value);

        // For type-based coloring, we use the exact target HSV values
        return new BundleColorParameters
        {
            HueShift = typeColor.Hue,           // Target hue (not a shift)
            SaturationVariation = typeColor.Saturation, // Target saturation (not a multiplier)
            TargetValue = typeColor.Value,      // Target brightness/value
            IsTypeBased = true,
            PokemonType = pokemonType,
            Algorithm = algorithm,
            ReplacementParameters = algorithm == ColorAlgorithm.ColorReplacement
                ? new ColorReplacementParameters()
                : null
        };
    }

    /// <summary>
    /// Get color info for a specific Pokemon type
    /// </summary>
    /// <param name="pokemonType">Pokemon type (0-17)</param>
    /// <returns>Type color information</returns>
    public TypeColorInfo? GetTypeColorInfo(int pokemonType)
    {
        if (Enum.IsDefined(typeof(PokemonType), pokemonType))
        {
            var type = (PokemonType)pokemonType;
            return _typeColors.TryGetValue(type, out var colorInfo) ? colorInfo : null;
        }
        return null;
    }

    /// <summary>
    /// Get all available type color mappings
    /// </summary>
    /// <returns>Dictionary of all type color mappings</returns>
    public IReadOnlyDictionary<PokemonType, TypeColorInfo> GetAllTypeMappings()
    {
        return _typeColors;
    }
}
