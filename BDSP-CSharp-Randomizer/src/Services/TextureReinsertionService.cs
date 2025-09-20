using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using BDSP.CSharp.Randomizer.Services;
using Serilog;

namespace BDSP.CSharp.Randomizer.Services;

/// <summary>
/// Service for reinserting modified textures back into Unity Asset Bundles
/// </summary>
public class TextureReinsertionService
{
    private readonly ILogger _logger;
    private readonly AssetsManager _assetsManager;

    public TextureReinsertionService(AssetsManager assetsManager)
    {
        _assetsManager = assetsManager ?? throw new ArgumentNullException(nameof(assetsManager));
        _logger = Log.ForContext<TextureReinsertionService>();
    }

    /// <summary>
    /// Reinsert modified textures into a bundle and save to output path
    /// </summary>
    /// <param name="bundleFile">Original bundle file</param>
    /// <param name="modifiedTextures">Modified texture data</param>
    /// <param name="outputPath">Path to save the modified bundle</param>
    /// <returns>Number of textures successfully reinserted</returns>
    public async Task<int> ReinsertTexturesAsync(
        BundleFileInstance bundleFile,
        Dictionary<string, ModifiedTexture> modifiedTextures,
        string outputPath)
    {
        if (bundleFile?.file == null)
        {
            _logger.Warning("Bundle file is null or invalid");
            return 0;
        }

        if (modifiedTextures.Count == 0)
        {
            _logger.Debug("No modified textures to reinsert, copying original bundle");
            File.Copy(bundleFile.path, outputPath, overwrite: true);
            return 0;
        }

        try
        {
            _logger.Debug("Reinserting {Count} textures into bundle: {BundleName}",
                modifiedTextures.Count, bundleFile.name);

            int reinsertedCount = 0;

            // Group textures by their assets file
            var texturesByFile = modifiedTextures.Values.GroupBy(t => t.FileInstance);

            foreach (var fileGroup in texturesByFile)
            {
                var fileInst = fileGroup.Key;
                var texturesInFile = fileGroup.ToList();

                try
                {
                    // Create bundle replacer for this assets file
                    var success = CreateBundleReplacer(fileInst, texturesInFile);
                    if (success)
                    {
                        reinsertedCount += texturesInFile.Count;
                        _logger.Debug("Created bundle replacer for file: {FileName} with {Count} textures",
                            fileInst.name, texturesInFile.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to create replacer for file: {FileName}", fileInst.name);
                }
            }

            // Write the modified bundle
            await WriteBundleWithReplacersAsync(bundleFile, outputPath);
            _logger.Information("Successfully reinserted {Count} textures into bundle: {BundleName}",
                reinsertedCount, bundleFile.name);

            return reinsertedCount;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error reinserting textures into bundle: {BundleName}", bundleFile.name);

            // Fallback: copy original bundle
            try
            {
                File.Copy(bundleFile.path, outputPath, overwrite: true);
                _logger.Information("Copied original bundle as fallback");
            }
            catch (Exception copyEx)
            {
                _logger.Error(copyEx, "Failed to copy original bundle as fallback");
            }

            return 0;
        }
    }

    /// <summary>
    /// Create an AssetFileInfo replacer for a modified texture
    /// </summary>
    private bool CreateAssetReplacer(ModifiedTexture modifiedTexture)
    {
        try
        {
            // Update the base field with new texture data
            var baseField = modifiedTexture.BaseField;

            // Clear streaming data since we're embedding the texture
            var streamDataField = baseField["m_StreamData"];
            if (!streamDataField.IsDummy)
            {
                streamDataField["offset"].AsULong = 0;
                streamDataField["size"].AsUInt = 0;
                streamDataField["path"].AsString = "";
            }

            // Check if we need to convert format - we convert compressed formats to RGBA32 
            // since we don't use external encoders for recompression
            var actualSize = modifiedTexture.ModifiedData.Length;
            var expectedRGBA32Size = modifiedTexture.Width * modifiedTexture.Height * 4;

            bool isFormatConverted = false;
            if (IsCompressedFormat(modifiedTexture.OriginalFormat) && actualSize == expectedRGBA32Size)
            {
                // Converting compressed format to RGBA32 since we don't use external encoders
                _logger.Debug("Converting texture format from {OriginalFormat} to RGBA32 for: {TextureName}",
                    modifiedTexture.OriginalFormat, modifiedTexture.Name);

                // STEP 1: Change format first
                baseField["m_TextureFormat"].AsInt = 4; // TextureFormat.RGBA32
                isFormatConverted = true;
            }
            else if (!IsCompressedFormat(modifiedTexture.OriginalFormat))
            {
                // Keep original format for uncompressed textures when possible
                _logger.Debug("Attempting to preserve original texture format {OriginalFormat} for: {TextureName}",
                    modifiedTexture.OriginalFormat, modifiedTexture.Name);
            }

            // STEP 2: Handle mip map structure for format conversion
            if (isFormatConverted)
            {
                // When converting to RGBA32, we need to properly handle mip maps
                _logger.Debug("Updating mip map structure for format conversion: {TextureName}", modifiedTexture.Name);

                // Disable mip maps for RGBA32 conversion
                var mipMapField = baseField["m_MipMap"];
                if (!mipMapField.IsDummy)
                {
                    mipMapField.AsBool = false;
                }

                // Set mip count to 1 (single level)
                var mipCountField = baseField["m_MipCount"];
                if (!mipCountField.IsDummy)
                {
                    mipCountField.AsInt = 1;
                }

                // Update texture settings for uncompressed data
                var isReadableField = baseField["m_IsReadable"];
                if (!isReadableField.IsDummy)
                {
                    isReadableField.AsBool = true; // Make readable for RGBA32
                }

                _logger.Debug("Updated mip map settings: MipMap=false, MipCount=1, IsReadable=true");
            }

            // STEP 3: Set the new image data AFTER format conversion
            var imageDataField = baseField["image data"];
            if (!imageDataField.IsDummy)
            {
                imageDataField.AsByteArray = modifiedTexture.ModifiedData;
                _logger.Debug("Set new image data: {Size} bytes for {TextureName}",
                    modifiedTexture.ModifiedData.Length, modifiedTexture.Name);
            }

            // STEP 4: Update size fields to match new data
            baseField["m_CompleteImageSize"].AsInt = modifiedTexture.ModifiedData.Length;
            baseField["m_ImageCount"].AsInt = 1;

            // Additional size field that might exist
            var dataSizeField = baseField["m_DataSize"];
            if (!dataSizeField.IsDummy)
            {
                dataSizeField.AsInt = modifiedTexture.ModifiedData.Length;
            }

            // Convert the field back to bytes and set as replacer
            var modifiedAssetData = baseField.WriteToByteArray();
            modifiedTexture.AssetInfo.SetNewData(modifiedAssetData);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating asset replacer for texture: {TextureName}", modifiedTexture.Name);
            return false;
        }
    }

    /// <summary>
    /// Create a bundle directory info replacer for an assets file with modified assets
    /// </summary>
    private bool CreateBundleReplacer(
        AssetsFileInstance fileInst,
        List<ModifiedTexture> modifiedTextures)
    {
        try
        {
            // Apply all asset replacers to the assets file
            foreach (var modifiedTexture in modifiedTextures)
            {
                CreateAssetReplacer(modifiedTexture);
            }

            // Find the corresponding directory info in the bundle
            var bundleFile = fileInst.parentBundle?.file;
            if (bundleFile == null)
            {
                _logger.Error("Parent bundle not found for assets file: {FileName}", fileInst.name);
                return false;
            }

            var dirInfos = bundleFile.BlockAndDirInfo.DirectoryInfos;
            var targetDirInfo = dirInfos.FirstOrDefault(d => d.Name == fileInst.name);
            if (targetDirInfo == null)
            {
                _logger.Error("Directory info not found for assets file: {FileName}", fileInst.name);
                return false;
            }

            // Set the modified assets file as the replacer
            targetDirInfo.SetNewData(fileInst.file);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error creating bundle replacer for file: {FileName}", fileInst.name);
            return false;
        }
    }

    /// <summary>
    /// Write the bundle with all replacers to the output path
    /// </summary>
    private async Task WriteBundleWithReplacersAsync(
        BundleFileInstance bundleFile,
        string outputPath)
    {
        try
        {
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Write the bundle with all replacers applied
            using var outputStream = File.Create(outputPath);
            using var writer = new AssetsFileWriter(outputStream);

            bundleFile.file.Write(writer);
            await outputStream.FlushAsync();

            _logger.Debug("Successfully wrote modified bundle to: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing bundle to: {OutputPath}", outputPath);
            throw;
        }
    }

    /// <summary>
    /// Check if the texture format is a compressed format that needs conversion to RGBA32
    /// Covers all major compressed texture formats used in Unity
    /// </summary>
    private static bool IsCompressedFormat(TextureFormat format)
    {
        int formatValue = (int)format;

        // ASTC formats (48-54)
        if (formatValue >= 48 && formatValue <= 54)
            return true;

        // DXT/BC formats
        if (formatValue == 10 || formatValue == 12) // DXT1, DXT5
            return true;
        if (formatValue >= 25 && formatValue <= 28) // BC4, BC5, BC6H, BC7
            return true;

        // ETC formats (34-47)
        if (formatValue >= 34 && formatValue <= 47)
            return true;

        // PVRTC formats (30-33)
        if (formatValue >= 30 && formatValue <= 33)
            return true;

        // Additional compressed formats that may be encountered
        // For safety, convert any format that's not clearly uncompressed
        // Known uncompressed formats: RGBA32 (4), ARGB32 (5), RGB24 (3), Alpha8 (1), RGBA4444 (13), RGB565 (7)
        var knownUncompressedFormats = new[] { 1, 3, 4, 5, 7, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 };
        if (!knownUncompressedFormats.Contains(formatValue))
        {
            // If it's not in our known uncompressed list, treat it as compressed to be safe
            return true;
        }

        return false;
    }
}