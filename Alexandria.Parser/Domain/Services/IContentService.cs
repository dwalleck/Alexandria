
using Alexandria.Parser.Domain.Entities;
using Alexandria.Parser.Domain.ValueObjects;

namespace Alexandria.Parser.Domain.Services;

public interface IContentService
{
    IEnumerable<SearchResult> Search(Book book, string searchTerm, SearchOptions? options = null);
    IEnumerable<SearchResult> SearchAll(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null);
    IEnumerable<SearchResult> SearchAny(Book book, IEnumerable<string> searchTerms, SearchOptions? options = null);
    string GetChapterPlainText(Chapter chapter);
    string GetFullPlainText(Book book);
    string GetPreview(Book book, int maxLength = 500);
    ReadingStatistics GetReadingStatistics(Book book, int wordsPerMinute = 250);
    IEnumerable<Chapter> FindChaptersWithTerm(Book book, string term);
}
