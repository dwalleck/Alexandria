namespace Alexandria.Parser.Domain.ValueObjects;

/// <summary>
/// Represents the reading progress through a book
/// </summary>
public sealed class ReadingProgress
{
    public ReadingProgress(
        string bookId,
        string chapterId,
        int chapterIndex,
        int positionInChapter,
        int totalChapters,
        DateTime lastReadTime,
        TimeSpan totalReadingTime)
    {
        BookId = bookId ?? throw new ArgumentNullException(nameof(bookId));
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        ChapterIndex = chapterIndex;
        PositionInChapter = Math.Max(0, positionInChapter);
        TotalChapters = totalChapters;
        LastReadTime = lastReadTime;
        TotalReadingTime = totalReadingTime;

        if (chapterIndex < 0 || chapterIndex >= totalChapters)
            throw new ArgumentOutOfRangeException(nameof(chapterIndex),
                "Chapter index must be between 0 and total chapters");
    }

    public string BookId { get; }
    public string ChapterId { get; }
    public int ChapterIndex { get; }
    public int PositionInChapter { get; }
    public int TotalChapters { get; }
    public DateTime LastReadTime { get; }
    public TimeSpan TotalReadingTime { get; }

    /// <summary>
    /// Gets the percentage of the book completed (0-100)
    /// </summary>
    public double GetPercentageComplete()
    {
        if (TotalChapters == 0)
            return 0;

        // Simple calculation based on chapter progress
        // Could be enhanced with actual word count per chapter
        var completedChapters = ChapterIndex;
        var currentChapterProgress = PositionInChapter > 0 ? 0.5 : 0; // Assume halfway if reading
        var progress = (completedChapters + currentChapterProgress) / TotalChapters * 100;

        return Math.Min(100, Math.Max(0, progress));
    }

    /// <summary>
    /// Checks if the book is completed
    /// </summary>
    public bool IsCompleted()
    {
        return ChapterIndex >= TotalChapters - 1;
    }

    /// <summary>
    /// Updates the reading position
    /// </summary>
    public ReadingProgress UpdatePosition(string chapterId, int chapterIndex, int positionInChapter)
    {
        return new ReadingProgress(
            BookId,
            chapterId,
            chapterIndex,
            positionInChapter,
            TotalChapters,
            DateTime.UtcNow,
            TotalReadingTime + (DateTime.UtcNow - LastReadTime)
        );
    }

    /// <summary>
    /// Moves to the next chapter
    /// </summary>
    public ReadingProgress NextChapter(string nextChapterId)
    {
        if (ChapterIndex >= TotalChapters - 1)
            throw new InvalidOperationException("Already at the last chapter");

        return new ReadingProgress(
            BookId,
            nextChapterId,
            ChapterIndex + 1,
            0,
            TotalChapters,
            DateTime.UtcNow,
            TotalReadingTime
        );
    }

    /// <summary>
    /// Moves to the previous chapter
    /// </summary>
    public ReadingProgress PreviousChapter(string previousChapterId)
    {
        if (ChapterIndex <= 0)
            throw new InvalidOperationException("Already at the first chapter");

        return new ReadingProgress(
            BookId,
            previousChapterId,
            ChapterIndex - 1,
            0,
            TotalChapters,
            DateTime.UtcNow,
            TotalReadingTime
        );
    }

    /// <summary>
    /// Creates initial reading progress for a book
    /// </summary>
    public static ReadingProgress StartNew(string bookId, string firstChapterId, int totalChapters)
    {
        return new ReadingProgress(
            bookId,
            firstChapterId,
            0,
            0,
            totalChapters,
            DateTime.UtcNow,
            TimeSpan.Zero
        );
    }
}