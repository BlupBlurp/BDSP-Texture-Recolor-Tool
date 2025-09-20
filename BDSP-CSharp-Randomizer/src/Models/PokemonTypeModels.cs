using System.Text.Json;

namespace BDSP.CSharp.Randomizer.Models;

/// <summary>
/// Pokemon data entry from PersonalTable.json
/// </summary>
public class PokemonData
{
    public int id { get; set; }
    public int monsno { get; set; }
    public int form_index { get; set; }
    public int type1 { get; set; }
    public int type2 { get; set; }
    // Add other properties as needed
}

/// <summary>
/// Root object for PersonalTable.json
/// </summary>
public class PersonalTable
{
    public List<PokemonData> Personal { get; set; } = new();
}

/// <summary>
/// Pokemon type definitions
/// </summary>
public enum PokemonType
{
    Normal = 0,     // White
    Fighting = 1,   // Orange
    Flying = 2,     // Sky Blue
    Poison = 3,     // Purple
    Ground = 4,     // Brown
    Rock = 5,       // Olive Green
    Bug = 6,        // Lime Green
    Ghost = 7,      // Indigo
    Steel = 8,      // Silver Gray
    Fire = 9,       // Red
    Water = 10,     // Deep Blue
    Grass = 11,     // Forest Green
    Electric = 12,  // Yellow
    Psychic = 13,   // Magenta Pink
    Ice = 14,       // Cyan
    Dragon = 15,    // Navy Blue
    Dark = 16,      // Black
    Fairy = 17      // Light Pink
}

/// <summary>
/// Color information for a Pokemon type
/// </summary>
public class TypeColorInfo
{
    public PokemonType Type { get; set; }
    public string Name { get; set; } = "";
    public float Hue { get; set; }        // HSV Hue (0-1)
    public float Saturation { get; set; }  // HSV Saturation (0-1)
    public float Value { get; set; }       // HSV Value/Brightness (0-1)
}