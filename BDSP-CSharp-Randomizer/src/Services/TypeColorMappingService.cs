using BDSP.CSharp.Randomizer.Models;
using Serilog;

namespace BDSP.CSharp.Randomizer.Services;

/// <summary>
/// Service for mapping Pokemon types to colors and generating color parameters
/// </summary>
public class TypeColorMappingService
{
    private readonly ILogger _logger;
    private readonly Dictionary<PokemonType, TypeColorInfo> _typeColors;

    public TypeColorMappingService()
    {
        _logger = Log.ForContext<TypeColorMappingService>();
        _typeColors = InitializeTypeColorMappings();

        _logger.Information("Initialized type color mappings for {Count} Pokemon types", _typeColors.Count);
    }

    /// <summary>
    /// Initialize the color mappings for all Pokemon types
    /// </summary>
    private Dictionary<PokemonType, TypeColorInfo> InitializeTypeColorMappings()
    {
        var mappings = new Dictionary<PokemonType, TypeColorInfo>();

        // Convert each type color to HSV values for consistent color manipulation
        mappings[PokemonType.Normal] = CreateTypeColor(PokemonType.Normal, "White", 0.0f, 0.0f, 0.95f);
        mappings[PokemonType.Fighting] = CreateTypeColor(PokemonType.Fighting, "Orange", 0.083f, 0.8f, 0.9f);
        mappings[PokemonType.Flying] = CreateTypeColor(PokemonType.Flying, "Sky Blue", 0.55f, 0.6f, 0.85f);
        mappings[PokemonType.Poison] = CreateTypeColor(PokemonType.Poison, "Purple", 0.83f, 0.7f, 0.7f);
        mappings[PokemonType.Ground] = CreateTypeColor(PokemonType.Ground, "Brown", 0.083f, 0.8f, 0.4f);
        mappings[PokemonType.Rock] = CreateTypeColor(PokemonType.Rock, "Olive Green", 0.17f, 0.6f, 0.5f);
        mappings[PokemonType.Bug] = CreateTypeColor(PokemonType.Bug, "Lime Green", 0.25f, 0.8f, 0.8f);
        mappings[PokemonType.Ghost] = CreateTypeColor(PokemonType.Ghost, "Indigo", 0.75f, 0.8f, 0.5f);
        mappings[PokemonType.Steel] = CreateTypeColor(PokemonType.Steel, "Silver Gray", 0.0f, 0.1f, 0.7f);
        mappings[PokemonType.Fire] = CreateTypeColor(PokemonType.Fire, "Red", 0.0f, 0.9f, 0.9f);
        mappings[PokemonType.Water] = CreateTypeColor(PokemonType.Water, "Deep Blue", 0.67f, 0.8f, 0.6f);
        mappings[PokemonType.Grass] = CreateTypeColor(PokemonType.Grass, "Forest Green", 0.33f, 0.8f, 0.4f);
        mappings[PokemonType.Electric] = CreateTypeColor(PokemonType.Electric, "Yellow", 0.17f, 0.9f, 0.95f);
        mappings[PokemonType.Psychic] = CreateTypeColor(PokemonType.Psychic, "Magenta Pink", 0.92f, 0.7f, 0.8f);
        mappings[PokemonType.Ice] = CreateTypeColor(PokemonType.Ice, "Cyan", 0.5f, 0.7f, 0.9f);
        mappings[PokemonType.Dragon] = CreateTypeColor(PokemonType.Dragon, "Navy Blue", 0.67f, 0.9f, 0.4f);
        mappings[PokemonType.Dark] = CreateTypeColor(PokemonType.Dark, "Black", 0.0f, 0.0f, 0.15f);
        mappings[PokemonType.Fairy] = CreateTypeColor(PokemonType.Fairy, "Light Pink", 0.92f, 0.4f, 0.95f);

        return mappings;
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