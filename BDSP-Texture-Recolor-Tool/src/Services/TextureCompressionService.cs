using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using BDSP.TextureRecolorTool.Models;
using Serilog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CommunityToolkit.HighPerformance;

namespace BDSP.TextureRecolorTool.Services;

/// <summary>
/// Service for handling texture compression using BCnEncoder.NET
/// 
/// This service provides texture compression functionality for the BDSP texture randomizer.
/// It supports multiple compression formats and handles the conversion between ImageSharp
/// Image<Rgba32> objects and compressed texture data suitable for Unity Asset Bundles.
/// 
/// Key features:
/// - RGBA32 uncompressed format (fast, large files)
/// - BC7 high-quality compression (slower, much smaller files)
/// - Extensible architecture for future compression formats
/// - Graceful fallback to RGBA32 on compression failures
/// - Performance optimizations for large textures
/// </summary>
public class TextureCompressionService
{
    private readonly ILogger _logger;

    // Unity TextureFormat constants for better maintainability
    private const int UNITY_TEXTURE_FORMAT_RGBA32 = 4;
    private const int UNITY_TEXTURE_FORMAT_BC7 = 25;

    public TextureCompressionService()
    {
        _logger = Log.ForContext<TextureCompressionService>();
    }

    /// <summary>
    /// Compress an Image<Rgba32> to the specified format
    /// </summary>
    /// <param name="image">Source image to compress</param>
    /// <param name="format">Target compression format</param>
    /// <returns>Compressed texture data bytes</returns>
    public byte[] CompressTexture(Image<Rgba32> image, TextureCompressionFormat format)
    {
        try
        {
            switch (format)
            {
                case TextureCompressionFormat.RGBA32:
                    return CompressToRGBA32(image);

                case TextureCompressionFormat.BC7:
                    return CompressToBC7(image);

                // Future compression formats can be added here:
                // case TextureCompressionFormat.BC1:
                //     return CompressToBC1(image);
                // case TextureCompressionFormat.BC3:
                //     return CompressToBC3(image);
                // etc.

                default:
                    _logger.Warning("Unsupported compression format {Format}, falling back to RGBA32", format);
                    return CompressToRGBA32(image);
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to compress texture with format {Format}, falling back to RGBA32", format);
            return CompressToRGBA32(image);
        }
    }

    /// <summary>
    /// Convert an Image<Rgba32> to uncompressed RGBA32 byte array
    /// 
    /// RGBA32 is the fastest format with no compression overhead.
    /// Results in larger files but very fast processing. Each pixel
    /// uses 4 bytes (R, G, B, A channels) in that order.
    /// 
    /// Performance notes:
    /// - Very fast conversion, minimal CPU usage
    /// - Large file sizes (4 bytes per pixel)
    /// - Compatible with all texture processing operations
    /// </summary>
    /// <param name="image">Source image</param>
    /// <returns>RGBA32 texture data</returns>
    private byte[] CompressToRGBA32(Image<Rgba32> image)
    {
        var data = new byte[image.Width * image.Height * 4];

        // Use ProcessPixelRows for better performance with large textures
        image.ProcessPixelRows(accessor =>
        {
            var index = 0;
            for (int y = 0; y < image.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = row[x];
                    data[index++] = pixel.R;
                    data[index++] = pixel.G;
                    data[index++] = pixel.B;
                    data[index++] = pixel.A;
                }
            }
        });

        _logger.Debug("Converted texture to RGBA32: {Width}x{Height}, {Size} bytes",
            image.Width, image.Height, data.Length);

        return data;
    }

