using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BDSP.TextureRecolorTool.Models;
using BDSP.TextureRecolorTool.Services;
using Serilog;
using ShellProgressBar;

namespace BDSP.TextureRecolorTool.Core;

/// <summary>
/// Main randomizer class that orchestrates the entire texture randomization process
/// </summary>
public class BDSPTextureRandomizer : IDisposable
{
    private readonly RandomizerOptions _options;
    private readonly FileFilter _fileFilter;
    private readonly ColorRandomizationService _colorService;
    private readonly TextureExtractionService _extractionService;
    private readonly TextureReinsertionService _reinsertionService;
    private readonly TextureCompressionService _compressionService;
    private readonly PokemonDataService? _pokemonDataService;
    private readonly TypeColorMappingService? _typeColorService;
    private readonly ColorPaletteConfigurationService? _configurationService;
    private readonly ColorAnalysisService? _colorAnalysisService;
    private readonly ColorReplacementService? _colorReplacementService;
    private readonly TypeColorPaletteService? _typeColorPaletteService;
    private readonly TextureExportService? _exportService;
    private readonly TextureImportService? _importService;
    private readonly AssetsManager _assetsManager;
    private readonly ILogger _logger;
    private readonly Random _random;
    private bool _disposed = false;

    public BDSPTextureRandomizer(RandomizerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = Log.ForContext<BDSPTextureRandomizer>();

        // Initialize random with seed if provided
        _random = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();
        _logger.Information("Initialized randomizer with seed: {Seed}",
            options.Seed?.ToString() ?? "random");

        // Validate paths
        ValidatePaths();

        // Initialize type-based services if needed (only for Process operations)
        if (_options.Mode == RandomizationMode.TypeBased && _options.Operation == OperationMode.Process)
        {
            if (string.IsNullOrEmpty(_options.PokemonDataPath))
            {
                throw new ArgumentException("PokemonDataPath is required for type-based randomization mode");
            }

            _pokemonDataService = new PokemonDataService(_options.PokemonDataPath);

            // Initialize YAML color palette configuration service
            var configPath = _options.ConfigPath ?? "TypeColorPalettes.yaml";
            _configurationService = new ColorPaletteConfigurationService(configPath);
            _logger.Information("Initialized YAML color palette configuration service with config path: {ConfigPath}", configPath);

            // Ensure YAML configuration file exists for both ColorReplacement and HueShift algorithms
            var ensureFileTask = _configurationService.EnsureConfigurationFileExistsAsync();
            ensureFileTask.Wait();
            bool fileExists = ensureFileTask.Result;

            if (fileExists)
            {
                _logger.Debug("YAML configuration file verified/created successfully");
            }
            else
            {
                _logger.Warning("Could not ensure YAML configuration file exists, will use hardcoded defaults");
            }

            // Initialize TypeColorMappingService with YAML configuration support
            _typeColorService = new TypeColorMappingService(_configurationService);

            // Initialize advanced color replacement services if requested
            if (_options.Algorithm == ColorAlgorithm.ColorReplacement)
            {
                _colorAnalysisService = new ColorAnalysisService();
                _colorReplacementService = new ColorReplacementService();
                _typeColorPaletteService = new TypeColorPaletteService(_configurationService);
                _logger.Information("Initialized advanced color replacement services with YAML configuration support");
            }

            _logger.Information("Initialized type-based randomization services with {Algorithm} algorithm", _options.Algorithm);
        }

        // Initialize core services
        _fileFilter = new FileFilter();

        // Initialize ColorRandomizationService with advanced services if available
        if (_colorAnalysisService != null && _colorReplacementService != null && _typeColorPaletteService != null)
        {
            _colorService = new ColorRandomizationService(_random, _colorAnalysisService, _colorReplacementService, _typeColorPaletteService);
        }
        else
        {
            _colorService = new ColorRandomizationService(_random);
        }

        _assetsManager = new AssetsManager();
        _extractionService = new TextureExtractionService(_assetsManager);
        _compressionService = new TextureCompressionService();
        _reinsertionService = new TextureReinsertionService(_assetsManager, _compressionService);

        // Initialize export/import services based on operation mode
        if (_options.Operation == OperationMode.Export)
        {
            _exportService = new TextureExportService(_assetsManager);
            _logger.Information("Initialized texture export service");
        }
        else if (_options.Operation == OperationMode.Import)
        {
            _importService = new TextureImportService(_assetsManager, _compressionService);
            _logger.Information("Initialized texture import service");
        }

        _logger.Information("BDSP Texture Randomizer initialized successfully in {Mode} mode with {Operation} operation", _options.Mode, _options.Operation);
    }

