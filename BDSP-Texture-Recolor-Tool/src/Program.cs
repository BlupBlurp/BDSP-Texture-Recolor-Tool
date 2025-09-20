using BDSP.TextureRecolorTool.Core;
using BDSP.TextureRecolorTool.Models;
using CommandLine;
using Serilog;

namespace BDSP.TextureRecolorTool;

/// <summary>
/// Main program entry point for the BDSP Texture Recolor Tool
/// </summary>
public class Program
{
    /// <summary>
    /// Command line options for the application
    /// </summary>
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Input directory containing Pokemon bundles")]
        public string InputPath { get; set; } = string.Empty;

        [Option('o', "output", Required = false, HelpText = "Output directory for resulting bundles (Process/Export/Import operations)")]
        public string OutputPath { get; set; } = string.Empty;

        [Option("operation", Required = false, Default = "Process", HelpText = "Operation mode: Process, Export, or Import")]
        public string Operation { get; set; } = "Process";

        [Option('s', "seed", Required = false, HelpText = "Random seed for reproducible results")]
        public int? Seed { get; set; }

        [Option('m', "max-bundles", Required = false, HelpText = "Maximum number of bundles to process (for testing)")]
        public int? MaxBundles { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Enable verbose logging")]
        public bool Verbose { get; set; }

        [Option('l', "log-file", Required = false, HelpText = "Log file path (defaults to bdsp_texture_recolor_tool.log)")]
        public string? LogFile { get; set; }

        [Option("mode", Required = false, Default = "TypeBased", HelpText = "Randomization mode: Random or TypeBased")]
        public string Mode { get; set; } = "TypeBased";

        [Option("algorithm", Required = false, Default = "ColorReplacement", HelpText = "Color algorithm for TypeBased mode: HueShift or ColorReplacement")]
        public string Algorithm { get; set; } = "ColorReplacement";

        [Option('f', "compression-format", Required = false, Default = "RGBA32", HelpText = "Texture compression format for reinsertion: RGBA32 (uncompressed, default) or BC7 (high quality compression)")]
        public string CompressionFormat { get; set; } = "RGBA32";

        [Option('d', "data-path", Required = false, HelpText = "Path to PersonalTable.json for type-based coloring (required for TypeBased mode)")]
        public string? PokemonDataPath { get; set; }

        [Option("textures-path", Required = false, HelpText = "Path to directory containing PNG textures (for Import operations)")]
        public string? TexturesPath { get; set; }
    }

    /// <summary>
    /// Main entry point
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>Exit code (0 = success, 1 = error)</returns>
    public static async Task<int> Main(string[] args)
    {
        return await Parser.Default.ParseArguments<Options>(args)
            .MapResult(async options => await RunRandomizer(options), HandleParseError);
    }

