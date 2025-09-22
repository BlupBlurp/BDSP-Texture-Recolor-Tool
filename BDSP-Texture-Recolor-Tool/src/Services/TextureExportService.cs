using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using BDSP.TextureRecolorTool.Models;
using Serilog;
using System.Text.Json;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for exporting textures from Unity Asset Bundles to PNG files
/// </summary>
public class TextureExportService : IDisposable
{
    private readonly ILogger _logger;
    private readonly AssetsManager _assetsManager;
    private bool _disposed = false;

    /// <summary>
    /// Supported Unity texture formats for processing
    /// </summary>
    private static readonly HashSet<int> SupportedFormats = new()
    {
        // ASTC formats (mobile/console)
        48, 49, 50, 51, 52, 53, 54,  // ASTC_RGB/RGBA variants
        
        // DXT/BC formats (PC)
        10, 12,                      // DXT1, DXT5
        25, 26, 27, 28,             // BC4, BC5, BC6H, BC7
        
        // ETC formats (mobile)
        34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47,
        
        // PVRTC formats (mobile)
        30, 31, 32, 33,
        
        // Uncompressed formats
        3, 4, 5, 7, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22
    };

    public TextureExportService(AssetsManager assetsManager)
    {
        _assetsManager = assetsManager ?? throw new ArgumentNullException(nameof(assetsManager));
        _logger = Log.ForContext<TextureExportService>();
    }

