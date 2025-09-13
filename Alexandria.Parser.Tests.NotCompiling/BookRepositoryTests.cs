using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Domain.ValueObjects;
using Alexandria.Parser.Infrastructure.Parsers;
using Alexandria.Parser.Infrastructure.Repositories;
using Alexandria.Parser.Tests.Utilities;
using NSubstitute;
using OneOf;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Infrastructure.Repositories;

public class BookRepositoryTests
{
    private readonly IEpubParser _parser;
    private readonly BookRepository _repository;

    public BookRepositoryTests()
    {
        _parser = Substitute.For<IEpubParser>();
        _repository = new BookRepository(_parser);
    }

    [Test]
    public async Task Should_Load_Book_Successfully()
    {
        // Arrange
        var filePath = "/path/to/book.epub";
        var expectedBook = CreateTestBook();
        _parser.ParseAsync(Arg.Any<Stream>())
            .Returns(OneOf<Book, ParseError>.FromT0(expectedBook));

        // Act
        var result = await _repository.LoadBookAsync(filePath);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo(expectedBook.Title.Value);
    }

    [Test]
    public async Task Should_Return_FileNotFoundError_When_File_Does_Not_Exist()
    {
        // Arrange
        var filePath = "/non/existent/file.epub";

        // Act
        var result = await _repository.LoadBookAsync(filePath);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error).IsOfType<FileNotFoundError>();
        await Assert.That(error.Message).Contains("not found");
    }

    [Test]
    public async Task Should_Return_ParseError_When_Parser_Fails()
    {
        // Arrange
        var filePath = "test.epub";
        var parseError = new ParseError("Invalid EPUB format", "PARSE_001");
        _parser.ParseAsync(Arg.Any<Stream>())
            .Returns(OneOf<Book, ParseError>.FromT1(parseError));

        // Create a temporary test file
        await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });

        try
        {
            // Act
            var result = await _repository.LoadBookAsync(filePath);

            // Assert
            await Assert.That(result.IsT1).IsTrue();
            var error = result.AsT1;
            await Assert.That(error).IsOfType<ParseError>();
            await Assert.That(error.Message).IsEqualTo("Invalid EPUB format");
            await Assert.That(((ParseError)error).Code).IsEqualTo("PARSE_001");
        }
        finally
        {
            // Clean up
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    public async Task Should_Load_Book_From_Stream()
    {
        // Arrange
        using var stream = TestEpubBuilder.CreateMinimalEpub();
        var expectedBook = CreateTestBook();
        _parser.ParseAsync(Arg.Any<Stream>())
            .Returns(OneOf<Book, ParseError>.FromT0(expectedBook));

        // Act
        var result = await _repository.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo(expectedBook.Title.Value);
    }

    [Test]
    public async Task Should_Handle_IOException_During_File_Read()
    {
        // Arrange
        var filePath = "locked.epub";

        // Create a file and lock it
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        fileStream.Write(new byte[] { 1, 2, 3 }, 0, 3);

        try
        {
            // Act - Try to read the locked file
            var result = await _repository.LoadBookAsync(filePath);

            // Assert
            await Assert.That(result.IsT1).IsTrue();
            var error = result.AsT1;
            await Assert.That(error).IsOfType<FileAccessError>();
        }
        finally
        {
            // Clean up
            fileStream.Close();
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Test]
    public async Task Should_Parse_Real_Epub_With_Real_Parser()
    {
        // Arrange
        var realParser = new AdaptiveEpubParser(new Epub2Parser(), new Epub3Parser());
        var realRepository = new BookRepository(realParser);
        using var stream = TestEpubBuilder.CreateMinimalEpub("2.0.1");

        // Act
        var result = await realRepository.LoadBookAsync(stream);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Minimal Test Book");
        await Assert.That(book.Authors).HasCount(1);
        await Assert.That(book.Chapters).HasCount(1);
    }

    [Test]
    public async Task Should_Load_Multiple_Books_Concurrently()
    {
        // Arrange
        var book1 = CreateTestBook("Book 1");
        var book2 = CreateTestBook("Book 2");
        var book3 = CreateTestBook("Book 3");

        _parser.ParseAsync(Arg.Any<Stream>())
            .Returns(
                OneOf<Book, ParseError>.FromT0(book1),
                OneOf<Book, ParseError>.FromT0(book2),
                OneOf<Book, ParseError>.FromT0(book3)
            );

        using var stream1 = TestEpubBuilder.CreateMinimalEpub();
        using var stream2 = TestEpubBuilder.CreateMinimalEpub();
        using var stream3 = TestEpubBuilder.CreateMinimalEpub();

        // Act
        var tasks = new[]
        {
            _repository.LoadBookAsync(stream1),
            _repository.LoadBookAsync(stream2),
            _repository.LoadBookAsync(stream3)
        };

        var results = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(results).HasCount(3);
        await Assert.That(results.All(r => r.IsT0)).IsTrue();
        await Assert.That(results[0].AsT0.Title.Value).IsEqualTo("Book 1");
        await Assert.That(results[1].AsT0.Title.Value).IsEqualTo("Book 2");
        await Assert.That(results[2].AsT0.Title.Value).IsEqualTo("Book 3");
    }

    [Test]
    public async Task Should_Validate_File_Extension()
    {
        // Arrange
        var invalidFile = "test.txt";
        await File.WriteAllTextAsync(invalidFile, "Not an EPUB");

        try
        {
            // Act
            var result = await _repository.LoadBookAsync(invalidFile);

            // Assert
            await Assert.That(result.IsT1).IsTrue();
            var error = result.AsT1;
            await Assert.That(error).IsOfType<ValidationError>();
            await Assert.That(error.Message).Contains("EPUB");
        }
        finally
        {
            if (File.Exists(invalidFile))
                File.Delete(invalidFile);
        }
    }

    [Test]
    public async Task Should_Handle_Empty_Stream()
    {
        // Arrange
        using var emptyStream = new MemoryStream();
        var parseError = new ParseError("Empty EPUB file", "EMPTY_FILE");
        _parser.ParseAsync(Arg.Any<Stream>())
            .Returns(OneOf<Book, ParseError>.FromT1(parseError));

        // Act
        var result = await _repository.LoadBookAsync(emptyStream);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var error = result.AsT1;
        await Assert.That(error).IsOfType<ParseError>();
    }

    private static Book CreateTestBook(string title = "Test Book")
    {
        var bookTitle = new BookTitle(title);
        var authors = new List<Author> { new("Test Author") };
        var chapters = new List<Chapter>
        {
            new("ch1", "Chapter 1", "<p>Content</p>", 0, "ch1.xhtml")
        };
        var language = new Language("en");

        return new Book(
            bookTitle,
            new List<BookTitle>(),
            authors,
            chapters,
            new List<BookIdentifier>(),
            language,
            new BookMetadata()
        );
    }
}