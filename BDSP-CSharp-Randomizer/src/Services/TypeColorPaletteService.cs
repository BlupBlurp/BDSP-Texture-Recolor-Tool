using BDSP.CSharp.Randomizer.Models;
using Serilog;

namespace BDSP.CSharp.Randomizer.Services;

/// <summary>
/// Service providing comprehensive color palettes for each Pokemon type with multiple color categories
/// This replaces the simple single-color approach with rich, recognizable type-themed palettes
/// </summary>
public class TypeColorPaletteService
{
    private readonly ILogger _logger;
    private readonly Dictionary<PokemonType, TypeColorPalette> _typePalettes;

    public TypeColorPaletteService()
    {
        _logger = Log.ForContext<TypeColorPaletteService>();
        _typePalettes = InitializeTypePalettes();

        _logger.Information("Initialized comprehensive color palettes for {Count} Pokemon types", _typePalettes.Count);
    }

    /// <summary>
    /// Get the complete color palette for a Pokemon type
    /// </summary>
    /// <param name="type">Pokemon type</param>
    /// <returns>Comprehensive color palette for the type</returns>
    public TypeColorPalette GetPaletteForType(PokemonType type)
    {
        if (_typePalettes.TryGetValue(type, out var palette))
        {
            return palette;
        }

        _logger.Warning("No palette found for type {Type}, returning Normal type palette", type);
        return _typePalettes[PokemonType.Normal];
    }

    /// <summary>
    /// Get the complete color palette for a Pokemon type by ID
    /// </summary>
    /// <param name="typeId">Pokemon type ID (0-17)</param>
    /// <returns>Comprehensive color palette for the type</returns>
    public TypeColorPalette GetPaletteForType(int typeId)
    {
        if (Enum.IsDefined(typeof(PokemonType), typeId))
        {
            return GetPaletteForType((PokemonType)typeId);
        }

        _logger.Warning("Invalid type ID {TypeId}, returning Normal type palette", typeId);
        return _typePalettes[PokemonType.Normal];
    }

    /// <summary>
    /// Get all available type palettes
    /// </summary>
    /// <returns>Dictionary of all type palettes</returns>
    public IReadOnlyDictionary<PokemonType, TypeColorPalette> GetAllPalettes()
    {
        return _typePalettes;
    }