    /// <summary>
    /// Export all textures from a bundle file to PNG files organized by bundle name
    /// </summary>
    /// <param name="bundlePath">Path to the bundle file</param>
    /// <param name="outputBasePath">Base output directory</param>
    /// <returns>Processing statistics</returns>
    public async Task<BundleExportResult> ExportBundleTexturesAsync(string bundlePath, string outputBasePath)
    {
        var bundleName = Path.GetFileNameWithoutExtension(bundlePath);
        var bundleOutputPath = Path.Combine(outputBasePath, bundleName);
        var result = new BundleExportResult { BundleName = bundleName };

        try
        {
            _logger.Debug("Exporting textures from bundle: {BundlePath}", bundlePath);

            // Create output directory for this bundle
            Directory.CreateDirectory(bundleOutputPath);

            // Load the bundle
            var bundleFileInstance = _assetsManager.LoadBundleFile(bundlePath, false);
            if (bundleFileInstance == null)
            {
                _logger.Error("Failed to load bundle file: {BundlePath}", bundlePath);
                result.Success = false;
                result.Error = "Failed to load bundle file";
                return result;
            }

            var bundleFile = bundleFileInstance.file;

            // Get all asset files in the bundle
            foreach (var dirInfo in bundleFile.BlockAndDirInfo.DirectoryInfos)
            {
                var assetsFilePath = dirInfo.Name;

                // Skip resource files (.resS). They are not asset files
                if (assetsFilePath.EndsWith(".resS", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.Debug("Skipping resource file: {ResourceFile}", assetsFilePath);
                    continue;
                }

                var assetsFileInstance = _assetsManager.LoadAssetsFileFromBundle(bundleFileInstance, assetsFilePath, false);

                if (assetsFileInstance?.file == null)
                {
                    _logger.Warning("Failed to load assets file: {AssetsFile}", assetsFilePath);
                    continue;
                }

                var assetsFile = assetsFileInstance.file;

                // Find all Texture2D assets
                var texture2DAssets = assetsFile.GetAssetsOfType(AssetClassID.Texture2D);

                foreach (var assetInfo in texture2DAssets)
                {
                    var baseField = _assetsManager.GetBaseField(assetsFileInstance, assetInfo);
                    if (baseField == null) continue;

                    try
                    {
                        var texture = await ExportSingleTextureAsync(baseField, assetInfo, bundleOutputPath, result, assetsFileInstance);
                        if (texture != null)
                        {
                            result.ExportedTextures.Add(texture);
                            result.TexturesProcessed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error exporting texture from bundle {Bundle}", bundleName);
                        result.Errors.Add($"Error exporting texture: {ex.Message}");
                    }
                }
            }

            // Save metadata about the exported textures
            await SaveExportMetadataAsync(bundleOutputPath, result);

            result.Success = true;
            _logger.Information("Exported {Count} textures from bundle {Bundle}",
                result.TexturesProcessed, bundleName);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to export bundle: {BundlePath}", bundlePath);
            result.Success = false;
            result.Error = ex.Message;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    /// <summary>
    /// Export a single texture to PNG format
    /// </summary>
    private async Task<ExportedTextureInfo?> ExportSingleTextureAsync(AssetTypeValueField baseField, AssetFileInfo assetInfo, string outputPath, BundleExportResult result, AssetsFileInstance assetsFileInstance)
    {
        try
        {
            // Create texture file object
            var textureFile = TextureFile.ReadTextureFile(baseField);
            if (textureFile == null)
            {
                _logger.Debug("Could not read texture file for PathId {PathId}", assetInfo.PathId);
                return null;
            }

            var textureName = textureFile.m_Name;
            if (string.IsNullOrEmpty(textureName))
            {
                textureName = $"texture_{assetInfo.PathId}";
            }

            var width = textureFile.m_Width;
            var height = textureFile.m_Height;
            var format = (TextureFormat)textureFile.m_TextureFormat;
            var mipCount = textureFile.m_MipCount;

            _logger.Debug("Processing texture: {Name} ({Width}x{Height}, Format: {Format})",
                textureName, width, height, format);

            // Check if format is supported
            if (!SupportedFormats.Contains((int)format))
            {
                _logger.Warning("Unsupported texture format {Format} for texture {Name}", format, textureName);
                result.Warnings.Add($"Unsupported format {format} for texture {textureName}");
                return null;
            }

            // Skip 0x0 textures (often font textures or placeholders)
            if (width == 0 || height == 0)
            {
                _logger.Debug("Skipping 0x0 texture: {TextureName}", textureName);
                return null;
            }

            // Get raw texture bytes
            var rawData = GetRawTextureBytes(textureFile, assetsFileInstance);
            if (rawData == null || rawData.Length == 0)
            {
                _logger.Warning("No texture data found for texture {Name}", textureName);
                result.Warnings.Add($"No texture data for texture {textureName}");
                return null;
            }

            // Convert to Image<Rgba32>
            var image = DecodeTexture(textureFile, rawData);
            if (image == null)
            {
                _logger.Warning("Failed to convert texture data for {Name}", textureName);
                result.Warnings.Add($"Failed to convert texture data for {textureName}");
                return null;
            }

            // Save as PNG
            var pngFileName = $"{textureName}.png";
            var pngFilePath = Path.Combine(outputPath, pngFileName);

            // Ensure unique filename if duplicate names exist
            var counter = 1;
            while (File.Exists(pngFilePath))
            {
                pngFileName = $"{textureName}_{counter}.png";
                pngFilePath = Path.Combine(outputPath, pngFileName);
                counter++;
            }

            await image.SaveAsPngAsync(pngFilePath);
            image.Dispose();

            _logger.Debug("Exported texture {Name} to {Path}", textureName, pngFileName);

            return new ExportedTextureInfo
            {
                OriginalName = textureName,
                FileName = pngFileName,
                Width = width,
                Height = height,
                Format = format,
                MipCount = mipCount,
                PathId = assetInfo.PathId
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error exporting individual texture");
            throw;
        }
    }

    /// <summary>
    /// Save metadata about exported textures for import process
    /// </summary>
    private async Task SaveExportMetadataAsync(string bundleOutputPath, BundleExportResult result)
    {
        try
        {
            var metadataPath = Path.Combine(bundleOutputPath, "export_metadata.json");
            var metadata = new ExportMetadata
            {
                BundleName = result.BundleName,
                ExportDate = DateTime.UtcNow,
                TextureCount = result.TexturesProcessed,
                Textures = result.ExportedTextures
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(metadata, options);
            await File.WriteAllTextAsync(metadataPath, json);

            _logger.Debug("Saved export metadata to {Path}", metadataPath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save export metadata for bundle {Bundle}", result.BundleName);
            result.Warnings.Add($"Failed to save metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Get raw texture bytes (either from embedded data or streaming source)
    /// </summary>
    private byte[]? GetRawTextureBytes(TextureFile textureFile, AssetsFileInstance assetsFileInstance)
    {
        try
        {
            _logger.Debug("Getting texture data for: {TextureName}, StreamPath: {StreamPath}, PictureDataSize: {PictureSize}",
                textureFile.m_Name, textureFile.m_StreamData.path, textureFile.pictureData?.Length ?? 0);

            // Handle streaming data for external texture data (following UABEA's approach)
            if (!string.IsNullOrEmpty(textureFile.m_StreamData.path) && textureFile.m_StreamData.size > 0)
            {
                _logger.Debug("Texture has streaming data: {Size} bytes at offset {Offset}",
                    textureFile.m_StreamData.size, textureFile.m_StreamData.offset);

                // Follow UABEA's GetResSTexture logic
                if (assetsFileInstance.parentBundle != null)
                {
                    // Extract filename from archive path (remove "archive:/" prefix)
                    string searchPath = textureFile.m_StreamData.path;
                    if (searchPath.StartsWith("archive:/"))
                        searchPath = searchPath.Substring(9);

                    searchPath = Path.GetFileName(searchPath);

                    _logger.Debug("Searching for streaming file: {SearchPath} in bundle", searchPath);

                    var bundle = assetsFileInstance.parentBundle.file;
                    var reader = bundle.DataReader;
                    var dirInfos = bundle.BlockAndDirInfo.DirectoryInfos;

                    for (int i = 0; i < dirInfos.Count; i++)
                    {
                        var info = dirInfos[i];
                        if (info.Name == searchPath)
                        {
                            _logger.Debug("Found streaming file in bundle at offset: {Offset}", info.Offset);
                            reader.Position = info.Offset + (long)textureFile.m_StreamData.offset;
                            var data = reader.ReadBytes((int)textureFile.m_StreamData.size);

                            _logger.Debug("Successfully loaded {DataSize} bytes from streaming source", data.Length);
                            return data;
                        }
                    }

                    _logger.Warning("Streaming file not found in bundle: {SearchPath}", searchPath);
                }
                else
                {
                    _logger.Debug("No parent bundle available for streaming texture");
                }
            }

            // Use embedded image data
            return textureFile.pictureData;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error getting raw texture bytes");
            return null;
        }
    }

    /// <summary>
    /// Decode texture data to ImageSharp Image
    /// </summary>
    private Image<Rgba32>? DecodeTexture(TextureFile textureFile, byte[] rawData)
    {
        try
        {
            _logger.Debug("Decoding texture: {Name}, Format: {Format}, Size: {Width}x{Height}, DataSize: {DataSize}",
                textureFile.m_Name, textureFile.m_TextureFormat, textureFile.m_Width, textureFile.m_Height, rawData.Length);

            // Use AssetRipper's TextureDecoder for reliable decoding
            var decodedBytes = TextureFile.DecodeManaged(rawData, (TextureFormat)textureFile.m_TextureFormat,
                (int)textureFile.m_Width, (int)textureFile.m_Height);

            if (decodedBytes == null || decodedBytes.Length == 0)
            {
                _logger.Warning("Texture decoding returned empty data");
                return null;
            }

            _logger.Debug("Decoded texture data: {DecodedSize} bytes, Expected: {Expected} bytes",
                decodedBytes.Length, textureFile.m_Width * textureFile.m_Height * 4);

            // Convert BGRA to RGBA if needed (AssetRipper returns BGRA)
            for (int i = 0; i < decodedBytes.Length; i += 4)
            {
                if (i + 2 < decodedBytes.Length)
                {
                    // Swap B and R channels
                    (decodedBytes[i], decodedBytes[i + 2]) = (decodedBytes[i + 2], decodedBytes[i]);
                }
            }

            // Create ImageSharp image from RGBA bytes
            var image = Image.LoadPixelData<Rgba32>(decodedBytes, (int)textureFile.m_Width, (int)textureFile.m_Height);

            // Unity textures have the Y axis flipped compared to PNG
            // I need to flip the image vertically to match the expected orientation
            image.Mutate(x => x.Flip(FlipMode.Vertical));

            _logger.Debug("Successfully created ImageSharp image: {Width}x{Height}", image.Width, image.Height);
            return image;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error decoding texture data");
            return null;
        }
    }

    /// <summary>
    /// Validate if a texture format is supported
    /// </summary>
    public static bool IsFormatSupported(TextureFormat format)
    {
        return SupportedFormats.Contains((int)format);
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
/// Result of exporting textures from a single bundle
/// </summary>
public class BundleExportResult
{
    public string BundleName { get; set; } = string.Empty;
    public bool Success { get; set; } = false;
    public string Error { get; set; } = string.Empty;
    public int TexturesProcessed { get; set; } = 0;
    public List<ExportedTextureInfo> ExportedTextures { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Information about an exported texture
/// </summary>
public class ExportedTextureInfo
{
    public string OriginalName { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public TextureFormat Format { get; set; }
    public int MipCount { get; set; }
    public long PathId { get; set; }
}

/// <summary>
/// Metadata saved with exported textures for import process
/// </summary>
public class ExportMetadata
{
    public string BundleName { get; set; } = string.Empty;
    public DateTime ExportDate { get; set; }
    public int TextureCount { get; set; }
    public List<ExportedTextureInfo> Textures { get; set; } = new();
}