    /// <summary>
    /// Compress an Image<Rgba32> to BC7 format using BCnEncoder.NET
    /// 
    /// BC7 is a high-quality block compression format that provides excellent
    /// quality-to-size ratio for color textures. Processing time is significantly
    /// longer than RGBA32 but results in ~75% smaller file sizes.
    /// 
    /// Performance notes:
    /// - CPU intensive operation, especially for large textures
    /// - Uses balanced quality settings for good speed/quality tradeoff
    /// - Includes fallback to RGBA32 if compression fails
    /// </summary>
    /// <param name="image">Source image to compress</param>
    /// <returns>BC7 compressed texture data</returns>
    private byte[] CompressToBC7(Image<Rgba32> image)
    {
        _logger.Debug("Compressing texture to BC7: {Width}x{Height}", image.Width, image.Height);

        // Create BCnEncoder with BC7 settings
        var encoder = new BcEncoder();
        encoder.OutputOptions.Quality = CompressionQuality.Balanced; // Good balance of quality and speed
        encoder.OutputOptions.Format = CompressionFormat.Bc7;
        encoder.OutputOptions.GenerateMipMaps = false; // Don't generate mipmaps for now, maintain original behavior

        // Convert ImageSharp Image<Rgba32> to Memory2D<ColorRgba32> that BCnEncoder can work with
        var width = image.Width;
        var height = image.Height;
        var pixelData = new BCnEncoder.Shared.ColorRgba32[width * height];

        // Copy pixels from ImageSharp to BCnEncoder format
        // More efficient than nested loops for large textures
        image.ProcessPixelRows(accessor =>
        {
            var pixelIndex = 0;
            for (int y = 0; y < height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    var pixel = row[x];
                    pixelData[pixelIndex++] = new BCnEncoder.Shared.ColorRgba32(pixel.R, pixel.G, pixel.B, pixel.A);
                }
            }
        });

        try
        {
            // Create Memory2D from the pixel array
            var pixelMemory = pixelData.AsMemory().AsMemory2D(height, width);

            // Encode to raw bytes (first mip level only)
            var rawBytes = encoder.EncodeToRawBytes(pixelMemory);
            var compressedData = rawBytes[0]; // Get the first (and only) mip level

            _logger.Debug("Compressed texture to BC7: {Width}x{Height}, original: {OriginalSize} bytes, compressed: {CompressedSize} bytes",
                image.Width, image.Height, image.Width * image.Height * 4, compressedData.Length);

            return compressedData;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "BC7 compression failed for {Width}x{Height} texture, falling back to RGBA32", width, height);
            return CompressToRGBA32(image);
        }
    }

    /// <summary>
    /// Get the Unity TextureFormat enum value for the specified compression format
    /// Maps our internal compression format enum to Unity's texture format constants
    /// </summary>
    /// <param name="format">Compression format</param>
    /// <returns>Unity TextureFormat enum value</returns>
    public int GetUnityTextureFormat(TextureCompressionFormat format)
    {
        return format switch
        {
            TextureCompressionFormat.RGBA32 => UNITY_TEXTURE_FORMAT_RGBA32,
            TextureCompressionFormat.BC7 => UNITY_TEXTURE_FORMAT_BC7,
            // Add cases for future formats here with their Unity TextureFormat values
            _ => UNITY_TEXTURE_FORMAT_RGBA32 // Default to RGBA32 for safety
        };
    }

    /// <summary>
    /// Check if the specified format requires compression (as opposed to raw data)
    /// Used to determine processing requirements and expected data sizes
    /// </summary>
    /// <param name="format">Compression format to check</param>
    /// <returns>True if format is compressed, false if uncompressed</returns>
    public bool IsCompressedFormat(TextureCompressionFormat format)
    {
        return format switch
        {
            TextureCompressionFormat.RGBA32 => false,
            TextureCompressionFormat.BC7 => true,
            // Add cases for future formats - generally block-compressed formats return true
            _ => false // Default to uncompressed for safety
        };
    }

    /// <summary>
    /// Calculate the expected compressed size for a texture
    /// Important for validation and memory allocation planning
    /// </summary>
    /// <param name="width">Texture width</param>
    /// <param name="height">Texture height</param>
    /// <param name="format">Compression format</param>
    /// <returns>Expected size in bytes</returns>
    public int CalculateExpectedSize(int width, int height, TextureCompressionFormat format)
    {
        return format switch
        {
            TextureCompressionFormat.RGBA32 => width * height * 4,
            // BC7 uses 16 bytes per 4x4 block (1 byte per pixel on average)
            TextureCompressionFormat.BC7 => ((width + 3) / 4) * ((height + 3) / 4) * 16,
            // Future formats:
            // BC1: ((width + 3) / 4) * ((height + 3) / 4) * 8,  // 8 bytes per 4x4 block
            // BC3: ((width + 3) / 4) * ((height + 3) / 4) * 16, // 16 bytes per 4x4 block
            _ => width * height * 4 // Default to RGBA32 size
        };
    }
}