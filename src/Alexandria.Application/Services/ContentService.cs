using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;
using Alexandria.Domain.Services;

namespace Alexandria.Application.Services;

public class ContentService : IContentService
{
    private readonly ContentProcessor _contentProcessor;
    private readonly SearchService _searchService;

    public ContentService()
    {
        _contentProcessor = new ContentProcessor();
        _searchService = new SearchService(_contentProcessor);
    }

    public IEnumerable<SearchResult> Search(Book book, string searchTerm, SearchOptions? options = null)
    {
        return _searchService.Search(book, searchTerm, options);
    }

    public IEnumerable<SearchResult> SearchAll(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null)
    {
        return _searchService.SearchAll(book, searchTerms, options);
    }

    public IEnumerable<SearchResult> SearchAny(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null)
    {
        return _searchService.SearchAny(book, searchTerms, options);
    }

    public string GetChapterPlainText(Chapter chapter)
    {
        return _contentProcessor.ExtractPlainText(chapter.Content);
    }

    public string GetFullPlainText(Book book)
    {
        var texts = book.Chapters.Select(c => _contentProcessor.ExtractPlainText(c.Content));
        return string.Join("\n\n", texts);
    }

    public string GetPreview(Book book, int maxLength = 500)
    {
        if (book.Chapters.Count == 0)
            return string.Empty;

        var firstChapterText = _contentProcessor.ExtractPlainText(book.Chapters[0].Content);

        if (firstChapterText.Length <= maxLength)
            return firstChapterText;

        var preview = firstChapterText.Substring(0, maxLength);
        var lastSpace = preview.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.8)
        {
            preview = preview.Substring(0, lastSpace);
        }

        return preview.Trim() + "...";
    }

    public ReadingStatistics GetReadingStatistics(Book book, int wordsPerMinute = 250)
    {
        var chapterStats = new List<ChapterStatistics>();

        foreach (var chapter in book.Chapters)
        {
            var wordCount = _contentProcessor.CountWords(chapter.Content);
            var readingTime = _contentProcessor.EstimateReadingTime(chapter.Content, wordsPerMinute);
            var sentences = _contentProcessor.ExtractSentences(chapter.Content).Count();

            chapterStats.Add(new ChapterStatistics(
                chapter.Id,
                chapter.Title,
                wordCount,
                sentences,
                readingTime
            ));
        }

        var totalWords = chapterStats.Sum(c => c.WordCount);
        var totalSentences = chapterStats.Sum(c => c.SentenceCount);
        var totalReadingTime = TimeSpan.FromMinutes(chapterStats.Sum(c => c.ReadingTime.TotalMinutes));

        return new ReadingStatistics(
            totalWords,
            totalSentences,
            totalReadingTime,
            chapterStats,
            wordsPerMinute
        );
    }

    public IEnumerable<Chapter> FindChaptersWithTerm(Book book, string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Enumerable.Empty<Chapter>();

        return book.Chapters.Where(c =>
        {
            var plainText = _contentProcessor.ExtractPlainText(c.Content);
            return plainText.Contains(term, StringComparison.OrdinalIgnoreCase);
        });
    }
}