    /// <summary>
    /// Run the texture randomizer with provided options
    /// </summary>
    /// <param name="options">Parsed command line options</param>
    /// <returns>Exit code</returns>
    private static async Task<int> RunRandomizer(Options options)
    {
        // Configure logging
        ConfigureLogging(options);

        try
        {
            Log.Information("BDSP Texture Recolor Tool v1.2.0");
            Log.Information("==================================");
            Log.Information("Input Path: {InputPath}", options.InputPath);
            Log.Information("Output Path: {OutputPath}", options.OutputPath);

            if (options.Seed.HasValue)
                Log.Information("Seed: {Seed}", options.Seed.Value);

            // Validate input paths
            if (!Directory.Exists(options.InputPath))
            {
                Log.Error("Input directory does not exist: {InputPath}", options.InputPath);
                return 1;
            }

            // Parse and validate operation mode
            if (!Enum.TryParse<OperationMode>(options.Operation, true, out var operation))
            {
                Log.Error("Invalid operation: {Operation}. Valid operations are: Process, Export, Import", options.Operation);
                return 1;
            }

            // Validate output path for all operations
            if (string.IsNullOrEmpty(options.OutputPath))
            {
                Log.Error("--output parameter is required for all operations");
                return 1;
            }

            // Create output directory if it doesn't exist
            Directory.CreateDirectory(options.OutputPath);

            // Parse and validate mode
            if (!Enum.TryParse<RandomizationMode>(options.Mode, true, out var mode))
            {
                Log.Error("Invalid mode: {Mode}. Valid modes are: Random, TypeBased", options.Mode);
                return 1;
            }

            // Parse and validate algorithm
            if (!Enum.TryParse<ColorAlgorithm>(options.Algorithm, true, out var algorithm))
            {
                Log.Error("Invalid algorithm: {Algorithm}. Valid algorithms are: HueShift, ColorReplacement", options.Algorithm);
                return 1;
            }

            // Parse and validate compression format
            if (!Enum.TryParse<TextureCompressionFormat>(options.CompressionFormat, true, out var compressionFormat))
            {
                Log.Error("Invalid compression format: {CompressionFormat}. Valid formats are: RGBA32, BC7", options.CompressionFormat);
                return 1;
            }

            // Validate Import operation specific parameters
            string texturesPath = string.Empty;

            if (operation == OperationMode.Import)
            {
                // For Import operations, we need textures path
                if (string.IsNullOrEmpty(options.TexturesPath))
                {
                    Log.Error("--textures-path parameter is required for Import operations");
                    return 1;
                }

                texturesPath = options.TexturesPath;

                if (!Directory.Exists(texturesPath))
                {
                    Log.Error("Textures directory does not exist: {TexturesPath}", texturesPath);
                    return 1;
                }

                Log.Information("Textures Path: {TexturesPath}", texturesPath);
            }

            // Validate Pokemon data path for type-based mode (only needed for Process operations)
            string pokemonDataPath = string.Empty;
            if (mode == RandomizationMode.TypeBased && operation == OperationMode.Process)
            {
                if (string.IsNullOrEmpty(options.PokemonDataPath))
                {
                    // Try default path relative to the executable
                    var defaultDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "PokemonData", "PersonalTable.json");
                    if (File.Exists(defaultDataPath))
                    {
                        pokemonDataPath = Path.GetFullPath(defaultDataPath);
                        Log.Information("Using default Pokemon data path: {DataPath}", pokemonDataPath);
                    }
                    else
                    {
                        Log.Error("Pokemon data path is required for TypeBased mode. Specify --data-path or place PersonalTable.json at: {DefaultPath}", defaultDataPath);
                        return 1;
                    }
                }
                else
                {
                    pokemonDataPath = options.PokemonDataPath;
                    if (!File.Exists(pokemonDataPath))
                    {
                        Log.Error("Pokemon data file does not exist: {DataPath}", pokemonDataPath);
                        return 1;
                    }
                }
            }
            else if (mode == RandomizationMode.TypeBased && (operation == OperationMode.Export || operation == OperationMode.Import))
            {
                // For Export/Import operations with TypeBased mode, Pokemon data is not needed
                // Set to provided path if available, or empty string (will be handled in constructor)
                pokemonDataPath = options.PokemonDataPath ?? string.Empty;
            }

            Log.Information("Operation: {Operation}", operation);
            Log.Information("Mode: {Mode}", mode);
            Log.Information("Algorithm: {Algorithm}", algorithm);
            Log.Information("Compression Format: {CompressionFormat}", compressionFormat);
            if (mode == RandomizationMode.TypeBased && operation == OperationMode.Process)
            {
                Log.Information("Pokemon Data Path: {DataPath}", pokemonDataPath);
            }

            // Create configuration
            var config = new RandomizerOptions
            {
                InputPath = options.InputPath,
                OutputPath = options.OutputPath,
                Seed = options.Seed,
                MaxBundles = options.MaxBundles,
                Operation = operation,
                Mode = mode,
                Algorithm = algorithm,
                CompressionFormat = compressionFormat,
                PokemonDataPath = pokemonDataPath,
                TexturesPath = texturesPath
            };

            // Run the randomizer
            var randomizer = new BDSPTextureRandomizer(config);
            var results = await randomizer.ProcessAllBundlesAsync();

            // Report results
            Log.Information("Processing completed!");
            Log.Information("Results: {Results}", results);

            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "An unhandled exception occurred");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Configure Serilog logging based on options
    /// </summary>
    /// <param name="options">Command line options</param>
    private static void ConfigureLogging(Options options)
    {
        var logConfig = new LoggerConfiguration();

        if (options.Verbose)
            logConfig.MinimumLevel.Debug();
        else
            logConfig.MinimumLevel.Information();

        // Console logging
        logConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        // File logging
        var logFile = options.LogFile ?? "bdsp_texture_recolor_tool.log";
        logConfig.WriteTo.File(
            logFile,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1));

        Log.Logger = logConfig.CreateLogger();
    }

    /// <summary>
    /// Handle command line parsing errors
    /// </summary>
    /// <param name="errors">Parsing errors</param>
    /// <returns>Error exit code</returns>
    private static Task<int> HandleParseError(IEnumerable<Error> errors)
    {
        return Task.FromResult(1);
    }
}