using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Represents an image resource within an EPUB
/// </summary>
public sealed class ImageResource : EpubResource
{
    private Size? _dimensions;

    public ImageResource(
        string id,
        string href,
        string mediaType,
        byte[]? content = null,
        Func<byte[]>? contentLoader = null,
        bool isCoverImage = false)
        : base(id, href, mediaType, content, contentLoader)
    {
        if (!IsImage())
            throw new ArgumentException($"Media type '{mediaType}' is not a valid image type", nameof(mediaType));

        IsCoverImage = isCoverImage;
    }

    /// <summary>
    /// Indicates if this is the cover image
    /// </summary>
    public bool IsCoverImage { get; }

    /// <summary>
    /// Gets the image format based on media type
    /// </summary>
    public ImageFormat Format => MediaType.ToLowerInvariant() switch
    {
        "image/jpeg" or "image/jpg" => ImageFormat.Jpeg,
        "image/png" => ImageFormat.Png,
        "image/gif" => ImageFormat.Gif,
        "image/svg+xml" => ImageFormat.Svg,
        "image/webp" => ImageFormat.WebP,
        "image/bmp" => ImageFormat.Bmp,
        _ => ImageFormat.Unknown
    };

    /// <summary>
    /// Attempts to get image dimensions (width and height in pixels)
    /// Note: This is a simplified implementation. For production, use an image library.
    /// </summary>
    public Size? GetDimensions()
    {
        if (_dimensions.HasValue)
            return _dimensions;

        try
        {
            _dimensions = Format switch
            {
                ImageFormat.Jpeg => GetJpegDimensions(),
                ImageFormat.Png => GetPngDimensions(),
                ImageFormat.Gif => GetGifDimensions(),
                _ => null
            };
        }
        catch
        {
            // If we can't determine dimensions, return null
            _dimensions = null;
        }

        return _dimensions;
    }

    private Size? GetJpegDimensions()
    {
        var data = Content;

        // JPEG markers
        const byte MARKER_PREFIX = 0xFF;
        const byte SOF0 = 0xC0;
        const byte SOF2 = 0xC2;

        for (int i = 0; i < data.Length - 10; i++)
        {
            if (data[i] == MARKER_PREFIX && (data[i + 1] == SOF0 || data[i + 1] == SOF2))
            {
                // Height is at offset 5-6, Width at 7-8 from marker
                int height = (data[i + 5] << 8) | data[i + 6];
                int width = (data[i + 7] << 8) | data[i + 8];
                return new Size(width, height);
            }
        }

        return null;
    }

    private Size? GetPngDimensions()
    {
        var data = Content;

        // PNG header is 8 bytes, then IHDR chunk
        // Width is at bytes 16-19, Height at 20-23
        if (data.Length >= 24)
        {
            int width = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
            int height = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
            return new Size(width, height);
        }

        return null;
    }

    private Size? GetGifDimensions()
    {
        var data = Content;

        // GIF dimensions are at bytes 6-7 (width) and 8-9 (height)
        if (data.Length >= 10)
        {
            int width = data[6] | (data[7] << 8);
            int height = data[8] | (data[9] << 8);
            return new Size(width, height);
        }

        return null;
    }

    /// <summary>
    /// Creates an ImageResource from an EpubResource if it's an image
    /// </summary>
    public static ImageResource? FromEpubResource(EpubResource resource, bool isCoverImage = false)
    {
        if (!resource.IsImage())
            return null;

        return new ImageResource(
            resource.Id,
            resource.Href,
            resource.MediaType,
            resource.IsContentLoaded ? resource.Content : null,
            resource.IsContentLoaded ? null : () => resource.Content,
            isCoverImage);
    }

    public override string ToString()
    {
        var coverIndicator = IsCoverImage ? " [COVER]" : "";
        var dimensions = GetDimensions();
        var dimensionText = dimensions.HasValue ? $" {dimensions.Value.Width}x{dimensions.Value.Height}" : "";
        return $"{FileName} ({Format}){dimensionText} [{Size:N0} bytes]{coverIndicator}";
    }
}

/// <summary>
/// Image format enumeration
/// </summary>
public enum ImageFormat
{
    Unknown,
    Jpeg,
    Png,
    Gif,
    Svg,
    WebP,
    Bmp
}

/// <summary>
/// Simple Size structure for image dimensions
/// </summary>
public readonly struct Size
{
    public Size(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public int Width { get; }
    public int Height { get; }

    public override string ToString() => $"{Width}x{Height}";
}