using System.Text;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Domain.Entities;

public class BookTests
{
    private static Book CreateTestBook(
        int chapterCount = 3,
        NavigationStructure? navigationStructure = null,
        ResourceCollection? resources = null)
    {
        var title = new BookTitle("Test Book");
        var alternateTitles = new List<BookTitle> { new("Alternate Title") };
        var authors = new List<Author> { new("John Doe", "Author") };

        var chapters = new List<Chapter>();
        for (int i = 0; i < chapterCount; i++)
        {
            var content = string.Join(" ", Enumerable.Repeat("word", 250)); // 250 words per chapter
            chapters.Add(new Chapter($"ch{i}", $"Chapter {i + 1}", $"<p>{content}</p>", i, $"ch{i}.xhtml"));
        }

        var identifiers = new List<BookIdentifier> { new("978-0-123456-78-9", "ISBN") };
        var language = new Language("en");
        var metadata = new BookMetadata();

        return new Book(title, alternateTitles, authors, chapters, identifiers, language, metadata, navigationStructure, resources);
    }

    private static NavigationStructure CreateTestNavigationStructure()
    {
        var ch1_1 = new NavigationItem("nav_ch1_1", "Section 1.1", "ch0.xhtml#s1", 2, 1);
        var ch1_2 = new NavigationItem("nav_ch1_2", "Section 1.2", "ch0.xhtml#s2", 3, 1);
        var ch1 = new NavigationItem("nav_ch1", "Chapter 1", "ch0.xhtml", 1, 0, new[] { ch1_1, ch1_2 });

        var ch2 = new NavigationItem("nav_ch2", "Chapter 2", "ch1.xhtml", 4, 0);
        var ch3 = new NavigationItem("nav_ch3", "Chapter 3", "ch2.xhtml", 5, 0);

        return new NavigationStructure("Table of Contents", new[] { ch1, ch2, ch3 });
    }

    [Test]
    public async Task Should_Create_Book_With_Required_Fields()
    {
        // Arrange & Act
        var book = CreateTestBook();

        // Assert
        await Assert.That(book.Title.Value).IsEqualTo("Test Book");
        await Assert.That(book.AlternateTitles).HasCount(1);
        await Assert.That(book.Authors).HasCount(1);
        await Assert.That(book.Chapters).HasCount(3);
        await Assert.That(book.Identifiers).HasCount(1);
        await Assert.That(book.Language.Code).IsEqualTo("en");
    }

