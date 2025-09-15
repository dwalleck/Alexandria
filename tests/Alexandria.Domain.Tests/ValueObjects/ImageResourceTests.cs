using Alexandria.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using System.Text;

namespace Alexandria.Domain.Tests.ValueObjects;

public class ImageResourceTests
{
    private static byte[] CreateFakeJpegContent()
    {
        // Simplified JPEG with SOF0 marker containing dimensions
        var jpeg = new byte[100];
        jpeg[0] = 0xFF; // JPEG marker
        jpeg[1] = 0xD8; // Start of Image

        // Add SOF0 marker with dimensions (200x100)
        jpeg[10] = 0xFF;
        jpeg[11] = 0xC0; // SOF0 marker
        jpeg[15] = 0x00; // Height high byte
        jpeg[16] = 100;  // Height low byte (100px)
        jpeg[17] = 0x00; // Width high byte
        jpeg[18] = 200;  // Width low byte (200px)

        return jpeg;
    }

    private static byte[] CreateFakePngContent()
    {
        // Simplified PNG with IHDR chunk containing dimensions
        var png = new byte[30];

        // PNG signature
        png[0] = 0x89;
        png[1] = 0x50; // P
        png[2] = 0x4E; // N
        png[3] = 0x47; // G

        // Width at bytes 16-19 (300px)
        png[16] = 0x00;
        png[17] = 0x00;
        png[18] = 0x01;
        png[19] = 0x2C; // 300 in hex

        // Height at bytes 20-23 (150px)
        png[20] = 0x00;
        png[21] = 0x00;
        png[22] = 0x00;
        png[23] = 0x96; // 150 in hex

        return png;
    }

    private static byte[] CreateFakeGifContent()
    {
        // Simplified GIF with dimensions
        var gif = new byte[15];

        // GIF signature
        gif[0] = 0x47; // G
        gif[1] = 0x49; // I
        gif[2] = 0x46; // F

        // Width at bytes 6-7 (250px)
        gif[6] = 0xFA; // 250 low byte
        gif[7] = 0x00; // 250 high byte

        // Height at bytes 8-9 (125px)
        gif[8] = 0x7D; // 125 low byte
        gif[9] = 0x00; // 125 high byte

        return gif;
    }

    [Test]
    public async Task Should_Create_ImageResource()
    {
        // Arrange
        var content = CreateFakeJpegContent();

        // Act
        var image = new ImageResource("cover", "cover.jpg", "image/jpeg", content, isCoverImage: true);

        // Assert
        await Assert.That(image.Id).IsEqualTo("cover");
        await Assert.That(image.Href).IsEqualTo("cover.jpg");
        await Assert.That(image.MediaType).IsEqualTo("image/jpeg");
        await Assert.That(image.IsCoverImage).IsTrue();
        await Assert.That(image.Format).IsEqualTo(ImageFormat.Jpeg);
    }

