using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using BDSP.TextureRecolorTool.Models;
using Serilog;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for extracting and manipulating textures from Unity Asset Bundles
/// </summary>
public class TextureExtractionService : IDisposable
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

    public TextureExtractionService(AssetsManager assetsManager)
    {
        _assetsManager = assetsManager ?? throw new ArgumentNullException(nameof(assetsManager));
        _logger = Log.ForContext<TextureExtractionService>();
    }

    /// <summary>
    /// Extract and process textures from a bundle file
    /// </summary>
    /// <param name="bundleFile">Bundle file instance</param>
    /// <param name="colorParams">Color parameters for this bundle</param>
    /// <returns>Dictionary of texture names and their modified data</returns>
    public async Task<Dictionary<string, ModifiedTexture>> ExtractAndProcessTexturesAsync(
        BundleFileInstance bundleFile, BundleColorParameters colorParams)
    {
        var modifiedTextures = new Dictionary<string, ModifiedTexture>();

        if (bundleFile?.file == null)
        {
            _logger.Warning("Bundle file is null or invalid");
            return modifiedTextures;
        }

        try
        {
            _logger.Debug("Extracting textures from bundle: {BundleName}", bundleFile.name);

            // Load bundle contents - bundleFile is already loaded by AssetsManager
            // _assetsManager.LoadBundleFile(bundleFile); // This line is not needed

            // Get all asset files in the bundle
            var assetFiles = bundleFile.file.BlockAndDirInfo.DirectoryInfos;

            // Use Task.Run for CPU-bound texture processing work
            await Task.Run(() =>
            {
                foreach (var dirInfo in assetFiles)
                {
                    var assetFile = _assetsManager.LoadAssetsFileFromBundle(bundleFile, dirInfo.Name);
                    if (assetFile == null) continue;

                    ProcessAssetsFile(assetFile, colorParams, modifiedTextures);
                }
            });

            _logger.Information("Extracted and processed {Count} textures from bundle", modifiedTextures.Count);
            return modifiedTextures;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error extracting textures from bundle: {BundleName}", bundleFile.name);
            return modifiedTextures;
        }
    }

    /// <summary>
    /// Process textures in a single assets file
    /// </summary>
    private void ProcessAssetsFile(
        AssetsFileInstance fileInst,
        BundleColorParameters colorParams,
        Dictionary<string, ModifiedTexture> modifiedTextures)
    {
        try
        {
            // Find all Texture2D assets
            var texture2DAssets = fileInst.file.GetAssetsOfType(AssetClassID.Texture2D);

            foreach (var asset in texture2DAssets)
            {
                try
                {
                    ProcessSingleTexture(fileInst, asset, colorParams, modifiedTextures);
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to process texture asset {PathId} in {FileName}",
                        asset.PathId, fileInst.name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing assets file: {FileName}", fileInst.name);
        }
    }

    /// <summary>
    /// Process a single texture asset
    /// </summary>
    private void ProcessSingleTexture(
        AssetsFileInstance fileInst,
        AssetFileInfo assetInfo,
        BundleColorParameters colorParams,
        Dictionary<string, ModifiedTexture> modifiedTextures)
    {
        try
        {
            // Get the base field for the texture
            var baseField = _assetsManager.GetBaseField(fileInst, assetInfo);
            if (baseField == null)
            {
                _logger.Debug("Could not get base field for texture {PathId}", assetInfo.PathId);
                return;
            }

            // Create texture file object
            var textureFile = TextureFile.ReadTextureFile(baseField);
            if (textureFile == null)
            {
                _logger.Debug("Could not read texture file for {PathId}", assetInfo.PathId);
                return;
            }

            // Skip textures that don't contain "_col" in their name (only process color textures)
            if (!textureFile.m_Name.Contains("_col", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Debug("Skipping non-color texture: {TextureName}", textureFile.m_Name);
                return;
            }

            // Skip 0x0 textures (often font textures or placeholders)
            if (textureFile.m_Width == 0 || textureFile.m_Height == 0)
            {
                _logger.Debug("Skipping 0x0 texture: {TextureName}", textureFile.m_Name);
                return;
            }

            // Check if format is supported
            if (!SupportedFormats.Contains((int)textureFile.m_TextureFormat))
            {
                _logger.Debug("Unsupported texture format {Format} for texture: {TextureName}",
                    textureFile.m_TextureFormat, textureFile.m_Name);
                return;
            }

            _logger.Debug("Processing texture: {TextureName} ({Width}x{Height}, Format: {Format})",
                textureFile.m_Name, textureFile.m_Width, textureFile.m_Height, textureFile.m_TextureFormat);

            // Get raw texture bytes
            var rawData = GetRawTextureBytes(textureFile, fileInst);
            if (rawData == null || rawData.Length == 0)
            {
                _logger.Warning("Could not get raw texture data for: {TextureName}", textureFile.m_Name);
                return;
            }

            // Decode texture to ImageSharp format
            var decodedImage = DecodeTexture(textureFile, rawData);
            if (decodedImage == null)
            {
                _logger.Warning("Could not decode texture: {TextureName}", textureFile.m_Name);
                return;
            }

            // Apply color randomization
            var colorService = new ColorRandomizationService();
            var modifiedImage = colorService.RandomizeTextureColor(decodedImage, colorParams, textureFile.m_Name);

            // Encode back to original format
            var encodedData = EncodeTexture(modifiedImage, (TextureFormat)textureFile.m_TextureFormat);
            if (encodedData == null)
            {
                _logger.Warning("Could not encode modified texture: {TextureName}", textureFile.m_Name);
                decodedImage.Dispose();
                modifiedImage.Dispose();
                return;
            }

            // Store modified texture data
            var modifiedTexture = new ModifiedTexture
            {
                Name = textureFile.m_Name,
                OriginalFormat = (TextureFormat)textureFile.m_TextureFormat,
                Width = (uint)textureFile.m_Width,
                Height = (uint)textureFile.m_Height,
                ModifiedData = encodedData,
                BaseField = baseField,
                AssetInfo = assetInfo,
                FileInstance = fileInst
            };

            var key = $"{fileInst.name}_{assetInfo.PathId}";
            modifiedTextures[key] = modifiedTexture;

            _logger.Debug("Successfully processed texture: {TextureName}", textureFile.m_Name);

            // Cleanup
            decodedImage.Dispose();
            modifiedImage.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing texture {PathId}", assetInfo.PathId);
        }
    }

    /// <summary>
    /// Get raw texture bytes from TextureFile
    /// </summary>
    private byte[]? GetRawTextureBytes(TextureFile textureFile, AssetsFileInstance fileInst)
    {
        try
        {
            _logger.Debug("Getting texture data for: {TextureName}, StreamPath: {StreamPath}, PictureDataSize: {PictureSize}",
                textureFile.m_Name, textureFile.m_StreamData.path, textureFile.pictureData?.Length ?? 0);

            // Handle streamingInfo for external texture data using UABEA's approach
            if (!string.IsNullOrEmpty(textureFile.m_StreamData.path) && textureFile.m_StreamData.size > 0)
            {
                // Follow UABEA's GetResSTexture logic
                if (fileInst.parentBundle != null)
                {
                    // Extract filename from archive path (remove "archive:/" prefix)
                    string searchPath = textureFile.m_StreamData.path;
                    if (searchPath.StartsWith("archive:/"))
                        searchPath = searchPath.Substring(9);

                    searchPath = Path.GetFileName(searchPath);

                    _logger.Debug("Searching for streaming file: {SearchPath} in bundle", searchPath);

                    var bundle = fileInst.parentBundle.file;
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
    /// Encode ImageSharp image back to Unity texture format
    /// </summary>
    private byte[]? EncodeTexture(Image<Rgba32> image, TextureFormat targetFormat)
    {
        try
        {
            _logger.Debug("Encoding texture: {Width}x{Height}, TargetFormat: {Format}",
                image.Width, image.Height, targetFormat);

            // Convert to RGBA32 byte array directly since we don't use external encoders
            var encodedData = ConvertImageToRGBA32(image);

            if (encodedData != null)
            {
                _logger.Debug("Successfully converted texture to RGBA32, Size: {Size} bytes",
                    targetFormat, encodedData.Length);
                return encodedData;
            }

            // Fallback: if encoding failed, convert to RGBA32 for compatibility
            _logger.Warning("Failed to encode to {Format}, falling back to RGBA32", targetFormat);

            var rgbaBytes = new byte[image.Width * image.Height * 4];
            image.CopyPixelDataTo(rgbaBytes);

            _logger.Debug("Extracted {ByteCount} RGBA bytes as fallback", rgbaBytes.Length);
            return rgbaBytes;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error encoding texture data");
            return null;
        }
    }

    /// <summary>
    /// Check if the texture format is a compressed format that needs conversion to RGBA32
    /// Covers all major compressed texture formats used in Unity
    /// </summary>
    private bool IsCompressedFormat(TextureFormat format)
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

    /// <summary>
    /// Convert ImageSharp image to RGBA32 byte array
    /// </summary>
    private static byte[] ConvertImageToRGBA32(Image<Rgba32> image)
    {
        var bytes = new byte[image.Width * image.Height * 4];
        int index = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                bytes[index++] = pixel.R;
                bytes[index++] = pixel.G;
                bytes[index++] = pixel.B;
                bytes[index++] = pixel.A;
            }
        }

        return bytes;
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
/// Represents a modified texture ready for reinsertion
/// </summary>
public class ModifiedTexture
{
    public required string Name { get; set; }
    public TextureFormat OriginalFormat { get; set; }
    public uint Width { get; set; }
    public uint Height { get; set; }
    public required byte[] ModifiedData { get; set; }
    public required AssetTypeValueField BaseField { get; set; }
    public required AssetFileInfo AssetInfo { get; set; }
    public required AssetsFileInstance FileInstance { get; set; }
}
