using Alexandria.Parser.Application.UseCases.LoadBook;
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.Errors;
using Alexandria.Parser.Domain.Interfaces;
using Alexandria.Parser.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OneOf;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Application.UseCases;

public class LoadBookHandlerTests
{
    private readonly IBookRepository _repository;
    private readonly ILogger<LoadBookHandler> _logger;
    private readonly LoadBookHandler _handler;

    public LoadBookHandlerTests()
    {
        _repository = Substitute.For<IBookRepository>();
        _logger = Substitute.For<ILogger<LoadBookHandler>>();
        _handler = new LoadBookHandler(_repository, _logger);
    }

    [Test]
    public async Task Should_Load_Book_From_File_Path()
    {
        // Arrange
        var filePath = "/path/to/book.epub";
        var expectedBook = CreateTestBook();
        _repository.LoadBookAsync(filePath)
            .Returns(OneOf<Book, DomainError>.FromT0(expectedBook));

        var request = new LoadBookRequest(filePath);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Test Book");
        await _repository.Received(1).LoadBookAsync(filePath);
    }

    [Test]
    public async Task Should_Load_Book_From_Stream()
    {
        // Arrange
        using var stream = new MemoryStream();
        var expectedBook = CreateTestBook();
        _repository.LoadBookAsync(stream)
            .Returns(OneOf<Book, DomainError>.FromT0(expectedBook));

        var request = new LoadBookRequest(stream);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Test Book");
        await _repository.Received(1).LoadBookAsync(stream);
    }

    [Test]
    public async Task Should_Return_Error_When_Repository_Fails()
    {
        // Arrange
        var filePath = "/path/to/invalid.epub";
        var error = new ParseError("Invalid format", "PARSE_001");
        _repository.LoadBookAsync(filePath)
            .Returns(OneOf<Book, DomainError>.FromT1(error));

        var request = new LoadBookRequest(filePath);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var returnedError = result.AsT1;
        await Assert.That(returnedError).IsOfType<ParseError>();
        await Assert.That(returnedError.Message).IsEqualTo("Invalid format");
    }

    [Test]
    public async Task Should_Return_FileNotFoundError()
    {
        // Arrange
        var filePath = "/non/existent/file.epub";
        var error = new FileNotFoundError(filePath);
        _repository.LoadBookAsync(filePath)
            .Returns(OneOf<Book, DomainError>.FromT1(error));

        var request = new LoadBookRequest(filePath);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var returnedError = result.AsT1;
        await Assert.That(returnedError).IsOfType<FileNotFoundError>();
        await Assert.That(((FileNotFoundError)returnedError).FilePath).IsEqualTo(filePath);
    }

    [Test]
    public async Task Should_Return_ValidationError_For_Invalid_Extension()
    {
        // Arrange
        var filePath = "/path/to/file.txt";
        var error = new ValidationError("File must have .epub extension");
        _repository.LoadBookAsync(filePath)
            .Returns(OneOf<Book, DomainError>.FromT1(error));

        var request = new LoadBookRequest(filePath);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var returnedError = result.AsT1;
        await Assert.That(returnedError).IsOfType<ValidationError>();
        await Assert.That(returnedError.Message).Contains("epub");
    }