    /// <summary>
    /// Process all Pokemon bundles in the input directory based on operation mode
    /// </summary>
    /// <returns>Processing statistics</returns>
    public async Task<ProcessingStatistics> ProcessAllBundlesAsync()
    {
        return _options.Operation switch
        {
            OperationMode.Export => await ExportAllBundlesAsync(),
            OperationMode.Import => await ImportAllBundlesAsync(),
            OperationMode.Process => await ProcessAllBundlesInternalAsync(),
            _ => throw new InvalidOperationException($"Unsupported operation mode: {_options.Operation}")
        };
    }

    /// <summary>
    /// Export all textures from bundles to PNG files
    /// </summary>
    /// <returns>Processing statistics</returns>
    public async Task<ProcessingStatistics> ExportAllBundlesAsync()
    {
        if (_exportService == null)
        {
            throw new InvalidOperationException("Export service not initialized");
        }

        _logger.Information("Starting batch export of Pokemon bundle textures");
        _logger.Information("Input directory: {InputPath}", _options.InputPath);
        _logger.Information("Export directory: {OutputPath}", _options.OutputPath);

        var stats = new ProcessingStatistics();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Discover Pokemon bundles
            var bundlePaths = _fileFilter.FindPokemonBundles(_options.InputPath);
            _logger.Information("Found {Count} Pokemon bundles to export", bundlePaths.Count);

            if (bundlePaths.Count == 0)
            {
                _logger.Warning("No Pokemon bundles found in input directory");
                return stats;
            }

            // Apply batch limit if specified
            if (_options.MaxBundles.HasValue && _options.MaxBundles.Value > 0)
            {
                bundlePaths = bundlePaths.Take(_options.MaxBundles.Value).ToList();
                _logger.Information("Limited to first {Count} bundles for export", bundlePaths.Count);
            }

            // Ensure output directory exists
            Directory.CreateDirectory(_options.OutputPath);

            // Process bundles with progress bar
            var progressOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGreen,
                ProgressCharacter = '█',
                ShowEstimatedDuration = true
            };

            using var progressBar = new ProgressBar(bundlePaths.Count, "Exporting Pokemon textures...", progressOptions);

            for (int i = 0; i < bundlePaths.Count; i++)
            {
                var bundlePath = bundlePaths[i];
                stats.BundlesProcessed++;

                try
                {
                    progressBar.Message = $"Exporting: {Path.GetFileName(bundlePath)}";

                    var exportResult = await _exportService.ExportBundleTexturesAsync(bundlePath, _options.OutputPath);

                    if (exportResult.Success)
                    {
                        stats.BundlesModified++;
                        stats.TexturesModified += exportResult.TexturesProcessed;
                    }
                    else
                    {
                        stats.ErrorsEncountered++;
                        _logger.Error("Failed to export bundle {Bundle}: {Error}",
                            Path.GetFileName(bundlePath), exportResult.Error);
                    }

                    // Progress reporting
                    if (stats.BundlesProcessed % 10 == 0)
                    {
                        _logger.Information("Export progress: {Processed}/{Total} bundles, {Textures} textures exported",
                            stats.BundlesProcessed, bundlePaths.Count, stats.TexturesModified);
                    }
                }
                catch (Exception ex)
                {
                    stats.ErrorsEncountered++;
                    _logger.Error(ex, "Unexpected error exporting bundle: {BundlePath}", bundlePath);
                }

                progressBar.Tick();
            }

            stopwatch.Stop();

