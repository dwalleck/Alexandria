using Alexandria.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using System.Text;

namespace Alexandria.Domain.Tests.ValueObjects;

public class EpubResourceTests
{
    private static byte[] CreateTestContent(string text = "Test content")
    {
        return Encoding.UTF8.GetBytes(text);
    }

    [Test]
    public async Task Should_Create_Resource_With_Direct_Content()
    {
        // Arrange
        var content = CreateTestContent();

        // Act
        var resource = new EpubResource("img1", "images/cover.jpg", "image/jpeg", content);

        // Assert
        await Assert.That(resource.Id).IsEqualTo("img1");
        await Assert.That(resource.Href).IsEqualTo("images/cover.jpg");
        await Assert.That(resource.MediaType).IsEqualTo("image/jpeg");
        await Assert.That(resource.Content).IsEqualTo(content);
        await Assert.That(resource.Size).IsEqualTo(content.Length);
        await Assert.That(resource.IsContentLoaded).IsTrue();
    }

    [Test]
    public async Task Should_Create_Resource_With_Lazy_Loading()
    {
        // Arrange
        var content = CreateTestContent();
        bool loaderCalled = false;
        Func<byte[]> loader = () =>
        {
            loaderCalled = true;
            return content;
        };

        // Act
        var resource = new EpubResource("css1", "styles/main.css", "text/css", contentLoader: loader);

        // Assert - content not loaded yet
        await Assert.That(loaderCalled).IsFalse();
        await Assert.That(resource.IsContentLoaded).IsFalse();

        // Access content
        var loadedContent = resource.Content;

        // Assert - content now loaded
        await Assert.That(loaderCalled).IsTrue();
        await Assert.That(resource.IsContentLoaded).IsTrue();
        await Assert.That(loadedContent).IsEqualTo(content);
    }

    [Test]
    public async Task Should_Extract_FileName_And_Extension()
    {
        // Arrange & Act
        var resource = new EpubResource("img1", "images/chapter1/figure.png", "image/png", CreateTestContent());

        // Assert
        await Assert.That(resource.FileName).IsEqualTo("figure.png");
        await Assert.That(resource.FileExtension).IsEqualTo(".png");
    }

    [Test]
    public async Task Should_Identify_Resource_Types()
    {
        // Arrange
        var imageResource = new EpubResource("img", "image.jpg", "image/jpeg", CreateTestContent());
        var cssResource = new EpubResource("css", "style.css", "text/css", CreateTestContent());
        var fontResource = new EpubResource("font", "font.ttf", "font/ttf", CreateTestContent());
        var htmlResource = new EpubResource("ch1", "chapter.xhtml", "application/xhtml+xml", CreateTestContent());

        // Act & Assert
        await Assert.That(imageResource.IsImage()).IsTrue();
        await Assert.That(imageResource.IsStylesheet()).IsFalse();

        await Assert.That(cssResource.IsStylesheet()).IsTrue();
        await Assert.That(cssResource.IsImage()).IsFalse();

        await Assert.That(fontResource.IsFont()).IsTrue();
        await Assert.That(fontResource.IsImage()).IsFalse();

        await Assert.That(htmlResource.IsHtmlContent()).IsTrue();
        await Assert.That(htmlResource.IsImage()).IsFalse();
    }

    [Test]
    public async Task Should_Throw_When_Id_Is_Empty()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new EpubResource("", "href", "type", CreateTestContent()))
            .Throws<ArgumentException>()
            .WithMessage("Resource ID cannot be empty (Parameter 'id')");
    }

    [Test]
    public async Task Should_Throw_When_Href_Is_Empty()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new EpubResource("id", "", "type", CreateTestContent()))
            .Throws<ArgumentException>()
            .WithMessage("Resource href cannot be empty (Parameter 'href')");
    }

    [Test]
    public async Task Should_Throw_When_MediaType_Is_Empty()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new EpubResource("id", "href", "", CreateTestContent()))
            .Throws<ArgumentException>()
            .WithMessage("Resource media type cannot be empty (Parameter 'mediaType')");
    }

    [Test]
    public async Task Should_Throw_When_No_Content_Or_Loader()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new EpubResource("id", "href", "type", null, null))
            .Throws<ArgumentException>()
            .WithMessage("Either content or contentLoader must be provided");
    }

    [Test]
    public async Task Should_Get_Content_As_String()
    {
        // Arrange
        var text = "Hello, EPUB!";
        var resource = new EpubResource("txt", "text.txt", "text/plain", CreateTestContent(text));

        // Act
        var contentString = resource.GetContentAsString();

        // Assert
        await Assert.That(contentString).IsEqualTo(text);
    }

    [Test]
    public async Task Should_Save_To_File()
    {
        // Arrange
        var content = CreateTestContent("File content");
        var resource = new EpubResource("file", "test.txt", "text/plain", content);
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await resource.SaveToFileAsync(tempFile);

            // Assert
            var savedContent = await File.ReadAllBytesAsync(tempFile);
            await Assert.That(savedContent).IsEqualTo(content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Test]
    public async Task Should_Save_To_Stream()
    {
        // Arrange
        var content = CreateTestContent("Stream content");
        var resource = new EpubResource("stream", "test.txt", "text/plain", content);

        // Act
        using var stream = new MemoryStream();
        await resource.SaveToStreamAsync(stream);

        // Assert
        await Assert.That(stream.ToArray()).IsEqualTo(content);
    }

    [Test]
    public async Task Should_Be_Equal_For_Same_Values()
    {
        // Arrange
        var resource1 = new EpubResource("id", "href", "type", CreateTestContent());
        var resource2 = new EpubResource("id", "href", "type", CreateTestContent("different"));

        // Act & Assert
        await Assert.That(resource1.Equals(resource2)).IsTrue();
        await Assert.That(resource1.GetHashCode()).IsEqualTo(resource2.GetHashCode());
    }

    [Test]
    public async Task Should_Not_Be_Equal_For_Different_Id()
    {
        // Arrange
        var resource1 = new EpubResource("id1", "href", "type", CreateTestContent());
        var resource2 = new EpubResource("id2", "href", "type", CreateTestContent());

        // Act & Assert
        await Assert.That(resource1.Equals(resource2)).IsFalse();
    }

    [Test]
    public async Task Should_Format_ToString()
    {
        // Arrange
        var content = CreateTestContent("12345"); // 5 bytes
        var resource = new EpubResource("img", "images/cover.jpg", "image/jpeg", content);

        // Act
        var result = resource.ToString();

        // Assert
        await Assert.That(result).IsEqualTo("cover.jpg (image/jpeg) [5 bytes]");
    }
}