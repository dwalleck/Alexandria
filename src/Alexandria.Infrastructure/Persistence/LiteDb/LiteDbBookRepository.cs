using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain;
using Alexandria.Domain.Common;
using Alexandria.Domain.Entities;
using Alexandria.Domain.Repositories;
using Alexandria.Infrastructure.Persistence.LiteDb.Mappers;
using Alexandria.Infrastructure.Persistence.LiteDb.Models;
using LiteDB;

namespace Alexandria.Infrastructure.Persistence.LiteDb;

/// <summary>
/// LiteDB implementation of IBookRepository.
/// Provides high-performance persistence for Book entities using LiteDB document database.
/// Uses DTOs internally for proper LiteDB serialization.
/// </summary>
public sealed class LiteDbBookRepository : IBookRepository
{
    private readonly LiteDbContext _context;
    private readonly ILiteCollection<BookDto> _books;

    public LiteDbBookRepository(LiteDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _books = _context.Database.GetCollection<BookDto>("books");
    }

    public ValueTask<Book?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dto = _books.FindById(id);
        var book = dto != null ? BookMapper.FromDto(dto) : null;
        return ValueTask.FromResult(book);
    }

    public ValueTask<Book?> GetByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        cancellationToken.ThrowIfCancellationRequested();

        var dto = _books.FindOne(b => b.Metadata.Isbn == isbn);
        var book = dto != null ? BookMapper.FromDto(dto) : null;
        return ValueTask.FromResult(book);
    }

    public Task<IReadOnlyList<Book>> GetByAuthorAsync(string authorName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorName);
        cancellationToken.ThrowIfCancellationRequested();

        // LiteDB doesn't support nested .Any() calls in LINQ, so we need to load all books and filter in memory
        var dtos = _books.FindAll();
        var matchingDtos = dtos.Where(b => b.Authors.Any(a => a.Name.Contains(authorName, StringComparison.OrdinalIgnoreCase)));

        var books = matchingDtos.Select(BookMapper.FromDto).ToList();
        return Task.FromResult<IReadOnlyList<Book>>(books);
    }

    public Task<IReadOnlyList<Book>> GetAllAsync(ISpecification<Book>? specification = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dtos = _books.FindAll();
        var books = dtos.Select(BookMapper.FromDto);

        if (specification != null)
        {
            books = books.Where(specification.IsSatisfiedBy);
        }

        return Task.FromResult<IReadOnlyList<Book>>(books.ToList());
    }

    public Task<PagedResult<Book>> GetPagedAsync(
        int pageNumber,
        int pageSize,
        ISpecification<Book>? specification = null,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be greater than 0");
        if (pageSize < 1)
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be greater than 0");

        cancellationToken.ThrowIfCancellationRequested();

        var dtos = _books.FindAll();
        var allBooks = dtos.Select(BookMapper.FromDto);

        if (specification != null)
        {
            allBooks = allBooks.Where(specification.IsSatisfiedBy);
        }

        var totalCount = allBooks.Count();
        var items = allBooks
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Book>(items, totalCount, pageNumber, pageSize));
    }

    public async IAsyncEnumerable<Book> StreamAsync(
        ISpecification<Book>? specification = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dtos = _books.FindAll();
        var books = dtos.Select(BookMapper.FromDto);

        if (specification != null)
        {
            books = books.Where(specification.IsSatisfiedBy);
        }

        foreach (var book in books)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return book;
            await Task.Yield(); // Allow other tasks to run
        }
    }

    public Task<Book> AddAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        // Store cover image in FileStorage if available
        if (book.GetCoverImage() is { } coverImage && coverImage.IsContentLoaded)
        {
            var fileStorage = _context.Database.FileStorage;
            using var stream = new System.IO.MemoryStream(coverImage.Content);
            fileStorage.Upload($"covers/{book.Id}", $"cover_{book.Id}{System.IO.Path.GetExtension(coverImage.Href)}", stream);
        }

        var dto = BookMapper.ToDto(book);
        _books.Insert(dto);
        return Task.FromResult(book);
    }

    public Task<IReadOnlyList<Book>> AddRangeAsync(IEnumerable<Book> books, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(books);
        cancellationToken.ThrowIfCancellationRequested();

        var bookList = books.ToList();
        var dtos = bookList.Select(BookMapper.ToDto);
        _books.InsertBulk(dtos);
        return Task.FromResult<IReadOnlyList<Book>>(bookList);
    }

    public Task<Book> UpdateAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        var dto = BookMapper.ToDto(book);
        _books.Update(dto);
        return Task.FromResult(book);
    }

    public Task<IReadOnlyList<Book>> UpdateRangeAsync(IEnumerable<Book> books, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(books);
        cancellationToken.ThrowIfCancellationRequested();

        var bookList = books.ToList();
        foreach (var book in bookList)
        {
            var dto = BookMapper.ToDto(book);
            _books.Update(dto);
        }
        return Task.FromResult<IReadOnlyList<Book>>(bookList);
    }

    public Task<bool> RemoveAsync(Book book, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(book);
        cancellationToken.ThrowIfCancellationRequested();

        var result = _books.Delete(book.Id);

        // Clean up cover image if exists
        if (result)
        {
            var fileStorage = _context.Database.FileStorage;
            if (fileStorage.Exists($"covers/{book.Id}"))
            {
                fileStorage.Delete($"covers/{book.Id}");
            }
        }

        return Task.FromResult(result);
    }

    public Task<bool> RemoveByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = _books.Delete(id);

        // Clean up cover image if exists
        if (result)
        {
            var fileStorage = _context.Database.FileStorage;
            if (fileStorage.Exists($"covers/{id}"))
            {
                fileStorage.Delete($"covers/{id}");
            }
        }

        return Task.FromResult(result);
    }

    public Task<int> RemoveRangeAsync(IEnumerable<Book> books, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(books);
        cancellationToken.ThrowIfCancellationRequested();

        var bookList = books.ToList();
        var count = 0;

        foreach (var book in bookList)
        {
            if (_books.Delete(book.Id))
            {
                count++;

                // Clean up cover image if exists
                var fileStorage = _context.Database.FileStorage;
                if (fileStorage.Exists($"covers/{book.Id}"))
                {
                    fileStorage.Delete($"covers/{book.Id}");
                }
            }
        }

        return Task.FromResult(count);
    }

    public ValueTask<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = _books.Exists(b => b.Id == id);
        return ValueTask.FromResult(exists);
    }

    public ValueTask<bool> ExistsByIsbnAsync(string isbn, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(isbn);
        cancellationToken.ThrowIfCancellationRequested();

        var exists = _books.Exists(b => b.Metadata.Isbn == isbn);
        return ValueTask.FromResult(exists);
    }

    public ValueTask<int> CountAsync(ISpecification<Book>? specification = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (specification == null)
        {
            return ValueTask.FromResult(_books.Count());
        }

        var dtos = _books.FindAll();
        var books = dtos.Select(BookMapper.FromDto);
        var count = books.Count(specification.IsSatisfiedBy);
        return ValueTask.FromResult(count);
    }

    public Task<IReadOnlyList<Book>> SearchAsync(
        string searchTerm,
        SearchFields searchFields = SearchFields.All,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(searchTerm);
        cancellationToken.ThrowIfCancellationRequested();

        var query = _books.Query();
        var searchTermLower = searchTerm.ToLowerInvariant();

        // Build search predicate based on specified fields
        var dtoResults = query.ToEnumerable().Where(dto =>
        {
            if ((searchFields & SearchFields.Title) != 0 &&
                dto.Title.Value.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase))
                return true;

            if ((searchFields & SearchFields.Author) != 0 &&
                dto.Authors.Any(a => a.Name.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase)))
                return true;

            if ((searchFields & SearchFields.Description) != 0 &&
                dto.Metadata.Description?.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase) == true)
                return true;

            if ((searchFields & SearchFields.Isbn) != 0 &&
                dto.Metadata.Isbn?.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase) == true)
                return true;

            if ((searchFields & SearchFields.Publisher) != 0 &&
                dto.Metadata.Publisher?.Contains(searchTermLower, StringComparison.OrdinalIgnoreCase) == true)
                return true;

            return false;
        });

        var results = dtoResults.Select(BookMapper.FromDto).ToList();
        return Task.FromResult<IReadOnlyList<Book>>(results);
    }

    public Task<IReadOnlyList<Book>> GetWithChaptersAsync(
        ISpecification<Book>? specification = null,
        CancellationToken cancellationToken = default)
    {
        // In LiteDB, chapters are already embedded in the Book document
        // So this is the same as GetAllAsync
        return GetAllAsync(specification, cancellationToken);
    }

    public Task<IReadOnlyList<Book>> GetRecentlyAddedAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than 0");

        cancellationToken.ThrowIfCancellationRequested();

        // LiteDB assigns _id in insertion order, so we can use that
        var dtos = _books
            .Query()
            .OrderByDescending(b => b.Id)
            .Limit(count)
            .ToList();

        var books = dtos.Select(BookMapper.FromDto).ToList();
        return Task.FromResult<IReadOnlyList<Book>>(books);
    }

    public Task<IReadOnlyList<Book>> GetByPublicationDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date must be greater than or equal to start date");

        cancellationToken.ThrowIfCancellationRequested();

        var dtos = _books
            .Query()
            .Where(b => b.Metadata.PublicationDate.HasValue &&
                       b.Metadata.PublicationDate.Value >= startDate &&
                       b.Metadata.PublicationDate.Value <= endDate)
            .ToList();

        var books = dtos.Select(BookMapper.FromDto).ToList();
        return Task.FromResult<IReadOnlyList<Book>>(books);
    }
}