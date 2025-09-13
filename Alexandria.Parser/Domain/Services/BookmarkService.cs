using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.ValueObjects;

namespace Alexandria.Parser.Domain.Services;

/// <summary>
/// Service for managing bookmarks and annotations in books
/// </summary>
public sealed class BookmarkService
{
    private readonly Dictionary<string, List<Bookmark>> _bookmarksByBook = [];
    private readonly Dictionary<string, List<Annotation>> _annotationsByBook = [];
    private readonly Dictionary<string, ReadingProgress> _readingProgress = [];

    /// <summary>
    /// Adds a bookmark to a book
    /// </summary>
    public Bookmark AddBookmark(Book book, string chapterId, int position, string? note = null)
    {
        var chapter = book.GetChapterById(chapterId);
        if (chapter == null)
            throw new ArgumentException($"Chapter with ID {chapterId} not found");

        var contextText = ExtractContextText(chapter.Content, position, 50);
        var bookmark = Bookmark.Create(chapterId, chapter.Title, position, note, contextText);

        if (!_bookmarksByBook.ContainsKey(book.Title.Value))
            _bookmarksByBook[book.Title.Value] = [];

        _bookmarksByBook[book.Title.Value].Add(bookmark);
        return bookmark;
    }

    /// <summary>
    /// Gets all bookmarks for a book
    /// </summary>
    public IEnumerable<Bookmark> GetBookmarks(Book book)
    {
        return _bookmarksByBook.TryGetValue(book.Title.Value, out var bookmarks)
            ? bookmarks.OrderBy(b => b.CreatedAt)
            : Enumerable.Empty<Bookmark>();
    }

    /// <summary>
    /// Gets bookmarks for a specific chapter
    /// </summary>
    public IEnumerable<Bookmark> GetChapterBookmarks(Book book, string chapterId)
    {
        return GetBookmarks(book).Where(b => b.ChapterId == chapterId);
    }

    /// <summary>
    /// Removes a bookmark
    /// </summary>
    public bool RemoveBookmark(Book book, string bookmarkId)
    {
        if (!_bookmarksByBook.TryGetValue(book.Title.Value, out var bookmarks))
            return false;

        var bookmark = bookmarks.FirstOrDefault(b => b.Id == bookmarkId);
        if (bookmark != null)
        {
            bookmarks.Remove(bookmark);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds an annotation/highlight to a book
    /// </summary>
    public Annotation AddAnnotation(
        Book book,
        string chapterId,
        int startPosition,
        int endPosition,
        string highlightedText,
        HighlightColor color = HighlightColor.Yellow,
        string? note = null)
    {
        var chapter = book.GetChapterById(chapterId);
        if (chapter == null)
            throw new ArgumentException($"Chapter with ID {chapterId} not found");

        var annotation = Annotation.Create(chapterId, startPosition, endPosition, highlightedText, color, note);

        if (!_annotationsByBook.ContainsKey(book.Title.Value))
            _annotationsByBook[book.Title.Value] = [];

        _annotationsByBook[book.Title.Value].Add(annotation);
        return annotation;
    }

    /// <summary>
    /// Gets all annotations for a book
    /// </summary>
    public IEnumerable<Annotation> GetAnnotations(Book book)
    {
        return _annotationsByBook.TryGetValue(book.Title.Value, out var annotations)
            ? annotations.OrderBy(a => a.CreatedAt)
            : Enumerable.Empty<Annotation>();
    }

    /// <summary>
    /// Gets annotations for a specific chapter
    /// </summary>
    public IEnumerable<Annotation> GetChapterAnnotations(Book book, string chapterId)
    {
        return GetAnnotations(book).Where(a => a.ChapterId == chapterId);
    }

    /// <summary>
    /// Removes an annotation
    /// </summary>
    public bool RemoveAnnotation(Book book, string annotationId)
    {
        if (!_annotationsByBook.TryGetValue(book.Title.Value, out var annotations))
            return false;

        var annotation = annotations.FirstOrDefault(a => a.Id == annotationId);
        if (annotation != null)
        {
            annotations.Remove(annotation);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates the reading progress for a book
    /// </summary>
    public ReadingProgress UpdateReadingProgress(Book book, string chapterId, int position)
    {
        var chapter = book.GetChapterById(chapterId);
        if (chapter == null)
            throw new ArgumentException($"Chapter with ID {chapterId} not found");

        var bookId = book.Title.Value;

        if (!_readingProgress.TryGetValue(bookId, out var progress))
        {
            // Start new reading progress
            progress = ReadingProgress.StartNew(bookId, chapterId, book.Chapters.Count);
        }
        else
        {
            // Update existing progress
            var chapterIndex = book.Chapters.ToList().IndexOf(chapter);
            progress = progress.UpdatePosition(chapterId, chapterIndex, position);
        }

        _readingProgress[bookId] = progress;
        return progress;
    }

    /// <summary>
    /// Gets the reading progress for a book
    /// </summary>
    public ReadingProgress? GetReadingProgress(Book book)
    {
        return _readingProgress.TryGetValue(book.Title.Value, out var progress) ? progress : null;
    }

    /// <summary>
    /// Clears all bookmarks and annotations for a book
    /// </summary>
    public void ClearBookData(Book book)
    {
        var bookId = book.Title.Value;
        _bookmarksByBook.Remove(bookId);
        _annotationsByBook.Remove(bookId);
        _readingProgress.Remove(bookId);
    }

    /// <summary>
    /// Exports all bookmarks and annotations for a book
    /// </summary>
    public BookmarkExport ExportBookmarks(Book book)
    {
        return new BookmarkExport(
            book.Title.Value,
            GetBookmarks(book).ToList(),
            GetAnnotations(book).ToList(),
            GetReadingProgress(book),
            DateTime.UtcNow
        );
    }

    private string ExtractContextText(string content, int position, int length)
    {
        // Simple context extraction - could be enhanced with HTML parsing
        var plainText = content.Replace("<", " <").Replace(">", "> ");
        plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"<[^>]*>", "");
        plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s+", " ").Trim();

        if (string.IsNullOrWhiteSpace(plainText) || position >= plainText.Length)
            return string.Empty;

        var start = Math.Max(0, position - length / 2);
        var end = Math.Min(plainText.Length, start + length);

        var context = plainText.Substring(start, end - start);
        if (start > 0)
            context = "..." + context;
        if (end < plainText.Length)
            context = context + "...";

        return context;
    }
}

/// <summary>
/// Export container for bookmarks and annotations
/// </summary>
public sealed class BookmarkExport
{
    public BookmarkExport(
        string bookTitle,
        IReadOnlyList<Bookmark> bookmarks,
        IReadOnlyList<Annotation> annotations,
        ReadingProgress? readingProgress,
        DateTime exportDate)
    {
        BookTitle = bookTitle ?? throw new ArgumentNullException(nameof(bookTitle));
        Bookmarks = bookmarks ?? new List<Bookmark>();
        Annotations = annotations ?? new List<Annotation>();
        ReadingProgress = readingProgress;
        ExportDate = exportDate;
    }

    public string BookTitle { get; }
    public IReadOnlyList<Bookmark> Bookmarks { get; }
    public IReadOnlyList<Annotation> Annotations { get; }
    public ReadingProgress? ReadingProgress { get; }
    public DateTime ExportDate { get; }

    public int TotalBookmarks => Bookmarks.Count;
    public int TotalAnnotations => Annotations.Count;
}