using Alexandria.Parser.Domain.ValueObjects;
using System.Collections.Generic;

namespace Alexandria.Parser.Domain.Entities;

/// <summary>
/// Represents an EPUB book with immutable properties following DDD principles
/// </summary>
public sealed class Book
{
    private readonly List<Chapter> _chapters;
    private readonly List<Author> _authors;
    private readonly List<BookIdentifier> _identifiers;

    public Book(
        BookTitle title,
        IEnumerable<BookTitle>? alternateTitles,
        IEnumerable<Author> authors,
        IEnumerable<Chapter> chapters,
        IEnumerable<BookIdentifier> identifiers,
        Language language,
        BookMetadata metadata)
    {
        Title = title ?? throw new ArgumentNullException(nameof(title));
        AlternateTitles = alternateTitles?.ToList() ?? new List<BookTitle>();
        _authors = authors?.ToList() ?? throw new ArgumentNullException(nameof(authors));
        _chapters = chapters?.ToList() ?? throw new ArgumentNullException(nameof(chapters));
        _identifiers = identifiers?.ToList() ?? throw new ArgumentNullException(nameof(identifiers));
        Language = language ?? throw new ArgumentNullException(nameof(language));
        Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        if (_chapters.Count == 0)
            throw new ArgumentException("A book must have at least one chapter", nameof(chapters));

        if (_authors.Count == 0)
            throw new ArgumentException("A book must have at least one author", nameof(authors));
    }

    public BookTitle Title { get; }
    public IReadOnlyList<BookTitle> AlternateTitles { get; }
    public IReadOnlyList<Author> Authors => _authors.AsReadOnly();
    public IReadOnlyList<Chapter> Chapters => _chapters.AsReadOnly();
    public IReadOnlyList<BookIdentifier> Identifiers => _identifiers.AsReadOnly();
    public Language Language { get; }
    public BookMetadata Metadata { get; }

    public Chapter? GetChapterById(string id)
    {
        return _chapters.FirstOrDefault(c => c.Id == id);
    }

    public Chapter? GetChapterByOrder(int order)
    {
        return _chapters.FirstOrDefault(c => c.Order == order);
    }
}