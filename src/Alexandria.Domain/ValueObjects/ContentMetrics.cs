using System;
using System.Collections.Generic;

namespace Alexandria.Domain.ValueObjects;

/// <summary>
/// Immutable value object containing comprehensive content analysis metrics.
/// </summary>
/// <remarks>
/// This value object encapsulates all metrics derived from content analysis,
/// including readability scores, word frequency, and reading time estimates.
/// All properties are immutable to maintain value object semantics.
/// </remarks>
public sealed record ContentMetrics
{
    /// <summary>
    /// Gets the total number of words in the content.
    /// </summary>
    public int WordCount { get; init; }

    /// <summary>
    /// Gets the total number of characters including spaces.
    /// </summary>
    public int CharacterCount { get; init; }

    /// <summary>
    /// Gets the total number of characters excluding spaces.
    /// </summary>
    public int CharacterCountNoSpaces { get; init; }

    /// <summary>
    /// Gets the total number of sentences detected.
    /// </summary>
    public int SentenceCount { get; init; }

    /// <summary>
    /// Gets the total number of paragraphs in the content.
    /// </summary>
    public int ParagraphCount { get; init; }

    /// <summary>
    /// Gets the estimated time to read the content at average reading speed.
    /// </summary>
    public TimeSpan EstimatedReadingTime { get; init; }

    /// <summary>
    /// Gets the average number of words per sentence.
    /// </summary>
    /// <remarks>
    /// Lower values generally indicate simpler, more readable text.
    /// Typical ranges: 15-20 words for general text, 20-25 for academic text.
    /// </remarks>
    public double AverageWordsPerSentence { get; init; }

    /// <summary>
    /// Gets the average number of syllables per word.
    /// </summary>
    /// <remarks>
    /// Used in readability calculations. Lower values indicate simpler vocabulary.
    /// </remarks>
    public double AverageSyllablesPerWord { get; init; }

    /// <summary>
    /// Gets the Flesch Reading Ease score (0-100, higher is easier to read).
    /// </summary>
    /// <remarks>
    /// Formula: 206.835 - 1.015 * (words/sentences) - 84.6 * (syllables/words)
    /// Score interpretation:
    /// - 90-100: Very Easy (5th grade)
    /// - 80-90: Easy (6th grade)
    /// - 70-80: Fairly Easy (7th grade)
    /// - 60-70: Standard (8th-9th grade)
    /// - 50-60: Fairly Difficult (10th-12th grade)
    /// - 30-50: Difficult (College)
    /// - 0-30: Very Difficult (College graduate)
    /// </remarks>
    public double ReadabilityScore { get; init; }

