using BDSP.TextureRecolorTool.Models;
using Serilog;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service responsible for loading and managing YAML color palette configuration
/// Provides fallback to hardcoded defaults when external config is unavailable
/// </summary>
public class ColorPaletteConfigurationService
{
    private readonly ILogger _logger;
    private readonly string _configurationPath;
    private Dictionary<PokemonType, TypeColorPalette>? _loadedPalettes;
    private bool _configurationLoadAttempted;

    public ColorPaletteConfigurationService(string? configPath = null)
    {
        _logger = Log.ForContext<ColorPaletteConfigurationService>();
        _configurationPath = configPath ?? "TypeColorPalettes.yaml";
        _configurationLoadAttempted = false;
    }

    /// <summary>
    /// Check if external YAML configuration was successfully loaded
    /// </summary>
    public bool IsExternalConfigurationLoaded => _loadedPalettes != null;

    /// <summary>
    /// Get the path to the configuration file being used
    /// </summary>
    public string ConfigurationPath => _configurationPath;

    /// <summary>
    /// Load raw YAML configuration including HSV data for HueShift algorithm
    /// Returns null if loading fails
    /// </summary>
    /// <returns>Raw YAML configuration or null if loading failed</returns>
    public async Task<PokemonTypeColorConfiguration?> LoadRawYamlConfigurationAsync()
    {
        try
        {
            if (!File.Exists(_configurationPath))
            {
                _logger.Debug("YAML configuration file not found at {Path} for raw configuration loading", _configurationPath);
                return null;
            }

            string yamlContent = await File.ReadAllTextAsync(_configurationPath);

            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                _logger.Warning("YAML configuration file is empty: {Path}", _configurationPath);
                return null;
            }

            var configuration = YamlConfigurationHelper.ParseConfiguration(yamlContent);
            return configuration;
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to load raw YAML configuration from {Path}", _configurationPath);
            return null;
        }
    }

    /// <summary>
    /// Load color palettes from YAML configuration file
    /// Returns null if loading fails, allowing fallback to hardcoded defaults
    /// </summary>
    /// <returns>Dictionary of loaded palettes or null if loading failed</returns>
    public async Task<Dictionary<PokemonType, TypeColorPalette>?> LoadColorPalettesAsync()
    {
        if (_configurationLoadAttempted)
        {
            return _loadedPalettes; // Return cached result (could be null)
        }

        _configurationLoadAttempted = true;

        try
        {
            if (!File.Exists(_configurationPath))
            {
                _logger.Information("YAML color configuration file not found at {Path}. Will use hardcoded defaults", _configurationPath);
                return null;
            }

            _logger.Information("Loading color palettes from YAML configuration: {Path}", _configurationPath);

            string yamlContent = await File.ReadAllTextAsync(_configurationPath);

            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                _logger.Warning("YAML configuration file is empty: {Path}. Using hardcoded defaults", _configurationPath);
                return null;
            }

            var configuration = YamlConfigurationHelper.ParseConfiguration(yamlContent);
            if (configuration == null)
            {
                _logger.Error("Failed to parse YAML configuration from {Path}. Invalid YAML syntax. Using hardcoded defaults", _configurationPath);
                return null;
            }

            var palettes = ConvertYamlConfigurationToPalettes(configuration);
            if (palettes == null || palettes.Count == 0)
            {
                _logger.Warning("No valid color palettes found in YAML configuration. Using hardcoded defaults");
                return null;
            }

            _loadedPalettes = palettes;
            _logger.Information("Successfully loaded {Count} type color palettes from YAML configuration", palettes.Count);

            // Log some details about what was loaded
            foreach (var palette in palettes.Values.Take(3)) // Log first 3 as examples
            {
                _logger.Debug("Loaded palette for {Type} (Primary: {Primary})",
                    palette.Type, palette.Primary.HexString);
            }

            return _loadedPalettes;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception occurred while loading YAML color configuration from {Path}. Using hardcoded defaults", _configurationPath);
            return null;
        }
    }

    /// <summary>
    /// Ensure the YAML configuration file exists, creating a default one if necessary
    /// This method can be called by any algorithm to ensure the configuration file is available
    /// </summary>
    /// <returns>True if file exists or was successfully created</returns>
    public async Task<bool> EnsureConfigurationFileExistsAsync()
    {
        if (File.Exists(_configurationPath))
        {
            _logger.Debug("YAML configuration file already exists at {Path}", _configurationPath);
            return true;
        }

        _logger.Information("YAML configuration file not found, creating default file at {Path}", _configurationPath);

        try
        {
            // Create hardcoded palettes to include in the YAML file
            var hardcodedPalettes = CreateHardcodedTypePalettes();
            bool created = await CreateDefaultConfigurationFileAsync(hardcodedPalettes);

            if (created)
            {
                _logger.Information("Successfully created default YAML configuration file");
                _logger.Information("You can now edit this file to customize Pokemon type colors");
            }
            else
            {
                _logger.Warning("Failed to create default YAML configuration file");
            }

            return created;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception occurred while creating default YAML configuration file");
            return false;
        }
    }

    /// <summary>
    /// Create a default YAML configuration file with all current hardcoded palettes
    /// This allows users to start with the existing colors and customize them
    /// </summary>
    /// <param name="hardcodedPalettes">Current hardcoded palettes to export</param>
    /// <returns>True if file was created successfully</returns>
    public async Task<bool> CreateDefaultConfigurationFileAsync(Dictionary<PokemonType, TypeColorPalette> hardcodedPalettes)
    {
        try
        {
            var configuration = ConvertPalettesToYamlConfiguration(hardcodedPalettes);

            // Add some helpful comments by generating the YAML manually with comments
            string yamlWithComments = GenerateCommentedYamlConfiguration(configuration);

            await File.WriteAllTextAsync(_configurationPath, yamlWithComments);

            _logger.Information("Created default YAML color configuration file: {Path}", _configurationPath);
            _logger.Information("Users can now edit this file to customize Pokemon type colors without recompiling");

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to create default YAML configuration file: {Path}", _configurationPath);
            return false;
        }
    }

    /// <summary>
    /// Convert YAML configuration to internal TypeColorPalette format
    /// </summary>
    private Dictionary<PokemonType, TypeColorPalette>? ConvertYamlConfigurationToPalettes(PokemonTypeColorConfiguration configuration)
    {
        var palettes = new Dictionary<PokemonType, TypeColorPalette>();
        int successCount = 0;
        int errorCount = 0;

        foreach (var kvp in configuration.PokemonTypes)
        {
            string typeName = kvp.Key;
            var yamlPalette = kvp.Value;

            // Try to parse the Pokemon type name
            if (!Enum.TryParse<PokemonType>(typeName, ignoreCase: true, out var pokemonType))
            {
                _logger.Warning("Unknown Pokemon type '{TypeName}' in YAML configuration, skipping", typeName);
                errorCount++;
                continue;
            }

            // Convert the YAML palette to internal format
            var palette = YamlConfigurationHelper.ConvertToTypeColorPalette(yamlPalette, pokemonType);
            if (palette == null)
            {
                _logger.Warning("Failed to convert color palette for type {Type}, invalid color values", pokemonType);
                errorCount++;
                continue;
            }

            palettes[pokemonType] = palette;
            successCount++;
        }

        _logger.Information("YAML conversion results: {Success} successful, {Errors} errors", successCount, errorCount);

        // Only return palettes if we got a reasonable number of valid ones
        if (successCount == 0)
        {
            _logger.Error("No valid type palettes found in YAML configuration");
            return null;
        }

        if (errorCount > successCount)
        {
            _logger.Warning("More errors ({Errors}) than successes ({Success}) in YAML parsing, configuration may be incomplete",
                errorCount, successCount);
        }

        return palettes;
    }

    /// <summary>
    /// Convert internal palettes to YAML configuration format
    /// Now includes HSV data from TypeColorMappingService for HueShift algorithm
    /// </summary>
    private PokemonTypeColorConfiguration ConvertPalettesToYamlConfiguration(Dictionary<PokemonType, TypeColorPalette> palettes)
    {
        var configuration = new PokemonTypeColorConfiguration
        {
            ConfigInfo = new ConfigurationInfo
            {
                Version = "1.0",
                Description = "Pokemon Type Color Palette Configuration for BDSP Texture Recolor Tool",
                LastModified = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            }
        };

        // Get HSV data from TypeColorMappingService
        var typeColorService = new BDSP.TextureRecolorTool.Services.TypeColorMappingService();
        var hsvMappings = typeColorService.GetAllTypeMappings();

        foreach (var palette in palettes.Values)
        {
            string typeName = palette.Type.ToString().ToLowerInvariant();

            // Get corresponding HSV color for this type
            TypeColorInfo? hsvColor = null;
            if (hsvMappings.TryGetValue(palette.Type, out var typeColorInfo))
            {
                hsvColor = typeColorInfo;
            }

            configuration.PokemonTypes[typeName] = YamlConfigurationHelper.ConvertToYamlPalette(palette, hsvColor);
        }

        return configuration;
    }

    /// <summary>
    /// Generate YAML with helpful comments for user guidance
    /// </summary>
    private string GenerateCommentedYamlConfiguration(PokemonTypeColorConfiguration configuration)
    {
        var yaml = new List<string>
        {
            "# Pokemon Type Color Palette Configuration",
            "# Edit these colors to customize the appearance of Pokemon textures",
            "#",
            "# Each Pokemon type has 6 color categories for the ColorReplacement algorithm.",
            "# Each category is assigned based on how often the color it's replacing appears in the texture.",
            "#",
            $"config_info:",
            $"  version: \"{configuration.ConfigInfo?.Version}\"",
            $"  description: \"{configuration.ConfigInfo?.Description}\"",
            $"  last_modified: \"{configuration.ConfigInfo?.LastModified}\"",
            "pokemon_types:"
        };

        // Add each type with helpful comments
        var typeDescriptions = new Dictionary<string, string>
        {
            ["normal"] = "Natural, earthy tones",
            ["fire"] = "Reds, oranges, and flames",
            ["water"] = "Ocean blues and aqua",
            ["grass"] = "Rich greens and nature",
            ["electric"] = "Bright yellows and energy",
            ["psychic"] = "Mystical purples and pinks",
            ["ice"] = "Icy blues and crystalline whites",
            ["dragon"] = "Mystical purples with royal colors",
            ["dark"] = "Deep blacks and shadow colors",
            ["fighting"] = "Earthy oranges and browns",
            ["poison"] = "Toxic purples and sickly greens",
            ["ground"] = "Earth tones with clay and sand",
            ["flying"] = "Sky blues with cloud whites",
            ["bug"] = "Forest greens with nature accents",
            ["rock"] = "Stone greys with mineral colors",
            ["ghost"] = "Dark purples with ethereal blues",
            ["steel"] = "Metallic silvers with chrome",
            ["fairy"] = "Soft pinks with magical colors"
        };

        foreach (var kvp in configuration.PokemonTypes.OrderBy(x => x.Key))
        {
            string typeName = kvp.Key;
            var palette = kvp.Value;

            yaml.Add($"  {typeName}:");
            yaml.Add($"    primary: \"{palette.Primary}\"");
            yaml.Add($"    secondary: \"{palette.Secondary}\"");
            yaml.Add($"    accent: \"{palette.Accent}\"");
            yaml.Add($"    dark: \"{palette.Dark}\"");
            yaml.Add($"    light: \"{palette.Light}\"");
            yaml.Add($"    neutral: \"{palette.Neutral}\"");

            // Add HSV data if available
            if (palette.HueShiftHsv != null)
            {
                yaml.Add($"    hue_shift_hsv:");
                yaml.Add($"      hue: {palette.HueShiftHsv.Hue.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture)}");
                yaml.Add($"      saturation: {palette.HueShiftHsv.Saturation.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture)}");
                yaml.Add($"      value: {palette.HueShiftHsv.Value.ToString("0.0##", System.Globalization.CultureInfo.InvariantCulture)}");
            }
        }

        return string.Join(Environment.NewLine, yaml);
    }

    /// <summary>
    /// Validate that a YAML configuration file has the correct structure
    /// </summary>
    /// <param name="filePath">Path to YAML file to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public async Task<bool> ValidateConfigurationFileAsync(string? filePath = null)
    {
        string pathToCheck = filePath ?? _configurationPath;

        try
        {
            if (!File.Exists(pathToCheck))
            {
                return false;
            }

            string content = await File.ReadAllTextAsync(pathToCheck);
            var config = YamlConfigurationHelper.ParseConfiguration(content);

            if (config == null)
            {
                return false;
            }

            // Check that we have at least some Pokemon types defined
            if (config.PokemonTypes.Count == 0)
            {
                return false;
            }

            // Validate that we can convert at least one palette successfully
            foreach (var kvp in config.PokemonTypes.Take(1))
            {
                if (Enum.TryParse<PokemonType>(kvp.Key, ignoreCase: true, out var type))
                {
                    var palette = YamlConfigurationHelper.ConvertToTypeColorPalette(kvp.Value, type);
                    if (palette != null)
                    {
                        return true; // At least one valid palette found
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Create complete hardcoded type palettes for all 18 Pokemon types
    /// This generates the same palettes as in the reference TypeColorPalettes.yaml file
    /// </summary>
    private Dictionary<PokemonType, TypeColorPalette> CreateHardcodedTypePalettes()
    {
        var palettes = new Dictionary<PokemonType, TypeColorPalette>();

        // NORMAL TYPE - Natural, earthy tones
        palettes[PokemonType.Normal] = new TypeColorPalette
        {
            Type = PokemonType.Normal,
            Name = "Normal",
            Primary = CreateColor(210, 180, 140),    // #D2B48C
            Secondary = CreateColor(160, 140, 120),  // #A08C78
            Accent = CreateColor(240, 220, 200),     // #F0DCC8
            Dark = CreateColor(90, 70, 50),          // #5A4632
            Light = CreateColor(250, 240, 230),      // #FAF0E6
            Neutral = CreateColor(180, 160, 140)     // #B4A08C
        };

        // FIRE TYPE - Reds, oranges, and flames
        palettes[PokemonType.Fire] = new TypeColorPalette
        {
            Type = PokemonType.Fire,
            Name = "Fire",
            Primary = CreateColor(220, 80, 40),      // #DC5028
            Secondary = CreateColor(255, 140, 60),   // #FF8C3C
            Accent = CreateColor(255, 220, 60),      // #FFDC3C
            Dark = CreateColor(140, 40, 20),         // #8C2814
            Light = CreateColor(255, 200, 120),      // #FFC878
            Neutral = CreateColor(200, 120, 80)      // #C87850
        };

        // WATER TYPE - Ocean blues and aqua
        palettes[PokemonType.Water] = new TypeColorPalette
        {
            Type = PokemonType.Water,
            Name = "Water",
            Primary = CreateColor(60, 120, 200),     // #3C78C8
            Secondary = CreateColor(40, 100, 160),   // #2864A0
            Accent = CreateColor(120, 220, 255),     // #78DCFF
            Dark = CreateColor(20, 60, 120),         // #143C78
            Light = CreateColor(160, 200, 255),      // #A0C8FF
            Neutral = CreateColor(100, 160, 200)     // #64A0C8
        };

        // GRASS TYPE - Rich greens with natural accents
        palettes[PokemonType.Grass] = new TypeColorPalette
        {
            Type = PokemonType.Grass,
            Name = "Grass",
            Primary = CreateColor(80, 160, 80),      // #50A050
            Secondary = CreateColor(60, 120, 60),    // #3C783C
            Accent = CreateColor(160, 240, 120),     // #A0F078
            Dark = CreateColor(40, 80, 40),          // #285028
            Light = CreateColor(140, 220, 140),      // #8CDC8C
            Neutral = CreateColor(100, 140, 100)     // #648C64
        };

        // ELECTRIC TYPE - Bright yellows with electric blues
        palettes[PokemonType.Electric] = new TypeColorPalette
        {
            Type = PokemonType.Electric,
            Name = "Electric",
            Primary = CreateColor(255, 220, 60),     // #FFDC3C
            Secondary = CreateColor(255, 180, 40),   // #FFB428
            Accent = CreateColor(120, 200, 255),     // #78C8FF
            Dark = CreateColor(180, 140, 20),        // #B48C14
            Light = CreateColor(255, 255, 200),      // #FFFFC8
            Neutral = CreateColor(220, 200, 100)     // #DCC864
        };

        // PSYCHIC TYPE - Magentas and mystical purples
        palettes[PokemonType.Psychic] = new TypeColorPalette
        {
            Type = PokemonType.Psychic,
            Name = "Psychic",
            Primary = CreateColor(220, 80, 160),     // #DC50A0
            Secondary = CreateColor(180, 60, 140),   // #B43C8C
            Accent = CreateColor(140, 120, 255),     // #8C78FF
            Dark = CreateColor(120, 40, 80),         // #782850
            Light = CreateColor(255, 160, 220),      // #FFA0DC
            Neutral = CreateColor(200, 120, 180)     // #C878B4
        };

        // ICE TYPE - Cool blues and crystalline whites
        palettes[PokemonType.Ice] = new TypeColorPalette
        {
            Type = PokemonType.Ice,
            Name = "Ice",
            Primary = CreateColor(140, 220, 255),    // #8CDCFF
            Secondary = CreateColor(100, 180, 220),  // #64B4DC
            Accent = CreateColor(255, 255, 255),     // #FFFFFF
            Dark = CreateColor(60, 140, 180),        // #3C8CB4
            Light = CreateColor(220, 240, 255),      // #DCF0FF
            Neutral = CreateColor(180, 220, 240)     // #B4DCF0
        };

        // DRAGON TYPE - Royal purples with golden accents
        palettes[PokemonType.Dragon] = new TypeColorPalette
        {
            Type = PokemonType.Dragon,
            Name = "Dragon",
            Primary = CreateColor(100, 60, 160),     // #643CA0
            Secondary = CreateColor(60, 80, 140),    // #3C508C
            Accent = CreateColor(255, 200, 60),      // #FFC83C
            Dark = CreateColor(40, 20, 80),          // #281450
            Light = CreateColor(160, 120, 220),      // #A078DC
            Neutral = CreateColor(120, 100, 160)     // #7864A0
        };

        // DARK TYPE - Deep blacks with shadow colors
        palettes[PokemonType.Dark] = new TypeColorPalette
        {
            Type = PokemonType.Dark,
            Name = "Dark",
            Primary = CreateColor(60, 60, 60),       // #3C3C3C
            Secondary = CreateColor(40, 40, 50),     // #282832
            Accent = CreateColor(140, 100, 120),     // #8C6478
            Dark = CreateColor(20, 20, 20),          // #141414
            Light = CreateColor(120, 120, 120),      // #787878
            Neutral = CreateColor(80, 80, 90)        // #50505A
        };

        // FIGHTING TYPE - Strong browns and combat reds
        palettes[PokemonType.Fighting] = new TypeColorPalette
        {
            Type = PokemonType.Fighting,
            Name = "Fighting",
            Primary = CreateColor(180, 100, 60),     // #B4643C
            Secondary = CreateColor(140, 80, 40),    // #8C5028
            Accent = CreateColor(220, 60, 40),       // #DC3C28
            Dark = CreateColor(80, 40, 20),          // #502814
            Light = CreateColor(240, 180, 120),      // #F0B478
            Neutral = CreateColor(160, 120, 80)      // #A07850
        };

        // POISON TYPE - Rich purples with toxic greens
        palettes[PokemonType.Poison] = new TypeColorPalette
        {
            Type = PokemonType.Poison,
            Name = "Poison",
            Primary = CreateColor(160, 80, 200),     // #A050C8
            Secondary = CreateColor(120, 60, 140),   // #783C8C
            Accent = CreateColor(160, 255, 80),      // #A0FF50
            Dark = CreateColor(80, 40, 100),         // #502864
            Light = CreateColor(220, 180, 255),      // #DCB4FF
            Neutral = CreateColor(140, 120, 160)     // #8C78A0
        };

        // GROUND TYPE - Earth tones with clay and sand colors
        palettes[PokemonType.Ground] = new TypeColorPalette
        {
            Type = PokemonType.Ground,
            Name = "Ground",
            Primary = CreateColor(140, 100, 60),     // #8C643C
            Secondary = CreateColor(200, 160, 100),  // #C8A064
            Accent = CreateColor(80, 200, 120),      // #50C878
            Dark = CreateColor(60, 40, 20),          // #3C2814
            Light = CreateColor(220, 200, 160),      // #DCC8A0
            Neutral = CreateColor(160, 140, 100)     // #A08C64
        };

        // FLYING TYPE - Sky blues with cloud whites
        palettes[PokemonType.Flying] = new TypeColorPalette
        {
            Type = PokemonType.Flying,
            Name = "Flying",
            Primary = CreateColor(120, 180, 240),    // #78B4F0
            Secondary = CreateColor(200, 220, 240),  // #C8DCF0
            Accent = CreateColor(255, 255, 255),     // #FFFFFF
            Dark = CreateColor(60, 100, 160),        // #3C64A0
            Light = CreateColor(230, 240, 255),      // #E6F0FF
            Neutral = CreateColor(180, 200, 220)     // #B4C8DC
        };

        // BUG TYPE - Natural greens with vibrant accents
        palettes[PokemonType.Bug] = new TypeColorPalette
        {
            Type = PokemonType.Bug,
            Name = "Bug",
            Primary = CreateColor(100, 160, 60),     // #64A03C
            Secondary = CreateColor(60, 120, 40),    // #3C7828
            Accent = CreateColor(200, 240, 80),      // #C8F050
            Dark = CreateColor(40, 80, 20),          // #285014
            Light = CreateColor(160, 220, 120),      // #A0DC78
            Neutral = CreateColor(120, 140, 100)     // #788C64
        };

        // ROCK TYPE - Stone greys with mineral colors
        palettes[PokemonType.Rock] = new TypeColorPalette
        {
            Type = PokemonType.Rock,
            Name = "Rock",
            Primary = CreateColor(140, 130, 120),    // #8C8278
            Secondary = CreateColor(100, 90, 80),    // #645A50
            Accent = CreateColor(200, 180, 140),     // #C8B48C
            Dark = CreateColor(60, 55, 50),          // #3C3732
            Light = CreateColor(200, 190, 180),      // #C8BEB4
            Neutral = CreateColor(120, 115, 110)     // #78736E
        };

        // GHOST TYPE - Dark purples with ethereal blues
        palettes[PokemonType.Ghost] = new TypeColorPalette
        {
            Type = PokemonType.Ghost,
            Name = "Ghost",
            Primary = CreateColor(80, 60, 120),      // #503C78
            Secondary = CreateColor(60, 80, 140),    // #3C508C
            Accent = CreateColor(200, 180, 255),     // #C8B4FF
            Dark = CreateColor(40, 30, 60),          // #281E3C
            Light = CreateColor(160, 140, 200),      // #A08CC8
            Neutral = CreateColor(100, 90, 130)      // #645A82
        };

        // STEEL TYPE - Metallic silvers with chrome highlights
        palettes[PokemonType.Steel] = new TypeColorPalette
        {
            Type = PokemonType.Steel,
            Name = "Steel",
            Primary = CreateColor(180, 180, 200),    // #B4B4C8
            Secondary = CreateColor(140, 140, 160),  // #8C8CA0
            Accent = CreateColor(240, 240, 255),     // #F0F0FF
            Dark = CreateColor(80, 80, 100),         // #505064
            Light = CreateColor(220, 220, 240),      // #DCDCF0
            Neutral = CreateColor(160, 160, 180)     // #A0A0B4
        };

        // FAIRY TYPE - Soft pinks with magical colors
        palettes[PokemonType.Fairy] = new TypeColorPalette
        {
            Type = PokemonType.Fairy,
            Name = "Fairy",
            Primary = CreateColor(255, 160, 220),    // #FFA0DC
            Secondary = CreateColor(220, 120, 180),  // #DC78B4
            Accent = CreateColor(255, 255, 255),     // #FFFFFF
            Dark = CreateColor(180, 80, 140),        // #B4508C
            Light = CreateColor(255, 220, 240),      // #FFDCF0
            Neutral = CreateColor(240, 180, 210)     // #F0B4D2
        };

        return palettes;
    }    /// <summary>
         /// Helper method to create a ColorInfo object from RGB values
         /// Matches the pattern used in TypeColorPaletteService
         /// </summary>
    private static ColorInfo CreateColor(byte r, byte g, byte b)
    {
        return new ColorInfo(r, g, b);
    }
}