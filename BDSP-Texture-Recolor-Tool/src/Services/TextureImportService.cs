using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using BDSP.TextureRecolorTool.Models;
using Serilog;
using System.Text.Json;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for importing PNG textures back into Unity Asset Bundles
/// </summary>
public class TextureImportService : IDisposable
{
    private readonly ILogger _logger;
    private readonly AssetsManager _assetsManager;
    private bool _disposed = false;

    public TextureImportService(AssetsManager assetsManager)
    {
        _assetsManager = assetsManager ?? throw new ArgumentNullException(nameof(assetsManager));
        _logger = Log.ForContext<TextureImportService>();
    }

    /// <summary>
    /// Import PNG textures back into a bundle file
    /// </summary>
    /// <param name="originalBundlePath">Path to the original bundle file</param>
    /// <param name="texturesPath">Path to directory containing PNG textures and metadata</param>
    /// <param name="outputBundlePath">Path where the modified bundle will be saved</param>
    /// <returns>Import result</returns>
    public async Task<BundleImportResult> ImportBundleTexturesAsync(string originalBundlePath, string texturesPath, string outputBundlePath)
    {
        var bundleName = Path.GetFileNameWithoutExtension(originalBundlePath);
        var result = new BundleImportResult { BundleName = bundleName };

        try
        {
            _logger.Debug("Importing textures into bundle: {BundlePath}", originalBundlePath);

            // Load export metadata
            var metadataPath = Path.Combine(texturesPath, "export_metadata.json");
            if (!File.Exists(metadataPath))
            {
                result.Success = false;
                result.Error = $"Export metadata not found at: {metadataPath}";
                return result;
            }

            var metadataJson = await File.ReadAllTextAsync(metadataPath);
            var metadata = JsonSerializer.Deserialize<ExportMetadata>(metadataJson);
            if (metadata == null)
            {
                result.Success = false;
                result.Error = "Failed to parse export metadata";
                return result;
            }

            _logger.Information("Found metadata for {Count} textures in bundle {Bundle}",
                metadata.TextureCount, metadata.BundleName);

            // Load the original bundle
            var bundleFileInstance = _assetsManager.LoadBundleFile(originalBundlePath, false);
            if (bundleFileInstance == null)
            {
                result.Success = false;
                result.Error = "Failed to load original bundle file";
                return result;
            }

            var bundleFile = bundleFileInstance.file;
            var modifiedAssetsFiles = new List<AssetsFileInstance>();

            // Process each assets file in the bundle
            foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
            {
                var assetsFilePath = dirInfo.Name;
                var assetsFileInstance = _assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, assetsFilePath, false);

                if (assetsFileInstance?.file == null)
                {
                    _logger.Warning("Failed to load assets file: {AssetsFile}", assetsFilePath);
                    continue;
                }

                var assetsFile = assetsFileInstance.file;
                bool hasModifications = false;

                // Find all Texture2D assets
                var texture2DAssets = assetsFile.GetAssetsOfType(AssetClassID.Texture2D);

                foreach (var assetInfo in texture2DAssets)
                {
                    var baseField = _assetsManager.GetBaseField(assetsFileInstance, assetInfo);
                    if (baseField == null) continue;

                    try
                    {
                        if (await ApplyTextureModificationAsync(baseField, assetInfo, texturesPath, metadata, result))
                        {
                            result.TexturesModified++;
                            hasModifications = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error modifying texture for PathId {PathId}", assetInfo.PathId);
                        result.Errors.Add($"Error processing texture PathId {assetInfo.PathId}: {ex.Message}");
                    }
                }

                if (hasModifications)
                {
                    modifiedAssetsFiles.Add(assetsFileInstance);
                }
            }

            // Create output directory
            var outputDir = Path.GetDirectoryName(outputBundlePath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Write the modified bundle
            if (modifiedAssetsFiles.Count > 0)
            {
                _logger.Information("Applying texture modifications to {Count} assets files in bundle {Bundle}",
                    modifiedAssetsFiles.Count, bundleName);

                // CRITICAL: Register each modified assets file with the bundle
                foreach (var modifiedAssetsFile in modifiedAssetsFiles)
                {
                    var dirInfos = bundleFile.BlockAndDirInfo.DirectoryInfos;
                    var targetDirInfo = dirInfos.FirstOrDefault(d => d.Name == modifiedAssetsFile.name);
                    if (targetDirInfo != null)
                    {
                        // Set the modified assets file as the replacer
                        targetDirInfo.SetNewData(modifiedAssetsFile.file);
                        _logger.Debug("Registered modified assets file: {FileName}", modifiedAssetsFile.name);
                    }
                    else
                    {
                        _logger.Warning("Directory info not found for assets file: {FileName}", modifiedAssetsFile.name);
                    }
                }

                // Write the bundle with all replacers applied
                using var outputStream = File.Create(outputBundlePath);
                using var writer = new AssetsFileWriter(outputStream);

                bundleFile.Write(writer);
                await outputStream.FlushAsync();

                result.Success = true;

                _logger.Information("Successfully imported {Count} textures into bundle {Bundle}",
                    result.TexturesModified, bundleName);
            }
            else
            {
                _logger.Warning("No texture modifications found for bundle {Bundle}", bundleName);

                // Copy original file if no modifications were made
                File.Copy(originalBundlePath, outputBundlePath, true);
                result.Success = true;
                result.Warnings.Add("No textures were modified - original bundle copied");
            }

            result.TexturesProcessed = metadata.TextureCount;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import textures into bundle: {BundlePath}", originalBundlePath);
            result.Success = false;
            result.Error = ex.Message;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Apply texture modification for a single texture
    /// </summary>
    private async Task<bool> ApplyTextureModificationAsync(AssetTypeValueField baseField, AssetFileInfo assetInfo, string texturesPath, ExportMetadata metadata, BundleImportResult result)
    {
        try
        {
            var pathId = assetInfo.PathId;
            var textureName = baseField["m_Name"].AsString;

            // Find the corresponding exported texture in metadata
            var exportedTexture = metadata.Textures.FirstOrDefault(t => t.PathId == pathId);
            if (exportedTexture == null)
            {
                // Try to find by name if PathId doesn't match
                exportedTexture = metadata.Textures.FirstOrDefault(t => t.OriginalName == textureName);
                if (exportedTexture == null)
                {
                    _logger.Debug("No exported texture found for {Name} (PathId: {PathId})", textureName, pathId);
                    return false;
                }
            }

            // Check if the PNG file exists
            var pngPath = Path.Combine(texturesPath, exportedTexture.FileName);
            if (!File.Exists(pngPath))
            {
                _logger.Warning("PNG file not found: {PngPath}", pngPath);
                result.Warnings.Add($"PNG file not found: {exportedTexture.FileName}");
                return false;
            }

            // Load the PNG image
            using var image = await Image.LoadAsync<Rgba32>(pngPath);

            // Verify dimensions match
            if (image.Width != exportedTexture.Width || image.Height != exportedTexture.Height)
            {
                _logger.Warning("Dimension mismatch for texture {Name}: expected {ExpectedW}x{ExpectedH}, got {ActualW}x{ActualH}",
                    exportedTexture.OriginalName, exportedTexture.Width, exportedTexture.Height, image.Width, image.Height);
                result.Warnings.Add($"Dimension mismatch for {exportedTexture.OriginalName}");
            }

            // Convert image back to texture data
            var textureData = ConvertImageToTextureData(image, exportedTexture.Format);
            if (textureData == null)
            {
                _logger.Error("Failed to convert PNG to texture data for {Name}", exportedTexture.OriginalName);
                result.Errors.Add($"Failed to convert PNG to texture data for {exportedTexture.OriginalName}");
                return false;
            }

            // Apply modifications to the base field
            // Clear streaming data since we're embedding the texture
            var streamDataField = baseField["m_StreamData"];
            if (!streamDataField.IsDummy)
            {
                streamDataField["offset"].AsULong = 0;
                streamDataField["size"].AsUInt = 0;
                streamDataField["path"].AsString = "";
            }

            // Update format if it was converted (e.g., ASTC to RGBA32)
            bool formatConverted = ShouldConvertFormat(exportedTexture.Format);
            if (formatConverted)
            {
                baseField["m_TextureFormat"].AsInt = (int)TextureFormat.RGBA32;

                // Handle mip map structure for format conversion
                var mipMapField = baseField["m_MipMap"];
                if (!mipMapField.IsDummy)
                {
                    mipMapField.AsBool = false;
                }

                // Set mip count to 1 (single level)
                baseField["m_MipCount"].AsInt = 1;

                // Update texture settings for uncompressed data
                var isReadableField = baseField["m_IsReadable"];
                if (!isReadableField.IsDummy)
                {
                    isReadableField.AsBool = true; // Make readable for RGBA32
                }
            }

            // Update texture data
            baseField["image data"].AsByteArray = textureData;

            // Update dimensions in case they changed
            baseField["m_Width"].AsInt = image.Width;
            baseField["m_Height"].AsInt = image.Height;

            // CRITICAL: Update size fields to match new data
            baseField["m_CompleteImageSize"].AsInt = textureData.Length;
            baseField["m_ImageCount"].AsInt = 1;

            // Additional size field that might exist
            var dataSizeField = baseField["m_DataSize"];
            if (!dataSizeField.IsDummy)
            {
                dataSizeField.AsInt = textureData.Length;
            }

            // Convert the field back to bytes and create a replacer
            var modifiedAssetData = baseField.WriteToByteArray();
            assetInfo.SetNewData(modifiedAssetData);

            _logger.Debug("Applied texture modification for {Name} (PathId: {PathId})", exportedTexture.OriginalName, pathId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error applying texture modification");
            throw;
        }
    }

    /// <summary>
    /// Convert an Image<Rgba32> to texture data bytes
    /// </summary>
    private byte[]? ConvertImageToTextureData(Image<Rgba32> image, TextureFormat originalFormat)
    {
        try
        {
            // For now, convert everything to RGBA32 format for simplicity
            // This ensures compatibility and avoids complex format-specific encoding
            var targetFormat = ShouldConvertFormat(originalFormat) ? TextureFormat.RGBA32 : originalFormat;

            if (targetFormat == TextureFormat.RGBA32)
            {
                var data = new byte[image.Width * image.Height * 4];
                var index = 0;

                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        var pixel = image[x, y];
                        data[index++] = pixel.R;
                        data[index++] = pixel.G;
                        data[index++] = pixel.B;
                        data[index++] = pixel.A;
                    }
                }

                return data;
            }
            else
            {
                // For other formats, we would need format-specific encoding
                // This is a complex task that requires specialized texture compression libraries
                _logger.Warning("Advanced format encoding not implemented for {Format}, using RGBA32", originalFormat);
                return ConvertImageToTextureData(image, TextureFormat.RGBA32);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error converting image to texture data");
            return null;
        }
    }

    /// <summary>
    /// Determine if a texture format should be converted to RGBA32
    /// </summary>
    private static bool ShouldConvertFormat(TextureFormat format)
    {
        // Convert ASTC and other compressed formats to RGBA32 for simplicity
        return format switch
        {
            TextureFormat.ASTC_RGB_4x4 or TextureFormat.ASTC_RGB_5x5 or TextureFormat.ASTC_RGB_6x6 or
            TextureFormat.ASTC_RGB_8x8 or TextureFormat.ASTC_RGB_10x10 or TextureFormat.ASTC_RGB_12x12 or
            TextureFormat.ASTC_RGBA_4x4 or TextureFormat.ASTC_RGBA_5x5 or TextureFormat.ASTC_RGBA_6x6 or
            TextureFormat.ASTC_RGBA_8x8 or TextureFormat.ASTC_RGBA_10x10 or TextureFormat.ASTC_RGBA_12x12 => true,
            _ => false
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Result of importing textures into a bundle
/// </summary>
public class BundleImportResult
{
    public string BundleName { get; set; } = string.Empty;
    public bool Success { get; set; } = false;
    public string Error { get; set; } = string.Empty;
    public int TexturesProcessed { get; set; } = 0;
    public int TexturesModified { get; set; } = 0;
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