    [Test]
    public async Task Should_Throw_When_MediaType_Not_Image()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new ImageResource("css", "style.css", "text/css", new byte[10]))
            .Throws<ArgumentException>()
            .WithMessage("Media type 'text/css' is not a valid image type (Parameter 'mediaType')");
    }

    [Test]
    public async Task Should_Identify_Image_Formats()
    {
        // Arrange & Act
        var jpeg = new ImageResource("img1", "image.jpg", "image/jpeg", CreateFakeJpegContent());
        var png = new ImageResource("img2", "image.png", "image/png", CreateFakePngContent());
        var gif = new ImageResource("img3", "image.gif", "image/gif", CreateFakeGifContent());
        var svg = new ImageResource("img4", "image.svg", "image/svg+xml", new byte[10]);
        var webp = new ImageResource("img5", "image.webp", "image/webp", new byte[10]);

        // Assert
        await Assert.That(jpeg.Format).IsEqualTo(ImageFormat.Jpeg);
        await Assert.That(png.Format).IsEqualTo(ImageFormat.Png);
        await Assert.That(gif.Format).IsEqualTo(ImageFormat.Gif);
        await Assert.That(svg.Format).IsEqualTo(ImageFormat.Svg);
        await Assert.That(webp.Format).IsEqualTo(ImageFormat.WebP);
    }

    [Test]
    public async Task Should_Get_Jpeg_Dimensions()
    {
        // Arrange
        var image = new ImageResource("img", "test.jpg", "image/jpeg", CreateFakeJpegContent());

        // Act
        var dimensions = image.GetDimensions();

        // Assert
        await Assert.That(dimensions).IsNotNull();
        await Assert.That(dimensions!.Value.Width).IsEqualTo(200);
        await Assert.That(dimensions.Value.Height).IsEqualTo(100);
    }

    [Test]
    public async Task Should_Get_Png_Dimensions()
    {
        // Arrange
        var image = new ImageResource("img", "test.png", "image/png", CreateFakePngContent());

        // Act
        var dimensions = image.GetDimensions();

        // Assert
        await Assert.That(dimensions).IsNotNull();
        await Assert.That(dimensions!.Value.Width).IsEqualTo(300);
        await Assert.That(dimensions.Value.Height).IsEqualTo(150);
    }

    [Test]
    public async Task Should_Get_Gif_Dimensions()
    {
        // Arrange
        var image = new ImageResource("img", "test.gif", "image/gif", CreateFakeGifContent());

        // Act
        var dimensions = image.GetDimensions();

        // Assert
        await Assert.That(dimensions).IsNotNull();
        await Assert.That(dimensions!.Value.Width).IsEqualTo(250);
        await Assert.That(dimensions.Value.Height).IsEqualTo(125);
    }

    [Test]
    public async Task Should_Return_Null_Dimensions_For_Unknown_Format()
    {
        // Arrange
        var image = new ImageResource("img", "test.svg", "image/svg+xml", new byte[10]);

        // Act
        var dimensions = image.GetDimensions();

        // Assert
        await Assert.That(dimensions).IsNull();
    }

    [Test]
    public async Task Should_Create_From_EpubResource()
    {
        // Arrange
        var epubResource = new EpubResource("img", "image.png", "image/png", CreateFakePngContent());

        // Act
        var imageResource = ImageResource.FromEpubResource(epubResource, isCoverImage: true);

        // Assert
        await Assert.That(imageResource).IsNotNull();
        await Assert.That(imageResource!.Id).IsEqualTo("img");
        await Assert.That(imageResource.IsCoverImage).IsTrue();
        await Assert.That(imageResource.Format).IsEqualTo(ImageFormat.Png);
    }

    [Test]
    public async Task Should_Return_Null_When_Creating_From_Non_Image_Resource()
    {
        // Arrange
        var epubResource = new EpubResource("css", "style.css", "text/css", new byte[10]);

        // Act
        var imageResource = ImageResource.FromEpubResource(epubResource);

        // Assert
        await Assert.That(imageResource).IsNull();
    }

    [Test]
    public async Task Should_Format_ToString_With_Cover_Indicator()
    {
        // Arrange
        var image = new ImageResource("cover", "cover.jpg", "image/jpeg", CreateFakeJpegContent(), isCoverImage: true);

        // Act
        var result = image.ToString();

        // Assert
        await Assert.That(result).Contains("cover.jpg");
        await Assert.That(result).Contains("Jpeg");
        await Assert.That(result).Contains("200x100");
        await Assert.That(result).Contains("[COVER]");
    }

    [Test]
    public async Task Should_Format_ToString_Without_Dimensions()
    {
        // Arrange
        var image = new ImageResource("img", "image.svg", "image/svg+xml", new byte[50]);

        // Act
        var result = image.ToString();

        // Assert
        await Assert.That(result).Contains("image.svg");
        await Assert.That(result).Contains("Svg");
        await Assert.That(result).Contains("50 bytes");
        await Assert.That(result).DoesNotContain("x"); // No dimensions
    }
}