    [Test]
    public async Task Should_Throw_When_Required_Fields_Are_Null()
    {
        // Arrange
        var title = new BookTitle("Test");
        var authors = new List<Author> { new("Author") };
        var chapters = new List<Chapter> { new("id", "Title", "Content", 0, "href") };
        var language = new Language("en");

        // Act & Assert
        await Assert.That(() => new Book(null!, new List<BookTitle>(), authors, chapters, new List<BookIdentifier>(), language, new BookMetadata()))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Book(title, new List<BookTitle>(), null!, chapters, new List<BookIdentifier>(), language, new BookMetadata()))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Book(title, new List<BookTitle>(), authors, null!, new List<BookIdentifier>(), language, new BookMetadata()))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new Book(title, new List<BookTitle>(), authors, chapters, new List<BookIdentifier>(), null!, new BookMetadata()))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Should_Throw_When_No_Chapters()
    {
        // Arrange
        var title = new BookTitle("Test");
        var authors = new List<Author> { new("Author") };
        var emptyChapters = new List<Chapter>();
        var language = new Language("en");

        // Act & Assert
        await Assert.That(() => new Book(title, new List<BookTitle>(), authors, emptyChapters, new List<BookIdentifier>(), language, new BookMetadata()))
            .Throws<ArgumentException>()
            .WithMessage("A book must have at least one chapter");
    }

    [Test]
    public async Task Should_Throw_When_No_Authors()
    {
        // Arrange
        var title = new BookTitle("Test");
        var emptyAuthors = new List<Author>();
        var chapters = new List<Chapter> { new("id", "Title", "Content", 0, "href") };
        var language = new Language("en");

        // Act & Assert
        await Assert.That(() => new Book(title, new List<BookTitle>(), emptyAuthors, chapters, new List<BookIdentifier>(), language, new BookMetadata()))
            .Throws<ArgumentException>()
            .WithMessage("A book must have at least one author");
    }

    [Test]
    public async Task Should_Calculate_Total_Word_Count()
    {
        // Arrange
        var book = CreateTestBook(4); // 4 chapters * 250 words = 1000 words

        // Act
        var totalWords = book.GetTotalWordCount();

        // Assert
        await Assert.That(totalWords).IsEqualTo(1000);
    }

    [Test]
    public async Task Should_Calculate_Estimated_Reading_Time()
    {
        // Arrange
        var book = CreateTestBook(10); // 10 chapters * 250 words = 2500 words = 10 minutes

        // Act
        var readingTime = book.GetEstimatedReadingTime();

        // Assert
        await Assert.That(readingTime.TotalMinutes).IsEqualTo(10);
    }

    [Test]
    public async Task Should_Find_Chapter_By_Id()
    {
        // Arrange
        var book = CreateTestBook(5);

        // Act
        var chapter = book.GetChapterById("ch2");

        // Assert
        await Assert.That(chapter).IsNotNull();
        await Assert.That(chapter!.Title).IsEqualTo("Chapter 3");
        await Assert.That(chapter.Order).IsEqualTo(2);
    }

    [Test]
    public async Task Should_Return_Null_When_Chapter_Not_Found()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var chapter = book.GetChapterById("non-existent");

        // Assert
        await Assert.That(chapter).IsNull();
    }

    [Test]
    public async Task Should_Get_Chapters_In_Order()
    {
        // Arrange
        var title = new BookTitle("Test Book");
        var authors = new List<Author> { new("Author") };
        var language = new Language("en");

        // Create chapters in random order
        var chapters = new List<Chapter>
        {
            new("ch2", "Chapter 3", "Content", 2, "ch2.xhtml"),
            new("ch0", "Chapter 1", "Content", 0, "ch0.xhtml"),
            new("ch1", "Chapter 2", "Content", 1, "ch1.xhtml")
        };

        var book = new Book(title, new List<BookTitle>(), authors, chapters, new List<BookIdentifier>(), language, new BookMetadata());

        // Act - Chapters should be automatically sorted by Order
        var orderedChapters = book.Chapters;

        // Assert
        await Assert.That(orderedChapters[0].Title).IsEqualTo("Chapter 1");
        await Assert.That(orderedChapters[1].Title).IsEqualTo("Chapter 2");
        await Assert.That(orderedChapters[2].Title).IsEqualTo("Chapter 3");
    }

    [Test]
    public async Task Should_Have_Default_Empty_Collections()
    {
        // Arrange
        var title = new BookTitle("Test");
        var authors = new List<Author> { new("Author") };
        var chapters = new List<Chapter> { new("id", "Title", "Content", 0, "href") };
        var language = new Language("en");

        // Act - Create book with empty collections for optional fields
        var book = new Book(
            title,
            new List<BookTitle>(), // empty alternate titles
            authors,
            chapters,
            new List<BookIdentifier>(), // empty identifiers
            language,
            new BookMetadata()
        );

        // Assert
        await Assert.That(book.AlternateTitles).IsNotNull();
        await Assert.That(book.AlternateTitles).HasCount(0);
        await Assert.That(book.Identifiers).IsNotNull();
        await Assert.That(book.Identifiers).HasCount(0);
    }

    [Test]
    public async Task Should_Access_Metadata_Properties()
    {
        // Arrange
        var customMetadata = new Dictionary<string, string>
        {
            ["custom1"] = "value1",
            ["custom2"] = "value2"
        };

        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: new DateTime(2024, 1, 1),
            description: "Test Description",
            rights: "All rights reserved",
            subject: "Fiction",
            coverage: "Global",
            customMetadata: customMetadata
        );

        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            new List<Chapter> { new("id", "Title", "Content", 0, "href") },
            new List<BookIdentifier>(),
            new Language("en"),
            metadata
        );

        // Assert
        await Assert.That(book.Metadata.Publisher).IsEqualTo("Test Publisher");
        await Assert.That(book.Metadata.PublicationDate).IsEqualTo(new DateTime(2024, 1, 1));
        await Assert.That(book.Metadata.Description).IsEqualTo("Test Description");
        await Assert.That(book.Metadata.CustomMetadata).HasCount(2);
    }

    [Test]
    public async Task Should_Get_Table_Of_Contents()
    {
        // Arrange
        var navigation = CreateTestNavigationStructure();
        var book = CreateTestBook(navigationStructure: navigation);

        // Act
        var toc = book.GetTableOfContents();

        // Assert
        await Assert.That(toc).IsNotNull();
        await Assert.That(toc!.Title).IsEqualTo("Table of Contents");
        await Assert.That(toc.Items).HasCount(3);
    }

    [Test]
    public async Task Should_Return_Null_When_No_Navigation()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var toc = book.GetTableOfContents();

        // Assert
        await Assert.That(toc).IsNull();
    }

    [Test]
    public async Task Should_Set_Navigation_Structure()
    {
        // Arrange
        var book = CreateTestBook();
        var navigation = CreateTestNavigationStructure();

        // Act
        book.SetNavigationStructure(navigation);
        var toc = book.GetTableOfContents();

        // Assert
        await Assert.That(toc).IsNotNull();
        await Assert.That(toc).IsEqualTo(navigation);
    }

    [Test]
    public async Task Should_Throw_When_Setting_Navigation_Twice()
    {
        // Arrange
        var navigation1 = CreateTestNavigationStructure();
        var navigation2 = CreateTestNavigationStructure();
        var book = CreateTestBook(navigationStructure: navigation1);

        // Act & Assert
        await Assert.That(() => book.SetNavigationStructure(navigation2))
            .Throws<InvalidOperationException>()
            .WithMessage("Navigation structure has already been set");
    }

    [Test]
    public async Task Should_Navigate_To_Chapter_By_Href()
    {
        // Arrange
        var navigation = CreateTestNavigationStructure();
        var book = CreateTestBook(navigationStructure: navigation);

        // Act
        var chapter = book.NavigateToChapter("ch1.xhtml");

        // Assert
        await Assert.That(chapter).IsNotNull();
        await Assert.That(chapter!.Href).IsEqualTo("ch1.xhtml");
        await Assert.That(chapter.Title).IsEqualTo("Chapter 2");
    }

    [Test]
    public async Task Should_Navigate_To_Chapter_By_Href_With_Fragment()
    {
        // Arrange
        var navigation = CreateTestNavigationStructure();
        var book = CreateTestBook(navigationStructure: navigation);

        // Act
        var chapter = book.NavigateToChapter("ch0.xhtml#s1");

        // Assert
        await Assert.That(chapter).IsNotNull();
        await Assert.That(chapter!.Href).IsEqualTo("ch0.xhtml");
        await Assert.That(chapter.Title).IsEqualTo("Chapter 1");
    }

    [Test]
    public async Task Should_Return_Null_For_Invalid_TocRef()
    {
        // Arrange
        var navigation = CreateTestNavigationStructure();
        var book = CreateTestBook(navigationStructure: navigation);

        // Act
        var chapter = book.NavigateToChapter("nonexistent.xhtml");

        // Assert
        await Assert.That(chapter).IsNull();
    }

    [Test]
    public async Task Should_Get_Next_Chapter()
    {
        // Arrange
        var book = CreateTestBook(3);
        var firstChapter = book.Chapters[0];

        // Act
        var nextChapter = book.GetNextChapter(firstChapter);

        // Assert
        await Assert.That(nextChapter).IsNotNull();
        await Assert.That(nextChapter!.Title).IsEqualTo("Chapter 2");
    }

    [Test]
    public async Task Should_Return_Null_For_Last_Chapter_Next()
    {
        // Arrange
        var book = CreateTestBook(3);
        var lastChapter = book.Chapters[2];

        // Act
        var nextChapter = book.GetNextChapter(lastChapter);

        // Assert
        await Assert.That(nextChapter).IsNull();
    }

    [Test]
    public async Task Should_Get_Previous_Chapter()
    {
        // Arrange
        var book = CreateTestBook(3);
        var secondChapter = book.Chapters[1];

        // Act
        var prevChapter = book.GetPreviousChapter(secondChapter);

        // Assert
        await Assert.That(prevChapter).IsNotNull();
        await Assert.That(prevChapter!.Title).IsEqualTo("Chapter 1");
    }

    [Test]
    public async Task Should_Return_Null_For_First_Chapter_Previous()
    {
        // Arrange
        var book = CreateTestBook(3);
        var firstChapter = book.Chapters[0];

        // Act
        var prevChapter = book.GetPreviousChapter(firstChapter);

        // Assert
        await Assert.That(prevChapter).IsNull();
    }

    [Test]
    public async Task Should_Get_First_Chapter_When_Current_Is_Null()
    {
        // Arrange
        var book = CreateTestBook(3);

        // Act
        var nextChapter = book.GetNextChapter(null);

        // Assert
        await Assert.That(nextChapter).IsNotNull();
        await Assert.That(nextChapter!.Title).IsEqualTo("Chapter 1");
    }

    [Test]
    public async Task Should_Get_Resources()
    {
        // Arrange
        var resources = new[]
        {
            new EpubResource("style1", "styles.css", "text/css"),
            new ImageResource("img1", "cover.jpg", "image/jpeg", null, null, true)
        };
        var resourceCollection = new ResourceCollection(resources);
        var book = CreateTestBook(resources: resourceCollection);

        // Act
        var result = book.GetResources();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(resourceCollection);
    }

    [Test]
    public async Task Should_Return_Null_When_No_Resources()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var result = book.GetResources();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Should_Set_Resources()
    {
        // Arrange
        var book = CreateTestBook();
        var resources = new[]
        {
            new EpubResource("font1", "font.ttf", "font/ttf"),
            new ImageResource("img1", "image.png", "image/png")
        };
        var resourceCollection = new ResourceCollection(resources);

        // Act
        book.SetResources(resourceCollection);
        var result = book.GetResources();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(resourceCollection);
    }

    [Test]
    public async Task Should_Throw_When_Setting_Resources_Twice()
    {
        // Arrange
        var resources1 = new ResourceCollection(new[] { new EpubResource("r1", "file1.css", "text/css") });
        var resources2 = new ResourceCollection(new[] { new EpubResource("r2", "file2.css", "text/css") });
        var book = CreateTestBook(resources: resources1);

        // Act & Assert
        await Assert.That(() => book.SetResources(resources2))
            .Throws<InvalidOperationException>()
            .WithMessage("Resources have already been set");
    }

    [Test]
    public async Task Should_Get_Cover_Image()
    {
        // Arrange
        var coverImage = new ImageResource("cover", "cover.jpg", "image/jpeg", null, null, true);
        var otherImage = new ImageResource("img1", "image.png", "image/png");
        var resources = new ResourceCollection(new EpubResource[] { coverImage, otherImage });
        var book = CreateTestBook(resources: resources);

        // Act
        var result = book.GetCoverImage();

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsEqualTo(coverImage);
        await Assert.That(result!.IsCoverImage).IsTrue();
    }

    [Test]
    public async Task Should_Return_Null_When_No_Cover_Image()
    {
        // Arrange
        var resources = new ResourceCollection(new[]
        {
            new ImageResource("img1", "image1.png", "image/png"),
            new ImageResource("img2", "image2.jpg", "image/jpeg")
        });
        var book = CreateTestBook(resources: resources);

        // Act
        var result = book.GetCoverImage();

        // Assert
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Should_Get_All_Images()
    {
        // Arrange
        var image1 = new ImageResource("img1", "image1.png", "image/png");
        var image2 = new ImageResource("img2", "image2.jpg", "image/jpeg");
        var coverImage = new ImageResource("cover", "cover.jpg", "image/jpeg", null, null, true);
        var cssResource = new EpubResource("style", "styles.css", "text/css");

        var resources = new ResourceCollection(new EpubResource[] { image1, cssResource, image2, coverImage });
        var book = CreateTestBook(resources: resources);

        // Act
        var images = book.GetImages().ToList();

        // Assert
        await Assert.That(images).HasCount(3);
        await Assert.That(images).Contains(image1);
        await Assert.That(images).Contains(image2);
        await Assert.That(images).Contains(coverImage);
    }

    [Test]
    public async Task Should_Return_Empty_When_No_Images()
    {
        // Arrange
        var resources = new ResourceCollection(new[]
        {
            new EpubResource("style", "styles.css", "text/css"),
            new EpubResource("font", "font.ttf", "font/ttf")
        });
        var book = CreateTestBook(resources: resources);

        // Act
        var images = book.GetImages();

        // Assert
        await Assert.That(images).HasCount(0);
    }

    [Test]
    public async Task Should_Get_Stylesheets()
    {
        // Arrange
        var css1 = new EpubResource("style1", "main.css", "text/css");
        var css2 = new EpubResource("style2", "theme.css", "text/css");
        var image = new ImageResource("img", "image.png", "image/png");

        var resources = new ResourceCollection(new[] { css1, image, css2 });
        var book = CreateTestBook(resources: resources);

        // Act
        var stylesheets = book.GetStylesheets().ToList();

        // Assert
        await Assert.That(stylesheets).HasCount(2);
        await Assert.That(stylesheets).Contains(css1);
        await Assert.That(stylesheets).Contains(css2);
    }

    [Test]
    public async Task Should_Get_Fonts()
    {
        // Arrange
        var font1 = new EpubResource("font1", "regular.ttf", "font/ttf");
        var font2 = new EpubResource("font2", "bold.otf", "font/otf");
        var css = new EpubResource("style", "styles.css", "text/css");

        var resources = new ResourceCollection(new[] { font1, css, font2 });
        var book = CreateTestBook(resources: resources);

        // Act
        var fonts = book.GetFonts().ToList();

        // Assert
        await Assert.That(fonts).HasCount(2);
        await Assert.That(fonts).Contains(font1);
        await Assert.That(fonts).Contains(font2);
    }

    [Test]
    public async Task Should_Extract_Resources_To_Directory()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test_extract_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var imageContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG header
            var cssContent = Encoding.UTF8.GetBytes("body { font-size: 16px; }");

            var image = new ImageResource("img", "cover.jpg", "image/jpeg", imageContent);
            var css = new EpubResource("style", "styles.css", "text/css", cssContent);

            var resources = new ResourceCollection(new[] { image, css });
            var book = CreateTestBook(resources: resources);

            // Act
            await book.ExtractResourcesToDirectoryAsync(tempDir);

            // Assert
            var imagePath = Path.Combine(tempDir, "cover.jpg");
            var cssPath = Path.Combine(tempDir, "styles.css");

            await Assert.That(File.Exists(imagePath)).IsTrue();
            await Assert.That(File.Exists(cssPath)).IsTrue();

            var extractedImage = await File.ReadAllBytesAsync(imagePath);
            var extractedCss = await File.ReadAllBytesAsync(cssPath);

            await Assert.That(extractedImage).IsEqualTo(imageContent);
            await Assert.That(extractedCss).IsEqualTo(cssContent);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Test]
    public async Task Should_Throw_When_Extracting_With_No_Resources()
    {
        // Arrange
        var book = CreateTestBook();
        var tempDir = Path.GetTempPath();

        // Act & Assert
        await Assert.That(async () => await book.ExtractResourcesToDirectoryAsync(tempDir))
            .Throws<InvalidOperationException>()
            .WithMessage("No resources to extract");
    }

    #region Content Processing Tests

    [Test]
    public async Task Should_Search_For_Term_In_Book()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "Chapter 1", "<p>The quick brown fox jumps.</p>", 0, "ch1.xhtml"),
            new("ch2", "Chapter 2", "<p>The lazy dog sleeps.</p>", 1, "ch2.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var results = book.Search("fox").ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
    }

    [Test]
    public async Task Should_Get_Chapter_Plain_Text()
    {
        // Arrange
        var book = CreateTestBook();
        var chapterId = "ch0";

        // Act
        var plainText = book.GetChapterPlainText(chapterId);

        // Assert
        await Assert.That(plainText).Contains("word");
        await Assert.That(plainText).DoesNotContain("<p>");
        await Assert.That(plainText).DoesNotContain("</p>");
    }

    [Test]
    public async Task Should_Return_Empty_For_Invalid_Chapter_Id()
    {
        // Arrange
        var book = CreateTestBook();

        // Act
        var plainText = book.GetChapterPlainText("invalid");

        // Assert
        await Assert.That(plainText).IsEmpty();
    }

    [Test]
    public async Task Should_Get_Full_Plain_Text()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>First chapter.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Second chapter.</p>", 1, "ch2.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var fullText = book.GetFullPlainText();

        // Assert
        await Assert.That(fullText).Contains("First chapter");
        await Assert.That(fullText).Contains("Second chapter");
        await Assert.That(fullText).Contains("\n\n"); // Chapters separated by double newline
    }

    [Test]
    public async Task Should_Get_Book_Preview()
    {
        // Arrange
        var longContent = string.Join(" ", Enumerable.Repeat("word", 200));
        var chapters = new List<Chapter>
        {
            new("ch1", "One", $"<p>{longContent}</p>", 0, "ch1.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var preview = book.GetPreview(100);

        // Assert
        await Assert.That(preview.Length).IsLessThanOrEqualTo(103); // 100 + "..."
        await Assert.That(preview).EndsWith("...");
    }

    [Test]
    public async Task Should_Get_Reading_Statistics()
    {
        // Arrange - 3 chapters with 250 words each
        var book = CreateTestBook(3);

        // Act
        var stats = book.GetReadingStatistics(250);

        // Assert
        await Assert.That(stats.TotalWords).IsEqualTo(750);
        await Assert.That(stats.ChapterStatistics).HasCount(3);
        await Assert.That(stats.AverageWordsPerChapter).IsEqualTo(250);
        await Assert.That(stats.TotalReadingTime.TotalMinutes).IsEqualTo(3.0);
    }

    [Test]
    public async Task Should_Find_Chapters_With_Term()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>Contains special term here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Does not contain it.</p>", 1, "ch2.xhtml"),
            new("ch3", "Three", "<p>Has special term again.</p>", 2, "ch3.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var foundChapters = book.FindChaptersWithTerm("special").ToList();

        // Assert
        await Assert.That(foundChapters).HasCount(2);
        await Assert.That(foundChapters[0].Id).IsEqualTo("ch1");
        await Assert.That(foundChapters[1].Id).IsEqualTo("ch3");
    }

    [Test]
    public async Task Should_Search_All_Terms_With_AND_Logic()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>Has both cat and dog here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Only has cat.</p>", 1, "ch2.xhtml"),
            new("ch3", "Three", "<p>Only has dog.</p>", 2, "ch3.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var results = book.SearchAll(new[] { "cat", "dog" }).ToList();

        // Assert
        await Assert.That(results).HasCount(1);
        await Assert.That(results[0].Chapter.Id).IsEqualTo("ch1");
    }

    [Test]
    public async Task Should_Search_Any_Terms_With_OR_Logic()
    {
        // Arrange
        var chapters = new List<Chapter>
        {
            new("ch1", "One", "<p>Has cat here.</p>", 0, "ch1.xhtml"),
            new("ch2", "Two", "<p>Has dog here.</p>", 1, "ch2.xhtml"),
            new("ch3", "Three", "<p>Has neither.</p>", 2, "ch3.xhtml")
        };
        var book = new Book(
            new BookTitle("Test"),
            new List<BookTitle>(),
            new List<Author> { new("Author") },
            chapters,
            new List<BookIdentifier>(),
            new Language("en"),
            new BookMetadata()
        );

        // Act
        var results = book.SearchAny(new[] { "cat", "dog" }).ToList();

        // Assert
        await Assert.That(results).HasCount(2);
        await Assert.That(results.Any(r => r.Chapter.Id == "ch1")).IsTrue();
        await Assert.That(results.Any(r => r.Chapter.Id == "ch2")).IsTrue();
    }

    #endregion
}