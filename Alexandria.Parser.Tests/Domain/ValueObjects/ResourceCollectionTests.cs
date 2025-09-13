using Alexandria.Parser.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using System.Text;

namespace Alexandria.Parser.Tests.Domain.ValueObjects;

public class ResourceCollectionTests
{
    private static List<EpubResource> CreateTestResources()
    {
        return
        [
            new EpubResource("img1", "images/cover.jpg", "image/jpeg", Encoding.UTF8.GetBytes("cover")),
            new EpubResource("img2", "images/chapter1.png", "image/png", Encoding.UTF8.GetBytes("ch1")),
            new EpubResource("css1", "styles/main.css", "text/css", Encoding.UTF8.GetBytes("css")),
            new EpubResource("font1", "fonts/main.ttf", "font/ttf", Encoding.UTF8.GetBytes("font")),
            new EpubResource("ch1", "chapter1.xhtml", "application/xhtml+xml", Encoding.UTF8.GetBytes("html"))
        ];
    }

    [Test]
    public async Task Should_Create_ResourceCollection()
    {
        // Arrange
        var resources = CreateTestResources();

        // Act
        var collection = new ResourceCollection(resources);

        // Assert
        await Assert.That(collection.Count).IsEqualTo(5);
        await Assert.That(collection.All).HasCount(5);
    }

    [Test]
    public async Task Should_Get_Resource_By_Id()
    {
        // Arrange
        var resources = CreateTestResources();
        var collection = new ResourceCollection(resources);

        // Act
        var resource = collection.GetById("img1");

        // Assert
        await Assert.That(resource).IsNotNull();
        await Assert.That(resource!.Href).IsEqualTo("images/cover.jpg");
    }

    [Test]
    public async Task Should_Get_Resource_By_Href()
    {
        // Arrange
        var resources = CreateTestResources();
        var collection = new ResourceCollection(resources);

        // Act
        var resource = collection.GetByHref("styles/main.css");

        // Assert
        await Assert.That(resource).IsNotNull();
        await Assert.That(resource!.Id).IsEqualTo("css1");
    }

    [Test]
    public async Task Should_Get_Resource_By_Href_Without_Fragment()
    {
        // Arrange
        var resources = CreateTestResources();
        var collection = new ResourceCollection(resources);

        // Act
        var resource = collection.GetByHref("chapter1.xhtml#section1");

        // Assert
        await Assert.That(resource).IsNotNull();
        await Assert.That(resource!.Id).IsEqualTo("ch1");
    }

    [Test]
    public async Task Should_Return_Null_For_Non_Existent_Resource()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act
        var byId = collection.GetById("nonexistent");
        var byHref = collection.GetByHref("nonexistent.file");

