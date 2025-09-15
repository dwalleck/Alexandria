using System;
using System.Collections.Generic;
using System.Linq;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Represents reading statistics for a book
/// </summary>
public sealed class ReadingStatistics
{
    public ReadingStatistics(
        int totalWords,
        int totalSentences,
        TimeSpan totalReadingTime,
        IEnumerable<ChapterStatistics> chapterStatistics,
        int wordsPerMinute)
    {
        TotalWords = totalWords;
        TotalSentences = totalSentences;
        TotalReadingTime = totalReadingTime;
        ChapterStatistics = chapterStatistics?.ToList() ?? [];
        WordsPerMinute = wordsPerMinute;
        AverageWordsPerChapter = ChapterStatistics.Count > 0 ? TotalWords / ChapterStatistics.Count : 0;
        AverageSentencesPerChapter = ChapterStatistics.Count > 0 ? TotalSentences / ChapterStatistics.Count : 0;
    }

    public int TotalWords { get; }
    public int TotalSentences { get; }
    public TimeSpan TotalReadingTime { get; }
    public IReadOnlyList<ChapterStatistics> ChapterStatistics { get; }
    public int WordsPerMinute { get; }
    public int AverageWordsPerChapter { get; }
    public int AverageSentencesPerChapter { get; }

    public ChapterStatistics? GetLongestChapter()
    {
        return ChapterStatistics.OrderByDescending(c => c.WordCount).FirstOrDefault();
    }

    public ChapterStatistics? GetShortestChapter()
    {
        return ChapterStatistics.OrderBy(c => c.WordCount).FirstOrDefault();
    }

    public double GetAverageReadingTimePerChapter()
    {
        if (ChapterStatistics.Count == 0)
            return 0;

        return ChapterStatistics.Average(c => c.ReadingTime.TotalMinutes);
    }
}

/// <summary>
/// Represents reading statistics for a single chapter
/// </summary>
public sealed class ChapterStatistics
{
    public ChapterStatistics(
        string chapterId,
        string chapterTitle,
        int wordCount,
        int sentenceCount,
        TimeSpan readingTime)
    {
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        ChapterTitle = chapterTitle ?? throw new ArgumentNullException(nameof(chapterTitle));
        WordCount = wordCount;
        SentenceCount = sentenceCount;
        ReadingTime = readingTime;
        AverageWordsPerSentence = sentenceCount > 0 ? (double)wordCount / sentenceCount : 0;
    }

    public string ChapterId { get; }
    public string ChapterTitle { get; }
    public int WordCount { get; }
    public int SentenceCount { get; }
    public TimeSpan ReadingTime { get; }
    public double AverageWordsPerSentence { get; }
}