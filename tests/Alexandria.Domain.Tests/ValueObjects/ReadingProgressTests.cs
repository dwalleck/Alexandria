using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Tests.ValueObjects;

public class ReadingProgressTests
{
    [Test]
    public async Task Should_Create_Reading_Progress()
    {
        // Arrange & Act
        var progress = new ReadingProgress(
            "book1",
            "chapter1",
            0,
            100,
            10,
            DateTime.UtcNow,
            TimeSpan.FromHours(2)
        );

        // Assert
        await Assert.That(progress.BookId).IsEqualTo("book1");
        await Assert.That(progress.ChapterId).IsEqualTo("chapter1");
        await Assert.That(progress.ChapterIndex).IsEqualTo(0);
        await Assert.That(progress.PositionInChapter).IsEqualTo(100);
        await Assert.That(progress.TotalChapters).IsEqualTo(10);
        await Assert.That(progress.TotalReadingTime).IsEqualTo(TimeSpan.FromHours(2));
    }

    [Test]
    public async Task Should_Throw_When_Required_Fields_Null()
    {
        // Act & Assert
        await Assert.That(() => new ReadingProgress(null!, "ch1", 0, 0, 10, DateTime.UtcNow, TimeSpan.Zero))
            .Throws<ArgumentNullException>();

        await Assert.That(() => new ReadingProgress("book1", null!, 0, 0, 10, DateTime.UtcNow, TimeSpan.Zero))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Should_Throw_When_Chapter_Index_Out_Of_Range()
    {
        // Act & Assert
        await Assert.That(() => new ReadingProgress("book1", "ch1", -1, 0, 10, DateTime.UtcNow, TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();

        await Assert.That(() => new ReadingProgress("book1", "ch1", 10, 0, 10, DateTime.UtcNow, TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Should_Calculate_Percentage_Complete()
    {
        // Arrange
        var progress = new ReadingProgress("book1", "ch5", 5, 100, 10, DateTime.UtcNow, TimeSpan.Zero);

        // Act
        var percentage = progress.GetPercentageComplete();

        // Assert
        await Assert.That(percentage).IsEqualTo(55.0); // 5.5 chapters out of 10 = 55%
    }

    [Test]
    public async Task Should_Return_Zero_Percentage_For_Empty_Book()
    {
        // Arrange
        var progress = new ReadingProgress("book1", "ch1", 0, 0, 0, DateTime.UtcNow, TimeSpan.Zero);

        // Act
        var percentage = progress.GetPercentageComplete();

        // Assert
        await Assert.That(percentage).IsEqualTo(0);
    }

    [Test]
    public async Task Should_Check_If_Completed()
    {
        // Arrange
        var notCompleted = new ReadingProgress("book1", "ch8", 8, 100, 10, DateTime.UtcNow, TimeSpan.Zero);
        var completed = new ReadingProgress("book1", "ch9", 9, 100, 10, DateTime.UtcNow, TimeSpan.Zero);

        // Act & Assert
        await Assert.That(notCompleted.IsCompleted()).IsFalse();
        await Assert.That(completed.IsCompleted()).IsTrue();
    }

    [Test]
    public async Task Should_Update_Position()
    {
        // Arrange
        var initialTime = DateTime.UtcNow.AddMinutes(-5);
        var progress = new ReadingProgress("book1", "ch1", 0, 100, 10, initialTime, TimeSpan.FromHours(1));

        // Act
        var updated = progress.UpdatePosition("ch2", 1, 200);

        // Assert
        await Assert.That(updated.ChapterId).IsEqualTo("ch2");
        await Assert.That(updated.ChapterIndex).IsEqualTo(1);
        await Assert.That(updated.PositionInChapter).IsEqualTo(200);
        await Assert.That(updated.TotalReadingTime).IsGreaterThan(TimeSpan.FromHours(1));
    }

    [Test]
    public async Task Should_Move_To_Next_Chapter()
    {
        // Arrange
        var progress = new ReadingProgress("book1", "ch1", 0, 100, 10, DateTime.UtcNow, TimeSpan.Zero);

        // Act
        var next = progress.NextChapter("ch2");

        // Assert
        await Assert.That(next.ChapterId).IsEqualTo("ch2");
        await Assert.That(next.ChapterIndex).IsEqualTo(1);
        await Assert.That(next.PositionInChapter).IsEqualTo(0);
    }

    [Test]
    public async Task Should_Throw_When_Already_At_Last_Chapter()
    {
        // Arrange
        var progress = new ReadingProgress("book1", "ch10", 9, 100, 10, DateTime.UtcNow, TimeSpan.Zero);

        // Act & Assert
        await Assert.That(() => progress.NextChapter("ch11"))
            .Throws<InvalidOperationException>()
            .WithMessage("Already at the last chapter");
    }

    [Test]
    public async Task Should_Move_To_Previous_Chapter()
    {
        // Arrange
        var progress = new ReadingProgress("book1", "ch2", 1, 100, 10, DateTime.UtcNow, TimeSpan.Zero);

        // Act
        var previous = progress.PreviousChapter("ch1");

        // Assert
        await Assert.That(previous.ChapterId).IsEqualTo("ch1");
        await Assert.That(previous.ChapterIndex).IsEqualTo(0);
        await Assert.That(previous.PositionInChapter).IsEqualTo(0);
    }

    [Test]
    public async Task Should_Throw_When_Already_At_First_Chapter()
    {
        // Arrange
        var progress = new ReadingProgress("book1", "ch1", 0, 100, 10, DateTime.UtcNow, TimeSpan.Zero);

        // Act & Assert
        await Assert.That(() => progress.PreviousChapter("ch0"))
            .Throws<InvalidOperationException>()
            .WithMessage("Already at the first chapter");
    }

    [Test]
    public async Task Should_Start_New_Reading_Progress()
    {
        // Act
        var progress = ReadingProgress.StartNew("book1", "ch1", 10);

        // Assert
        await Assert.That(progress.BookId).IsEqualTo("book1");
        await Assert.That(progress.ChapterId).IsEqualTo("ch1");
        await Assert.That(progress.ChapterIndex).IsEqualTo(0);
        await Assert.That(progress.PositionInChapter).IsEqualTo(0);
        await Assert.That(progress.TotalChapters).IsEqualTo(10);
        await Assert.That(progress.TotalReadingTime).IsEqualTo(TimeSpan.Zero);
    }
}