            _logger.Information("Export completed in {Duration:F2} seconds", stopwatch.Elapsed.TotalSeconds);
            _logger.Information("Export summary: {Processed} bundles processed, {Modified} bundles exported, {Textures} textures exported, {Errors} errors",
                stats.BundlesProcessed, stats.BundlesModified, stats.TexturesModified, stats.ErrorsEncountered);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during batch export");
            throw;
        }
    }

    /// <summary>
    /// Import PNG textures back into bundles
    /// </summary>
    /// <returns>Processing statistics</returns>
    public async Task<ProcessingStatistics> ImportAllBundlesAsync()
    {
        if (_importService == null)
        {
            throw new InvalidOperationException("Import service not initialized");
        }

        _logger.Information("Starting batch import of PNG textures into Pokemon bundles");
        _logger.Information("Original bundles directory: {InputPath}", _options.InputPath);
        _logger.Information("Textures directory: {TexturesPath}", _options.TexturesPath);
        _logger.Information("Output directory: {OutputPath}", _options.OutputPath);

        var stats = new ProcessingStatistics();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Discover original Pokemon bundles
            var bundlePaths = _fileFilter.FindPokemonBundles(_options.InputPath);
            _logger.Information("Found {Count} Pokemon bundles for import", bundlePaths.Count);

            if (bundlePaths.Count == 0)
            {
                _logger.Warning("No Pokemon bundles found in input directory");
                return stats;
            }

            // Apply batch limit if specified
            if (_options.MaxBundles.HasValue && _options.MaxBundles.Value > 0)
            {
                bundlePaths = bundlePaths.Take(_options.MaxBundles.Value).ToList();
                _logger.Information("Limited to first {Count} bundles for import", bundlePaths.Count);
            }

            // Use the configured output directory
            var importOutputPath = _options.OutputPath;
            Directory.CreateDirectory(importOutputPath);

            // Process bundles with progress bar
            var progressOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                BackgroundColor = ConsoleColor.DarkYellow,
                ProgressCharacter = '█',
                ShowEstimatedDuration = true
            };

            using var progressBar = new ProgressBar(bundlePaths.Count, "Importing Pokemon textures...", progressOptions);

            for (int i = 0; i < bundlePaths.Count; i++)
            {
                var originalBundlePath = bundlePaths[i];
                var bundleName = Path.GetFileNameWithoutExtension(originalBundlePath);
                var texturesPath = Path.Combine(_options.TexturesPath, bundleName);
                var outputBundlePath = Path.Combine(importOutputPath, Path.GetFileName(originalBundlePath));

                stats.BundlesProcessed++;

                try
                {
                    progressBar.Message = $"Importing: {bundleName}";

                    // Check if texture directory exists
                    if (!Directory.Exists(texturesPath))
                    {
                        _logger.Warning("Texture directory not found for bundle {Bundle}, skipping", bundleName);
                        continue;
                    }

                    var importResult = await _importService.ImportBundleTexturesAsync(
                        originalBundlePath, texturesPath, outputBundlePath, _options.CompressionFormat);

                    if (importResult.Success)
                    {
                        stats.BundlesModified++;
                        stats.TexturesModified += importResult.TexturesModified;
                    }
                    else
                    {
                        stats.ErrorsEncountered++;
                        _logger.Error("Failed to import bundle {Bundle}: {Error}",
                            bundleName, importResult.Error);
                    }

                    // Progress reporting
                    if (stats.BundlesProcessed % 10 == 0)
                    {
                        _logger.Information("Import progress: {Processed}/{Total} bundles, {Textures} textures imported",
                            stats.BundlesProcessed, bundlePaths.Count, stats.TexturesModified);
                    }
                }
                catch (Exception ex)
                {
                    stats.ErrorsEncountered++;
                    _logger.Error(ex, "Unexpected error importing bundle: {BundlePath}", originalBundlePath);
                }

                progressBar.Tick();
            }

            stopwatch.Stop();

            _logger.Information("Import completed in {Duration:F2} seconds", stopwatch.Elapsed.TotalSeconds);
            _logger.Information("Import summary: {Processed} bundles processed, {Modified} bundles imported, {Textures} textures imported, {Errors} errors",
                stats.BundlesProcessed, stats.BundlesModified, stats.TexturesModified, stats.ErrorsEncountered);
            _logger.Information("Imported bundles saved to: {OutputPath}", importOutputPath);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during batch import");
            throw;
        }
    }

    /// <summary>
    /// Export default color palette configuration to YAML file
    /// This allows users to customize type colors without recompiling
    /// </summary>
    /// <param name="outputPath">Optional custom path for YAML file</param>
    /// <returns>True if export was successful</returns>
    public async Task<bool> ExportDefaultConfigurationAsync(string? outputPath = null)
    {
        if (_typeColorPaletteService == null)
        {
            _logger.Warning("TypeColorPaletteService not available - initializing temporary service for export");

            // Create a temporary service for exporting even if not in ColorReplacement mode
            var tempConfigService = new ColorPaletteConfigurationService(outputPath);
            var tempPaletteService = new TypeColorPaletteService();

            return await tempPaletteService.ExportDefaultConfigurationAsync(outputPath);
        }

        return await _typeColorPaletteService.ExportDefaultConfigurationAsync(outputPath);
    }

    /// <summary>
    /// Check if external YAML configuration is being used
    /// </summary>
    public bool IsUsingExternalConfiguration => _typeColorPaletteService?.IsUsingExternalConfiguration ?? false;

    /// <summary>
    /// Get information about the current color configuration source
    /// </summary>
    public string GetConfigurationInfo()
    {
        if (_configurationService == null)
        {
            return "No YAML configuration service (not in type-based mode)";
        }

        if (_configurationService.IsExternalConfigurationLoaded)
        {
            return $"Using external YAML configuration: {_configurationService.ConfigurationPath}";
        }
        else
        {
            return $"Using hardcoded defaults (YAML file not found: {_configurationService.ConfigurationPath})";
        }
    }

    /// <summary>
    /// Process all Pokemon bundles in the input directory (original behavior)
    /// </summary>
    /// <returns>Processing statistics</returns>
    private async Task<ProcessingStatistics> ProcessAllBundlesInternalAsync()
    {
        _logger.Information("Starting batch processing of Pokemon bundles");
        _logger.Information("Input directory: {InputPath}", _options.InputPath);
        _logger.Information("Output directory: {OutputPath}", _options.OutputPath);

        var stats = new ProcessingStatistics();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Discover Pokemon bundles
            var bundlePaths = _fileFilter.FindPokemonBundles(_options.InputPath);
            _logger.Information("Found {Count} Pokemon bundles to process", bundlePaths.Count);

            if (bundlePaths.Count == 0)
            {
                _logger.Warning("No Pokemon bundles found in input directory");
                return stats;
            }

            // Apply batch limit if specified
            if (_options.MaxBundles.HasValue && _options.MaxBundles.Value > 0)
            {
                bundlePaths = bundlePaths.Take(_options.MaxBundles.Value).ToList();
                _logger.Information("Limited to first {Count} bundles for processing", bundlePaths.Count);
            }

            // Ensure output directory exists
            Directory.CreateDirectory(_options.OutputPath);

            // Process bundles with progress bar
            var progressOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Cyan,
                BackgroundColor = ConsoleColor.DarkCyan,
                ProgressCharacter = '█',
                ShowEstimatedDuration = true
            };

            using var progressBar = new ProgressBar(bundlePaths.Count, "Processing Pokemon bundles...", progressOptions);

            for (int i = 0; i < bundlePaths.Count; i++)
            {
                var bundlePath = bundlePaths[i];
                stats.BundlesProcessed++;

                try
                {
                    progressBar.Message = $"Processing: {Path.GetFileName(bundlePath)}";

                    var bundleStats = await ProcessSingleBundleAsync(bundlePath);

                    // Update overall statistics
                    if (bundleStats.Success)
                    {
                        stats.BundlesModified++;
                    }
                    else if (bundleStats.TexturesModified == 0 && bundleStats.Errors == 0)
                    {
                        // Bundle was skipped due to no color textures
                        stats.BundlesSkipped++;
                    }
                    stats.TexturesModified += bundleStats.TexturesModified;
                    stats.ErrorsEncountered += bundleStats.Errors;

                    _logger.Debug("Bundle {Index}/{Total}: {FileName} - {TextureCount} textures modified",
                        i + 1, bundlePaths.Count, Path.GetFileName(bundlePath), bundleStats.TexturesModified);
                }
                catch (Exception ex)
                {
                    stats.ErrorsEncountered++;
                    _logger.Error(ex, "Error processing bundle: {BundlePath}", bundlePath);
                }
                finally
                {
                    progressBar.Tick();
                }

                // Report progress every 10 bundles
                if ((i + 1) % 10 == 0)
                {
                    var elapsed = stopwatch.Elapsed;
                    var rate = (i + 1) / elapsed.TotalMinutes;
                    _logger.Information("Progress: {Current}/{Total} bundles processed ({Rate:F1} bundles/min)",
                        i + 1, bundlePaths.Count, rate);
                }
            }

            stopwatch.Stop();

            // Log final statistics
            LogFinalStatistics(stats, stopwatch.Elapsed);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error during batch processing");
            throw;
        }
    }

    /// <summary>
    /// Process a single Pokemon bundle
    /// </summary>
    private async Task<BundleProcessingResult> ProcessSingleBundleAsync(string bundlePath)
    {
        var result = new BundleProcessingResult();
        BundleFileInstance? bundleFile = null;

        try
        {
            _logger.Debug("Processing bundle: {BundlePath}", bundlePath);

            // Load the bundle
            bundleFile = _assetsManager.LoadBundleFile(bundlePath);
            if (bundleFile?.file == null)
            {
                _logger.Warning("Failed to load bundle file: {BundlePath}", bundlePath);
                result.Errors++;
                return result;
            }

            // Generate consistent color parameters for this bundle
            var colorParams = GenerateColorParametersForBundle(bundlePath);
            if (colorParams == null)
            {
                _logger.Warning("Could not generate color parameters for bundle: {BundlePath}", bundlePath);
                result.Errors++;
                return result;
            }

            if (colorParams.IsTypeBased)
            {
                _logger.Debug("Generated type-based color parameters for bundle - Type: {Type}, H:{Hue:F3} S:{Saturation:F3} V:{Value:F3}",
                    colorParams.PokemonType, colorParams.HueShift, colorParams.SaturationVariation, colorParams.TargetValue);
            }
            else
            {
                _logger.Debug("Generated random color parameters for bundle - Hue shift: {HueShift:F3}, Saturation: {Saturation:F3}",
                    colorParams.HueShift, colorParams.SaturationVariation);
            }

            // Extract and process textures
            var modifiedTextures = await _extractionService.ExtractAndProcessTexturesAsync(
                bundleFile, colorParams, _colorService);

            if (modifiedTextures.Count == 0)
            {
                _logger.Debug("Skipping bundle (no color textures to process): {BundlePath}", bundlePath);

                // Don't create output file - bundle contains no color textures to modify
                result.Success = false; // Don't count as "modified" in statistics
                result.TexturesModified = 0;
                return result;
            }

            // Reinsert modified textures
            var outputPath = GetOutputPath(bundlePath);
            var reinsertedCount = await _reinsertionService.ReinsertTexturesAsync(
                bundleFile, modifiedTextures, outputPath, _options.CompressionFormat);

            result.TexturesModified = reinsertedCount;
            result.Success = reinsertedCount > 0;

            _logger.Debug("Bundle processing complete: {BundlePath} - {Count} textures modified",
                bundlePath, reinsertedCount);

            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing bundle: {BundlePath}", bundlePath);
            result.Errors++;
            return result;
        }
        finally
        {
            // Clean up bundle resources
            if (bundleFile != null)
            {
                bundleFile.file?.Close();
            }
        }
    }

    /// <summary>
    /// Get the output path for a bundle file
    /// </summary>
    private string GetOutputPath(string inputPath)
    {
        var fileName = Path.GetFileName(inputPath);
        return Path.Combine(_options.OutputPath, fileName);
    }

    /// <summary>
    /// Validate that input and output paths exist and are accessible
    /// </summary>
    private void ValidatePaths()
    {
        if (string.IsNullOrWhiteSpace(_options.InputPath))
        {
            throw new ArgumentException("Input path cannot be empty");
        }

        if (!Directory.Exists(_options.InputPath))
        {
            throw new DirectoryNotFoundException($"Input directory not found: {_options.InputPath}");
        }

        // Validate paths based on operation type
        if (_options.Operation == OperationMode.Import)
        {
            // For Import operations, validate textures path and output path
            if (string.IsNullOrWhiteSpace(_options.TexturesPath))
            {
                throw new ArgumentException("Textures path cannot be empty for Import operations");
            }

            if (!Directory.Exists(_options.TexturesPath))
            {
                throw new DirectoryNotFoundException($"Textures directory not found: {_options.TexturesPath}");
            }

            if (string.IsNullOrWhiteSpace(_options.OutputPath))
            {
                throw new ArgumentException("Output path cannot be empty for Import operations");
            }

            // Create output directory if it doesn't exist
            try
            {
                Directory.CreateDirectory(_options.OutputPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot create output directory: {_options.OutputPath}", ex);
            }

            _logger.Debug("Path validation successful - Input: {InputPath}, TexturesPath: {TexturesPath}, OutputPath: {OutputPath}",
                _options.InputPath, _options.TexturesPath, _options.OutputPath);
        }
        else
        {
            // For Process and Export operations, validate regular output path
            if (string.IsNullOrWhiteSpace(_options.OutputPath))
            {
                throw new ArgumentException("Output path cannot be empty");
            }

            // Create output directory if it doesn't exist
            try
            {
                Directory.CreateDirectory(_options.OutputPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot create output directory: {_options.OutputPath}", ex);
            }

            _logger.Debug("Path validation successful - Input: {InputPath}, Output: {OutputPath}",
                _options.InputPath, _options.OutputPath);
        }
    }

    /// <summary>
    /// Log final processing statistics
    /// </summary>
    /// <summary>
    /// Generate color parameters for a bundle based on the selected mode
    /// </summary>
    /// <param name="bundlePath">Path to the bundle file</param>
    /// <returns>Color parameters or null if generation failed</returns>
    private BundleColorParameters? GenerateColorParametersForBundle(string bundlePath)
    {
        if (_options.Mode == RandomizationMode.TypeBased)
        {
            // Extract Pokemon number from bundle filename
            var bundleFileName = Path.GetFileNameWithoutExtension(bundlePath);
            var pokemonNumber = PokemonDataService.ExtractPokemonNumberFromBundle(bundleFileName);

            if (!pokemonNumber.HasValue)
            {
                _logger.Warning("Could not extract Pokemon number from bundle filename: {FileName}", bundleFileName);
                return null;
            }

            // Get Pokemon type
            var pokemonType = _pokemonDataService?.GetPokemonType1(pokemonNumber.Value);
            if (!pokemonType.HasValue)
            {
                _logger.Warning("Could not find type data for Pokemon #{Number} (bundle: {FileName})",
                    pokemonNumber.Value, bundleFileName);
                return null;
            }

            // Generate type-based color parameters
            return _typeColorService?.GenerateTypeBasedColorParameters(pokemonType.Value, _options.Algorithm);
        }
        else
        {
            // Generate random color parameters (original behavior)
            return _colorService.GenerateBundleColorParameters();
        }
    }

    /// <summary>
    /// Log final processing statistics
    /// </summary>
    /// <param name="stats">Processing statistics</param>
    /// <param name="elapsed">Total elapsed time</param>
    private void LogFinalStatistics(ProcessingStatistics stats, TimeSpan elapsed)
    {
        _logger.Information("=== BDSP Texture Randomizer - Processing Complete ===");
        _logger.Information("Total processing time: {Elapsed}", elapsed.ToString(@"hh\:mm\:ss\.fff"));
        _logger.Information("Bundles processed: {Processed}", stats.BundlesProcessed);
        _logger.Information("Bundles modified: {Modified}", stats.BundlesModified);
        _logger.Information("Bundles skipped (no color textures): {Skipped}", stats.BundlesSkipped);
        _logger.Information("Textures modified: {TexturesModified}", stats.TexturesModified);
        _logger.Information("Errors encountered: {Errors}", stats.ErrorsEncountered);

        if (stats.BundlesProcessed > 0)
        {
            var successRate = (double)stats.BundlesModified / stats.BundlesProcessed * 100;
            _logger.Information("Success rate: {SuccessRate:F1}%", successRate);

            var avgTexturesPerBundle = stats.BundlesModified > 0
                ? (double)stats.TexturesModified / stats.BundlesModified
                : 0;
            _logger.Information("Average textures per bundle: {AvgTextures:F1}", avgTexturesPerBundle);

            var bundlesPerSecond = stats.BundlesProcessed / elapsed.TotalSeconds;
            _logger.Information("Processing rate: {Rate:F2} bundles/second", bundlesPerSecond);
        }

        if (stats.ErrorsEncountered > 0)
        {
            _logger.Warning("Processing completed with {ErrorCount} errors. Check logs for details.", stats.ErrorsEncountered);
        }
        else
        {
            _logger.Information("Processing completed successfully with no errors!");
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _assetsManager?.UnloadAll();
            _exportService?.Dispose();
            _importService?.Dispose();
            _extractionService?.Dispose();
            _disposed = true;
            _logger.Debug("BDSPTextureRandomizer disposed");
        }
    }

    /// <summary>
    /// Result of processing a single bundle
    /// </summary>
    private class BundleProcessingResult
    {
        public bool Success { get; set; } = false;
        public int TexturesModified { get; set; } = 0;
        public int Errors { get; set; } = 0;
    }
}