    /// <summary>
    /// Gets the frequency distribution of words in the content.
    /// </summary>
    /// <remarks>
    /// Key: word (lowercase), Value: occurrence count.
    /// Useful for identifying key terms and creating search indexes.
    /// Limited to top 100 most frequent words to control memory usage.
    /// </remarks>
    public IReadOnlyDictionary<string, int> WordFrequency { get; init; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets the top N most frequent words (excluding common stop words).
    /// </summary>
    /// <remarks>
    /// Useful for generating tags or keywords for the content.
    /// </remarks>
    public IReadOnlyList<string> TopKeywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the unique word count (vocabulary size).
    /// </summary>
    public int UniqueWordCount => WordFrequency?.Count ?? 0;

    /// <summary>
    /// Gets the lexical diversity (unique words / total words).
    /// </summary>
    /// <remarks>
    /// Higher values indicate more varied vocabulary.
    /// Typical ranges: 0.4-0.6 for most texts.
    /// </remarks>
    public double LexicalDiversity => WordCount > 0 ? (double)UniqueWordCount / WordCount : 0;

    /// <summary>
    /// Calculates the reading difficulty level based on the readability score.
    /// </summary>
    /// <returns>The reading difficulty level</returns>
    public ReadingDifficulty GetDifficulty() => ReadabilityScore switch
    {
        >= 90 => ReadingDifficulty.VeryEasy,
        >= 80 => ReadingDifficulty.Easy,
        >= 70 => ReadingDifficulty.FairlyEasy,
        >= 60 => ReadingDifficulty.Standard,
        >= 50 => ReadingDifficulty.FairlyDifficult,
        >= 30 => ReadingDifficulty.Difficult,
        _ => ReadingDifficulty.VeryDifficult
    };

    /// <summary>
    /// Gets the estimated grade level based on the Flesch-Kincaid Grade Level formula.
    /// </summary>
    /// <returns>US grade level (1-16+)</returns>
    public int GetGradeLevel()
    {
        if (SentenceCount == 0 || WordCount == 0)
            return 0;

        // Flesch-Kincaid Grade Level formula
        var gradeLevel = 0.39 * AverageWordsPerSentence + 11.8 * AverageSyllablesPerWord - 15.59;
        return Math.Max(1, Math.Min(16, (int)Math.Round(gradeLevel)));
    }

    /// <summary>
    /// Gets a human-readable description of the reading level.
    /// </summary>
    /// <returns>Description of the reading level and typical audience</returns>
    public string GetReadingLevelDescription() => GetDifficulty() switch
    {
        ReadingDifficulty.VeryEasy => "Very Easy - Suitable for elementary school students",
        ReadingDifficulty.Easy => "Easy - Suitable for middle school students",
        ReadingDifficulty.FairlyEasy => "Fairly Easy - Suitable for junior high students",
        ReadingDifficulty.Standard => "Standard - Suitable for high school students",
        ReadingDifficulty.FairlyDifficult => "Fairly Difficult - Suitable for high school seniors and college freshmen",
        ReadingDifficulty.Difficult => "Difficult - Suitable for college students",
        ReadingDifficulty.VeryDifficult => "Very Difficult - Suitable for college graduates and professionals",
        _ => "Unknown difficulty level"
    };

    /// <summary>
    /// Creates an empty ContentMetrics instance for use when no content is available.
    /// </summary>
    public static ContentMetrics Empty { get; } = new ContentMetrics
    {
        WordCount = 0,
        CharacterCount = 0,
        CharacterCountNoSpaces = 0,
        SentenceCount = 0,
        ParagraphCount = 0,
        EstimatedReadingTime = TimeSpan.Zero,
        AverageWordsPerSentence = 0,
        AverageSyllablesPerWord = 0,
        ReadabilityScore = 0,
        WordFrequency = new Dictionary<string, int>(),
        TopKeywords = Array.Empty<string>()
    };

    /// <summary>
    /// Validates that the metrics are internally consistent.
    /// </summary>
    /// <returns>True if metrics are valid and consistent</returns>
    public bool IsValid()
    {
        // Basic validation rules
        if (WordCount < 0 || CharacterCount < 0 || SentenceCount < 0 || ParagraphCount < 0)
            return false;

        // Character count without spaces should be less than or equal to total
        if (CharacterCountNoSpaces > CharacterCount)
            return false;

        // If we have words, we should have characters
        if (WordCount > 0 && CharacterCount == 0)
            return false;

        // Readability score should be in valid range
        if (ReadabilityScore < 0 || ReadabilityScore > 120)
            return false;

        return true;
    }
}

/// <summary>
/// Represents the difficulty level of text based on readability metrics.
/// </summary>
public enum ReadingDifficulty
{
    /// <summary>
    /// Very easy to read, suitable for elementary school (5th grade).
    /// Flesch Reading Ease: 90-100
    /// </summary>
    VeryEasy,

    /// <summary>
    /// Easy to read, suitable for middle school (6th grade).
    /// Flesch Reading Ease: 80-90
    /// </summary>
    Easy,

    /// <summary>
    /// Fairly easy to read, suitable for 7th grade.
    /// Flesch Reading Ease: 70-80
    /// </summary>
    FairlyEasy,

    /// <summary>
    /// Standard difficulty, suitable for 8th-9th grade.
    /// Flesch Reading Ease: 60-70
    /// </summary>
    Standard,

    /// <summary>
    /// Fairly difficult, suitable for high school (10th-12th grade).
    /// Flesch Reading Ease: 50-60
    /// </summary>
    FairlyDifficult,

    /// <summary>
    /// Difficult to read, suitable for college level.
    /// Flesch Reading Ease: 30-50
    /// </summary>
    Difficult,

    /// <summary>
    /// Very difficult to read, suitable for college graduates.
    /// Flesch Reading Ease: 0-30
    /// </summary>
    VeryDifficult
}