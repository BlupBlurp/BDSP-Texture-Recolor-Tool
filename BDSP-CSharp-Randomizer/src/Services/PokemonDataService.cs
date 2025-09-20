using System.Text.Json;
using BDSP.CSharp.Randomizer.Models;
using Serilog;

namespace BDSP.CSharp.Randomizer.Services;

/// <summary>
/// Service for loading and querying Pokemon data from PersonalTable.json
/// </summary>
public class PokemonDataService
{
    private readonly ILogger _logger;
    private readonly Dictionary<int, PokemonData> _pokemonByMonsno;
    private readonly string _dataPath;

    public PokemonDataService(string dataPath)
    {
        _logger = Log.ForContext<PokemonDataService>();
        _dataPath = dataPath ?? throw new ArgumentNullException(nameof(dataPath));
        _pokemonByMonsno = new Dictionary<int, PokemonData>();

        LoadPokemonData();
    }

    /// <summary>
    /// Load Pokemon data from PersonalTable.json
    /// </summary>
    private void LoadPokemonData()
    {
        try
        {
            _logger.Information("Loading Pokemon data from: {DataPath}", _dataPath);

            if (!File.Exists(_dataPath))
            {
                throw new FileNotFoundException($"PersonalTable.json not found at: {_dataPath}");
            }

            var jsonContent = File.ReadAllText(_dataPath);
            var personalTable = JsonSerializer.Deserialize<PersonalTable>(jsonContent);

            if (personalTable?.Personal == null)
            {
                throw new InvalidDataException("Invalid PersonalTable.json format");
            }

            // Index Pokemon by monsno for fast lookup
            foreach (var pokemon in personalTable.Personal)
            {
                if (pokemon.monsno > 0) // Skip entries with monsno 0 (invalid/placeholder entries)
                {
                    // Use monsno as the key, handling multiple forms
                    // For now, we'll use the first form encountered for each Pokemon
                    if (!_pokemonByMonsno.ContainsKey(pokemon.monsno))
                    {
                        _pokemonByMonsno[pokemon.monsno] = pokemon;
                    }
                }
            }

            _logger.Information("Loaded data for {Count} Pokemon", _pokemonByMonsno.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load Pokemon data from {DataPath}", _dataPath);
            throw;
        }
    }

    /// <summary>
    /// Get Pokemon data by Pokemon number (monsno)
    /// </summary>
    /// <param name="monsno">Pokemon number (e.g., 1 for Bulbasaur)</param>
    /// <returns>Pokemon data or null if not found</returns>
    public PokemonData? GetPokemonByMonsno(int monsno)
    {
        return _pokemonByMonsno.TryGetValue(monsno, out var pokemon) ? pokemon : null;
    }

    /// <summary>
    /// Get the primary type (type1) of a Pokemon
    /// </summary>
    /// <param name="monsno">Pokemon number</param>
    /// <returns>Type1 value (0-17) or null if Pokemon not found</returns>
    public int? GetPokemonType1(int monsno)
    {
        var pokemon = GetPokemonByMonsno(monsno);
        return pokemon?.type1;
    }

    /// <summary>
    /// Extract Pokemon number from bundle filename
    /// </summary>
    /// <param name="bundleFileName">Bundle filename (e.g., "pm0001_00_00")</param>
    /// <returns>Pokemon number or null if pattern doesn't match</returns>
    public static int? ExtractPokemonNumberFromBundle(string bundleFileName)
    {
        // Pattern: pm####_##_##
        if (bundleFileName.StartsWith("pm") && bundleFileName.Length >= 6)
        {
            var numberPart = bundleFileName.Substring(2, 4);
            if (int.TryParse(numberPart, out int pokemonNumber))
            {
                return pokemonNumber;
            }
        }
        return null;
    }

    /// <summary>
    /// Get total number of loaded Pokemon
    /// </summary>
    public int Count => _pokemonByMonsno.Count;
}