        // Assert
        await Assert.That(byId).IsNull();
        await Assert.That(byHref).IsNull();
    }

    [Test]
    public async Task Should_Get_Images()
    {
        // Arrange
        var resources = CreateTestResources();
        var coverImage = new ImageResource("cover", "images/cover.jpg", "image/jpeg",
            Encoding.UTF8.GetBytes("cover"), isCoverImage: true);
        resources.Add(coverImage);
        var collection = new ResourceCollection(resources);

        // Act
        var images = collection.GetImages().ToList();
        var imageResources = collection.GetImageResources().ToList();

        // Assert
        await Assert.That(images).HasCount(1); // Only ImageResource objects
        await Assert.That(imageResources).HasCount(3); // All image resources including regular EpubResource
    }

    [Test]
    public async Task Should_Get_Cover_Image()
    {
        // Arrange
        var resources = CreateTestResources();
        var coverImage = new ImageResource("cover", "images/cover_special.jpg", "image/jpeg",
            Encoding.UTF8.GetBytes("cover"), isCoverImage: true);
        resources.Add(coverImage);
        var collection = new ResourceCollection(resources);

        // Act
        var cover = collection.CoverImage;

        // Assert
        await Assert.That(cover).IsNotNull();
        await Assert.That(cover!.Id).IsEqualTo("cover");
        await Assert.That(cover.IsCoverImage).IsTrue();
    }

    [Test]
    public async Task Should_Get_Stylesheets()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act
        var stylesheets = collection.GetStylesheets().ToList();

        // Assert
        await Assert.That(stylesheets).HasCount(1);
        await Assert.That(stylesheets[0].Id).IsEqualTo("css1");
    }

    [Test]
    public async Task Should_Get_Fonts()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act
        var fonts = collection.GetFonts().ToList();

        // Assert
        await Assert.That(fonts).HasCount(1);
        await Assert.That(fonts[0].Id).IsEqualTo("font1");
    }

    [Test]
    public async Task Should_Get_By_MediaType()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act
        var images = collection.GetByMediaType("image/jpeg").ToList();

        // Assert
        await Assert.That(images).HasCount(1);
        await Assert.That(images[0].Id).IsEqualTo("img1");
    }

    [Test]
    public async Task Should_Get_By_Extension()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act
        var pngFiles = collection.GetByExtension(".png").ToList();
        var pngFilesNoDot = collection.GetByExtension("png").ToList();

        // Assert
        await Assert.That(pngFiles).HasCount(1);
        await Assert.That(pngFiles[0].Id).IsEqualTo("img2");
        await Assert.That(pngFilesNoDot).HasCount(1);
    }

    [Test]
    public async Task Should_Check_Contains()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act & Assert
        await Assert.That(collection.ContainsId("img1")).IsTrue();
        await Assert.That(collection.ContainsId("nonexistent")).IsFalse();
        await Assert.That(collection.ContainsHref("styles/main.css")).IsTrue();
        await Assert.That(collection.ContainsHref("nonexistent.file")).IsFalse();
    }

    [Test]
    public async Task Should_Calculate_Total_Size()
    {
        // Arrange
        var collection = new ResourceCollection(CreateTestResources());

        // Act
        var totalSize = collection.GetTotalSize();

        // Assert
        // "cover" (5) + "ch1" (3) + "css" (3) + "font" (4) + "html" (4) = 19 bytes
        await Assert.That(totalSize).IsEqualTo(19);
    }

    [Test]
    public async Task Should_Throw_For_Duplicate_Id()
    {
        // Arrange
        var resources = new List<EpubResource>
        {
            new EpubResource("id1", "file1.txt", "text/plain", new byte[1]),
            new EpubResource("id1", "file2.txt", "text/plain", new byte[1]) // Duplicate ID
        };

        // Act & Assert
        await Assert.That(() => new ResourceCollection(resources))
            .Throws<ArgumentException>()
            .WithMessage("Duplicate resource ID: id1 (Parameter 'resources')");
    }

    [Test]
    public async Task Should_Throw_For_Multiple_Cover_Images()
    {
        // Arrange
        var resources = new List<EpubResource>
        {
            new ImageResource("cover1", "cover1.jpg", "image/jpeg", new byte[1], isCoverImage: true),
            new ImageResource("cover2", "cover2.jpg", "image/jpeg", new byte[1], isCoverImage: true)
        };

        // Act & Assert
        await Assert.That(() => new ResourceCollection(resources))
            .Throws<ArgumentException>()
            .WithMessage("Multiple cover images found (Parameter 'resources')");
    }

    [Test]
    public async Task Should_Throw_For_Null_Resources()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new ResourceCollection(null!))
            .Throws<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'resources')");
    }

    [Test]
    public async Task Should_Create_Empty_Collection()
    {
        // Arrange & Act
        var collection = ResourceCollection.Empty;

        // Assert
        await Assert.That(collection.Count).IsEqualTo(0);
        await Assert.That(collection.All).IsEmpty();
        await Assert.That(collection.CoverImage).IsNull();
    }

    [Test]
    public async Task Should_Extract_All_To_Directory()
    {
        // Arrange
        var resources = new List<EpubResource>
        {
            new EpubResource("img", "images/test.jpg", "image/jpeg", Encoding.UTF8.GetBytes("image")),
            new EpubResource("css", "styles/main.css", "text/css", Encoding.UTF8.GetBytes("css"))
        };
        var collection = new ResourceCollection(resources);
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        try
        {
            // Act
            await collection.ExtractAllToDirectoryAsync(tempDir);

            // Assert
            var imagePath = Path.Combine(tempDir, "images", "test.jpg");
            var cssPath = Path.Combine(tempDir, "styles", "main.css");

            await Assert.That(File.Exists(imagePath)).IsTrue();
            await Assert.That(File.Exists(cssPath)).IsTrue();

            var imageContent = await File.ReadAllTextAsync(imagePath);
            await Assert.That(imageContent).IsEqualTo("image");
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}