    [Test]
    public async Task Should_Handle_Null_Request()
    {
        // Act & Assert
        await Assert.That(() => _handler.Handle(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Should_Handle_Request_With_Null_Path_And_Stream()
    {
        // Arrange
        var request = new LoadBookRequest((string)null!);
        var error = new ValidationError("Either file path or stream must be provided");
        _repository.LoadBookAsync((string)null!)
            .Returns(OneOf<Book, DomainError>.FromT1(error));

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        await Assert.That(result.AsT1).IsOfType<ValidationError>();
    }

    [Test]
    public async Task Should_Load_Book_With_Complex_Metadata()
    {
        // Arrange
        var filePath = "/path/to/complex.epub";
        var complexBook = CreateComplexBook();
        _repository.LoadBookAsync(filePath)
            .Returns(OneOf<Book, DomainError>.FromT0(complexBook));

        var request = new LoadBookRequest(filePath);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT0).IsTrue();
        var book = result.AsT0;
        await Assert.That(book.Title.Value).IsEqualTo("Complex Book");
        await Assert.That(book.Authors).HasCount(3);
        await Assert.That(book.Chapters).HasCount(10);
        await Assert.That(book.Identifiers).HasCount(2);
        await Assert.That(book.Language.Code).IsEqualTo("en-US");
        await Assert.That(book.Metadata.Publisher).IsEqualTo("Test Publisher");
    }

    [Test]
    public async Task Should_Process_Multiple_Requests_Concurrently()
    {
        // Arrange
        var book1 = CreateTestBook("Book 1");
        var book2 = CreateTestBook("Book 2");
        var book3 = CreateTestBook("Book 3");

        _repository.LoadBookAsync("book1.epub")
            .Returns(OneOf<Book, DomainError>.FromT0(book1));
        _repository.LoadBookAsync("book2.epub")
            .Returns(OneOf<Book, DomainError>.FromT0(book2));
        _repository.LoadBookAsync("book3.epub")
            .Returns(OneOf<Book, DomainError>.FromT0(book3));

        var requests = new[]
        {
            new LoadBookRequest("book1.epub"),
            new LoadBookRequest("book2.epub"),
            new LoadBookRequest("book3.epub")
        };

        // Act
        var tasks = requests.Select(r => _handler.Handle(r));
        var results = await Task.WhenAll(tasks);

        // Assert
        await Assert.That(results).HasCount(3);
        await Assert.That(results.All(r => r.IsT0)).IsTrue();
        await Assert.That(results[0].AsT0.Title.Value).IsEqualTo("Book 1");
        await Assert.That(results[1].AsT0.Title.Value).IsEqualTo("Book 2");
        await Assert.That(results[2].AsT0.Title.Value).IsEqualTo("Book 3");
    }

    [Test]
    public async Task Should_Return_FileAccessError_When_File_Is_Locked()
    {
        // Arrange
        var filePath = "/path/to/locked.epub";
        var error = new FileAccessError("File is locked by another process", filePath);
        _repository.LoadBookAsync(filePath)
            .Returns(OneOf<Book, DomainError>.FromT1(error));

        var request = new LoadBookRequest(filePath);

        // Act
        var result = await _handler.Handle(request);

        // Assert
        await Assert.That(result.IsT1).IsTrue();
        var returnedError = result.AsT1;
        await Assert.That(returnedError).IsOfType<FileAccessError>();
        await Assert.That(((FileAccessError)returnedError).FilePath).IsEqualTo(filePath);
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

    private static Book CreateComplexBook()
    {
        var title = new BookTitle("Complex Book");
        var alternateTitles = new List<BookTitle>
        {
            new("Alternate Title 1"),
            new("Subtitle")
        };

        var authors = new List<Author>
        {
            new("Primary Author", "Author", "Author, Primary"),
            new("Secondary Author", "Editor"),
            new("Third Author", "Translator")
        };

        var chapters = new List<Chapter>();
        for (int i = 0; i < 10; i++)
        {
            chapters.Add(new Chapter(
                $"ch{i}",
                $"Chapter {i + 1}",
                $"<p>Content of chapter {i + 1}</p>",
                i,
                $"ch{i}.xhtml"
            ));
        }

        var identifiers = new List<BookIdentifier>
        {
            new("978-0-123456-78-9", "ISBN"),
            new("550e8400-e29b-41d4-a716-446655440000", "UUID")
        };

        var language = new Language("en-US");

        var metadata = new BookMetadata(
            publisher: "Test Publisher",
            publicationDate: new DateTime(2024, 1, 1),
            description: "A complex test book",
            rights: "All rights reserved",
            subject: "Fiction",
            coverage: "Global",
            customMetadata: new Dictionary<string, string>
            {
                ["custom1"] = "value1",
                ["custom2"] = "value2"
            }
        );

        return new Book(
            title,
            alternateTitles,
            authors,
            chapters,
            identifiers,
            language,
            metadata
        );
    }
}