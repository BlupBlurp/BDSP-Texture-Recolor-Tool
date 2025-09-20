# BDSP Texture Recolor Tool

A C# console application that replaces Pokémon BDSP texture colors based on randomized typings or completely random color schemes. It can also be used to batch export textures into .PNG images, and insert them back.

**Release Download:** [Nexus Mods](https://www.nexusmods.com/pokemonbdsp/mods/58)

> **Disclaimer:** This repository has been created with AI assistance.

## Features

- **Type-Based Recoloring:** Changes Pokémon colors to match their randomized types
- **Multiple Algorithms:** Choose between Color Replacement and Hue Shift methods
- **Random Mode:** Completely randomize colors regardless of typing
- **Export/Import System:** Export Pokémon textures as PNG images for manual editing, then import them back
- **Mod Compatibility:** Works with mods like Luminescent Platinum or Coronet Forms

## Requirements

- .NET 8.0 Runtime (automatically installed on modern Windows systems)
- A dump of BDSP romfs folder
- For randomized typing support: IO randomizer tool and BDSP Repacker or UABEA

### Building Requirements

If building from source:

- .NET 8.0 SDK
- PowerShell (for using the build script)
- Windows x64 (current build targets)

## Setup Instructions

1. **Extract the tool** to your desired directory
2. **Create a "PokemonBundles" folder** and place your Pokémon bundle files there

   - **Location:** `\romfs\Data\StreamingAssets\AssetAssistant\Pokemon Database\pokemons\common`
   - **For mod users (e.g., Luminescent Platinum):**
     - First, copy files from vanilla BDSP to the PokemonBundles folder
     - Then copy the mod bundles from the same path to the same location, replace conflicting files
     - This ensures all Pokémon (including new ones added by mods) are recolored

3. **Optional - for randomized typing support:**
   - Use the IO randomizer with your desired settings, and export
   - Get the personal_masterdatas file from `\romfs\Data\StreamingAssets\AssetAssistant\Pml` in the Output folder IO created
   - Use BDSP Repacker or UABEA to extract PersonalTable.json from the personal_masterdatas file
   - Create a "PokemonData" folder in the program directory
   - Place PersonalTable.json in the PokemonData folder

## Usage

Run the program from the command line using `BDSP-Texture-Recolor-Tool.exe` with the following options:

### Command-Line Arguments

- `-i, --input`: Path to input PokemonBundles folder (Required)
- `-o, --output`: Path to output folder for processed bundles
- `--operation`: Choose between "Process", "Export", or "Import" (Default: Process)
- `-s, --seed`: Random seed for reproducible results
- `--mode`: Choose "TypeBased" for type-based recoloring or "Random" for random colors (Default: TypeBased)
- `--algorithm`: Choose "ColorReplacement" or "HueShift" (Default: ColorReplacement)
- `-d, --data-path`: Path to PersonalTable.json file (required for TypeBased mode)
- `--textures-path`: Path to directory containing PNG textures (for Import operations)
- `-v, --verbose`: Enable detailed console output

### Operation Modes

- **Process Mode (Default):** Directly processes and recolors Pokémon bundles
- **Export Mode:** Extracts textures from bundles and saves them as PNG images for manual editing
- **Import Mode:** Takes manually edited PNG textures and imports them back into bundle format

### Example Commands

**Type-based recoloring with Color Replacement:**

```bash
BDSP-Texture-Recolor-Tool.exe -i "PokemonBundles" -o "OutputBundles" --mode TypeBased --algorithm ColorReplacement -d "PokemonData/PersonalTable.json" --verbose
```

**Type-based recoloring with Hue Shift:**

```bash
BDSP-Texture-Recolor-Tool.exe -i "PokemonBundles" -o "OutputBundles" --mode TypeBased --algorithm HueShift -d "PokemonData/PersonalTable.json" --verbose
```

**Completely random recoloring:**

```bash
BDSP-Texture-Recolor-Tool.exe -i "PokemonBundles" -o "OutputBundles" --mode Random --verbose
```

**Export textures as PNG images for manual editing:**

```bash
BDSP-Texture-Recolor-Tool.exe -i "PokemonBundles" -o "ExportedTextures" --operation Export --verbose
```

**Import manually edited PNG textures back to bundles:**

```bash
BDSP-Texture-Recolor-Tool.exe -i "PokemonBundles" -o "OutputBundles" --operation Import --textures-path "EditedTextures" --verbose
```

## Installation of Recolored Bundles

1. After running the tool, copy the generated files from the output folder back to your BDSP mod folder
2. Replace the original files in `\romfs\Data\StreamingAssets\AssetAssistant\Pokemon Database\pokemons\common`

## Known Issues & Future Improvements

- **Eye texture preservation:** Some Pokémon with white or gray body tones may have their eye colors affected. Planning to improve this by comparing normal and shiny eye textures to better preserve natural eye colors
- **Color representation accuracy:** Some type-based colors (particularly lighter types like Steel) may not be perfectly represented. Working on improving the color replacement algorithm
- **More customization:** I would like to add new arguments for the command for extra customization. For example, being able to choose the colors that represent each type. Or the compression format for the textures (files are currently very heavy using RGBA32)
- **Easier use:** The tool could directly ask for the path of the BDSP dump and mod folder, and automatically get all the necessary bundles and files, without the user having to manually copy and replace them
- **Clean up:** It should be possible to move the functionality of this tool to a script, so that it's not needed to have an exe with a lot of dependencies (and multiplatform support?). I originally tried to make this tool as a Python script using UnityPy, but I got stuck and I started all over in C# (I'm just inexperienced, there surely are better ways of doing this.)

## Building from Source

If you want to build the tool from source:

1. **Install .NET 8.0 SDK**
2. **Clone this repository**
3. **Run the build script:**

### Build Script Options

The `build-release.ps1` script supports several configuration options:

```powershell
# Build both release and debug versions (default)
.\build-release.ps1

# Build only the release version
.\build-release.ps1 -Configuration Release

# Build only the debug version
.\build-release.ps1 -Configuration Debug
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