    /// <summary>
    /// Initialize comprehensive color palettes for all Pokemon types
    /// Each palette includes primary, secondary, accent, dark, light, and neutral colors
    /// </summary>
    private Dictionary<PokemonType, TypeColorPalette> InitializeTypePalettes()
    {
        var palettes = new Dictionary<PokemonType, TypeColorPalette>();

        // NORMAL - Natural, earthy tones with warm neutrals
        palettes[PokemonType.Normal] = new TypeColorPalette
        {
            Type = PokemonType.Normal,
            Name = "Natural Tones",
            Primary = CreateColor(210, 180, 140),    // Tan/Beige
            Secondary = CreateColor(160, 140, 120),  // Warm brown
            Accent = CreateColor(240, 220, 200),     // Cream
            Dark = CreateColor(90, 70, 50),          // Dark brown
            Light = CreateColor(250, 240, 230),      // Off-white
            Neutral = CreateColor(180, 160, 140)     // Medium tan
        };

        // FIGHTING - Earthy oranges and browns with red accents
        palettes[PokemonType.Fighting] = new TypeColorPalette
        {
            Type = PokemonType.Fighting,
            Name = "Warrior Earth",
            Primary = CreateColor(180, 100, 60),     // Burnt orange
            Secondary = CreateColor(140, 80, 40),    // Deep brown
            Accent = CreateColor(220, 60, 40),       // Fighting red
            Dark = CreateColor(80, 40, 20),          // Dark earth
            Light = CreateColor(240, 180, 120),      // Light orange
            Neutral = CreateColor(160, 120, 80)      // Earthy tan
        };

        // FLYING - Sky blues with cloud whites and wind greys
        palettes[PokemonType.Flying] = new TypeColorPalette
        {
            Type = PokemonType.Flying,
            Name = "Sky Elements",
            Primary = CreateColor(120, 180, 240),    // Sky blue
            Secondary = CreateColor(200, 220, 240),  // Cloud grey-blue
            Accent = CreateColor(255, 255, 255),     // Pure white
            Dark = CreateColor(60, 100, 160),        // Storm blue
            Light = CreateColor(230, 240, 255),      // Light sky
            Neutral = CreateColor(180, 200, 220)     // Soft grey-blue
        };

        // POISON - Purple and magenta with toxic greens
        palettes[PokemonType.Poison] = new TypeColorPalette
        {
            Type = PokemonType.Poison,
            Name = "Toxic Purples",
            Primary = CreateColor(160, 80, 200),     // Rich purple
            Secondary = CreateColor(120, 60, 140),   // Dark purple
            Accent = CreateColor(160, 255, 80),      // Toxic green
            Dark = CreateColor(80, 40, 100),         // Deep purple
            Light = CreateColor(220, 180, 255),      // Light purple
            Neutral = CreateColor(140, 120, 160)     // Muted purple
        };

        // GROUND - Earth tones with clay and sand colors
        palettes[PokemonType.Ground] = new TypeColorPalette
        {
            Type = PokemonType.Ground,
            Name = "Earth Elements",
            Primary = CreateColor(140, 100, 60),     // Clay brown
            Secondary = CreateColor(200, 160, 100),  // Sandy brown
            Accent = CreateColor(80, 200, 120),      // Earth green
            Dark = CreateColor(60, 40, 20),          // Dark soil
            Light = CreateColor(220, 200, 160),      // Light sand
            Neutral = CreateColor(160, 140, 100)     // Dusty brown
        };

        // ROCK - Stone greys with mineral colors
        palettes[PokemonType.Rock] = new TypeColorPalette
        {
            Type = PokemonType.Rock,
            Name = "Stone Minerals",
            Primary = CreateColor(140, 130, 120),    // Stone grey
            Secondary = CreateColor(100, 90, 80),    // Dark stone
            Accent = CreateColor(200, 180, 140),     // Quartz beige
            Dark = CreateColor(60, 55, 50),          // Dark rock
            Light = CreateColor(200, 190, 180),      // Light stone
            Neutral = CreateColor(120, 115, 110)     // Medium stone
        };

        // Bug type - Forest greens with nature accents
        palettes[PokemonType.Bug] = new TypeColorPalette
        {
            Type = PokemonType.Bug,
            Name = "Nature Greens",
            Primary = CreateColor(100, 160, 60),     // Forest green
            Secondary = CreateColor(60, 120, 40),    // Deep green
            Accent = CreateColor(200, 240, 80),      // Bright lime
            Dark = CreateColor(40, 80, 20),          // Dark forest
            Light = CreateColor(160, 220, 120),      // Light green
            Neutral = CreateColor(120, 140, 100)     // Sage green
        };

        // GHOST - Dark purples with ethereal blues
        palettes[PokemonType.Ghost] = new TypeColorPalette
        {
            Type = PokemonType.Ghost,
            Name = "Ethereal Shadows",
            Primary = CreateColor(80, 60, 120),      // Dark purple
            Secondary = CreateColor(60, 80, 140),    // Ethereal blue
            Accent = CreateColor(200, 180, 255),     // Spirit light
            Dark = CreateColor(40, 30, 60),          // Deep shadow
            Light = CreateColor(160, 140, 200),      // Ghostly light
            Neutral = CreateColor(100, 90, 130)      // Muted purple
        };

        // STEEL - Metallic silvers with chrome highlights
        palettes[PokemonType.Steel] = new TypeColorPalette
        {
            Type = PokemonType.Steel,
            Name = "Metallic Chrome",
            Primary = CreateColor(180, 180, 200),    // Steel grey
            Secondary = CreateColor(140, 140, 160),  // Dark metal
            Accent = CreateColor(240, 240, 255),     // Chrome highlight
            Dark = CreateColor(80, 80, 100),         // Dark steel
            Light = CreateColor(220, 220, 240),      // Bright metal
            Neutral = CreateColor(160, 160, 180)     // Medium steel
        };

        // FIRE - Reds and oranges with yellow flames
        palettes[PokemonType.Fire] = new TypeColorPalette
        {
            Type = PokemonType.Fire,
            Name = "Flame Colors",
            Primary = CreateColor(220, 80, 40),      // Fire red
            Secondary = CreateColor(255, 140, 60),   // Orange flame
            Accent = CreateColor(255, 220, 60),      // Yellow flame
            Dark = CreateColor(140, 40, 20),         // Ember red
            Light = CreateColor(255, 200, 120),      // Bright flame
            Neutral = CreateColor(200, 120, 80)      // Warm orange
        };

        // WATER - Ocean blues with aqua highlights
        palettes[PokemonType.Water] = new TypeColorPalette
        {
            Type = PokemonType.Water,
            Name = "Ocean Blues",
            Primary = CreateColor(60, 120, 200),     // Ocean blue
            Secondary = CreateColor(40, 100, 160),   // Deep blue
            Accent = CreateColor(120, 220, 255),     // Aqua highlight
            Dark = CreateColor(20, 60, 120),         // Deep ocean
            Light = CreateColor(160, 200, 255),      // Light blue
            Neutral = CreateColor(100, 160, 200)     // Medium blue
        };

        // GRASS - Rich greens with natural accents
        palettes[PokemonType.Grass] = new TypeColorPalette
        {
            Type = PokemonType.Grass,
            Name = "Verdant Greens",
            Primary = CreateColor(80, 160, 80),      // Grass green
            Secondary = CreateColor(60, 120, 60),    // Forest green
            Accent = CreateColor(160, 240, 120),     // Bright leaf
            Dark = CreateColor(40, 80, 40),          // Dark foliage
            Light = CreateColor(140, 220, 140),      // Light green
            Neutral = CreateColor(100, 140, 100)     // Medium green
        };

        // ELECTRIC - Bright yellows with electric blues
        palettes[PokemonType.Electric] = new TypeColorPalette
        {
            Type = PokemonType.Electric,
            Name = "Electric Energy",
            Primary = CreateColor(255, 220, 60),     // Electric yellow
            Secondary = CreateColor(255, 180, 40),   // Orange-yellow
            Accent = CreateColor(120, 200, 255),     // Electric blue
            Dark = CreateColor(180, 140, 20),        // Dark yellow
            Light = CreateColor(255, 255, 200),      // Bright yellow
            Neutral = CreateColor(220, 200, 100)     // Muted yellow
        };

        // PSYCHIC - Magentas and pinks with psychic purples
        palettes[PokemonType.Psychic] = new TypeColorPalette
        {
            Type = PokemonType.Psychic,
            Name = "Psychic Energy",
            Primary = CreateColor(220, 80, 160),     // Magenta
            Secondary = CreateColor(180, 60, 140),   // Deep pink
            Accent = CreateColor(140, 120, 255),     // Psychic purple
            Dark = CreateColor(120, 40, 80),         // Dark magenta
            Light = CreateColor(255, 160, 220),      // Light pink
            Neutral = CreateColor(200, 120, 180)     // Muted pink
        };

        // ICE - Icy blues and cyans with crystalline whites
        palettes[PokemonType.Ice] = new TypeColorPalette
        {
            Type = PokemonType.Ice,
            Name = "Crystalline Ice",
            Primary = CreateColor(140, 220, 255),    // Ice blue
            Secondary = CreateColor(100, 180, 220),  // Deep ice
            Accent = CreateColor(255, 255, 255),     // Crystal white
            Dark = CreateColor(60, 140, 180),        // Deep ice
            Light = CreateColor(220, 240, 255),      // Light ice
            Neutral = CreateColor(180, 220, 240)     // Soft ice blue
        };

        // DRAGON - Mystical purples with royal colors
        palettes[PokemonType.Dragon] = new TypeColorPalette
        {
            Type = PokemonType.Dragon,
            Name = "Mystical Dragon",
            Primary = CreateColor(100, 60, 160),     // Dragon purple
            Secondary = CreateColor(60, 80, 140),    // Royal blue
            Accent = CreateColor(255, 200, 60),      // Golden accent
            Dark = CreateColor(40, 20, 80),          // Deep purple
            Light = CreateColor(160, 120, 220),      // Light purple
            Neutral = CreateColor(120, 100, 160)     // Muted purple
        };

        // DARK - Deep blacks with shadow colors
        palettes[PokemonType.Dark] = new TypeColorPalette
        {
            Type = PokemonType.Dark,
            Name = "Shadow Elements",
            Primary = CreateColor(60, 60, 60),       // Dark grey
            Secondary = CreateColor(40, 40, 50),     // Dark blue-grey
            Accent = CreateColor(140, 100, 120),     // Dark purple accent
            Dark = CreateColor(20, 20, 20),          // Near black
            Light = CreateColor(120, 120, 120),      // Medium grey
            Neutral = CreateColor(80, 80, 90)        // Dark neutral
        };

        // FAIRY - Soft pinks with magical colors
        palettes[PokemonType.Fairy] = new TypeColorPalette
        {
            Type = PokemonType.Fairy,
            Name = "Magical Pink",
            Primary = CreateColor(255, 160, 220),    // Fairy pink
            Secondary = CreateColor(220, 120, 180),  // Rose pink
            Accent = CreateColor(255, 255, 255),     // Magical white
            Dark = CreateColor(180, 80, 140),        // Deep pink
            Light = CreateColor(255, 220, 240),      // Light pink
            Neutral = CreateColor(240, 180, 210)     // Soft pink
        };

        return palettes;
    }

    /// <summary>
    /// Helper method to create a ColorInfo object from RGB values
    /// </summary>
    private static ColorInfo CreateColor(byte r, byte g, byte b)
    {
        var color = new ColorInfo(r, g, b);
        return color;
    }
}