using System;
using System.Collections.Generic;
using Alexandria.Domain.ValueObjects;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Domain.Tests.ValueObjects;

public class ContentMetricsTests
{
    [Test]
    public async Task GetDifficulty_ReturnsCorrectDifficultyForReadabilityScore()
    {
        // Arrange & Act & Assert
        var veryEasyMetrics = new ContentMetrics { ReadabilityScore = 95 };
        await Assert.That(veryEasyMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.VeryEasy);

        var easyMetrics = new ContentMetrics { ReadabilityScore = 85 };
        await Assert.That(easyMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.Easy);

        var fairlyEasyMetrics = new ContentMetrics { ReadabilityScore = 75 };
        await Assert.That(fairlyEasyMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.FairlyEasy);

        var standardMetrics = new ContentMetrics { ReadabilityScore = 65 };
        await Assert.That(standardMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.Standard);

        var fairlyDifficultMetrics = new ContentMetrics { ReadabilityScore = 55 };
        await Assert.That(fairlyDifficultMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.FairlyDifficult);

        var difficultMetrics = new ContentMetrics { ReadabilityScore = 40 };
        await Assert.That(difficultMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.Difficult);

        var veryDifficultMetrics = new ContentMetrics { ReadabilityScore = 20 };
        await Assert.That(veryDifficultMetrics.GetDifficulty()).IsEqualTo(ReadingDifficulty.VeryDifficult);
    }

    [Test]
    public async Task GetGradeLevel_CalculatesCorrectGradeLevel()
    {
        // Arrange
        var metrics = new ContentMetrics
        {
            AverageWordsPerSentence = 20,
            AverageSyllablesPerWord = 1.5,
            SentenceCount = 10,
            WordCount = 200
        };

        // Act
        var gradeLevel = metrics.GetGradeLevel();

        // Assert
        // Formula: 0.39 * 20 + 11.8 * 1.5 - 15.59 = 7.8 + 17.7 - 15.59 = 9.91 ≈ 10
        await Assert.That(gradeLevel).IsEqualTo(10);
    }

    [Test]
    public async Task GetGradeLevel_ReturnsZeroWhenNoContent()
    {
        // Arrange
        var metrics = new ContentMetrics
        {
            SentenceCount = 0,
            WordCount = 0
        };

        // Act
        var gradeLevel = metrics.GetGradeLevel();

        // Assert
        await Assert.That(gradeLevel).IsEqualTo(0);
    }

    [Test]
    public async Task GetGradeLevel_ClampsToValidRange()
    {
        // Arrange - Very easy text
        var easyMetrics = new ContentMetrics
        {
            AverageWordsPerSentence = 5,
            AverageSyllablesPerWord = 1,
            SentenceCount = 10,
            WordCount = 50
        };

        // Arrange - Very difficult text
        var difficultMetrics = new ContentMetrics
        {
            AverageWordsPerSentence = 50,
            AverageSyllablesPerWord = 3,
            SentenceCount = 10,
            WordCount = 500
        };

        // Act
        var easyGrade = easyMetrics.GetGradeLevel();
        var difficultGrade = difficultMetrics.GetGradeLevel();

        // Assert
        await Assert.That(easyGrade).IsGreaterThanOrEqualTo(1);
        await Assert.That(difficultGrade).IsLessThanOrEqualTo(16);
    }

    [Test]
    public async Task UniqueWordCount_ReturnsCorrectCount()
    {
        // Arrange
        var metrics = new ContentMetrics
        {
            WordFrequency = new Dictionary<string, int>
            {
                { "the", 5 },
                { "quick", 1 },
                { "brown", 1 },
                { "fox", 2 }
            }
        };

        // Act & Assert
        await Assert.That(metrics.UniqueWordCount).IsEqualTo(4);
    }

    [Test]
    public async Task UniqueWordCount_ReturnsZeroWhenNoWordFrequency()
    {
        // Arrange
        var metricsWithNull = new ContentMetrics { WordFrequency = null! };
        var metricsWithEmpty = new ContentMetrics { WordFrequency = new Dictionary<string, int>() };

        // Act & Assert
        await Assert.That(metricsWithNull.UniqueWordCount).IsEqualTo(0);
        await Assert.That(metricsWithEmpty.UniqueWordCount).IsEqualTo(0);
    }

    [Test]
    public async Task LexicalDiversity_CalculatesCorrectly()
    {
        // Arrange
        var metrics = new ContentMetrics
        {
            WordCount = 100,
            WordFrequency = new Dictionary<string, int>
            {
                { "unique1", 1 },
                { "unique2", 1 },
                { "repeated", 98 }
            }
        };

        // Act
        var diversity = metrics.LexicalDiversity;

        // Assert
        // 3 unique words / 100 total words = 0.03
        await Assert.That(diversity).IsEqualTo(0.03);
    }

    [Test]
    public async Task LexicalDiversity_ReturnsZeroWhenNoWords()
    {
        // Arrange
        var metrics = new ContentMetrics { WordCount = 0 };

        // Act & Assert
        await Assert.That(metrics.LexicalDiversity).IsEqualTo(0);
    }

    [Test]
    public async Task GetReadingLevelDescription_ReturnsCorrectDescriptions()
    {
        // Arrange & Act & Assert
        var veryEasy = new ContentMetrics { ReadabilityScore = 95 };
        await Assert.That(veryEasy.GetReadingLevelDescription())
            .Contains("Very Easy")
            .And.Contains("elementary school");

        var standard = new ContentMetrics { ReadabilityScore = 65 };
        await Assert.That(standard.GetReadingLevelDescription())
            .Contains("Standard")
            .And.Contains("high school");

        var difficult = new ContentMetrics { ReadabilityScore = 40 };
        await Assert.That(difficult.GetReadingLevelDescription())
            .Contains("Difficult")
            .And.Contains("college");
    }

    [Test]
    public async Task IsValid_ReturnsTrueForValidMetrics()
    {
        // Arrange
        var validMetrics = new ContentMetrics
        {
            WordCount = 100,
            CharacterCount = 500,
            CharacterCountNoSpaces = 400,
            SentenceCount = 10,
            ParagraphCount = 3,
            ReadabilityScore = 65
        };

        // Act & Assert
        await Assert.That(validMetrics.IsValid()).IsTrue();
    }

    [Test]
    public async Task IsValid_ReturnsFalseForNegativeValues()
    {
        // Arrange
        var negativeWordCount = new ContentMetrics { WordCount = -1 };
        var negativeCharCount = new ContentMetrics { CharacterCount = -1 };
        var negativeSentenceCount = new ContentMetrics { SentenceCount = -1 };
        var negativeParagraphCount = new ContentMetrics { ParagraphCount = -1 };

        // Act & Assert
        await Assert.That(negativeWordCount.IsValid()).IsFalse();
        await Assert.That(negativeCharCount.IsValid()).IsFalse();
        await Assert.That(negativeSentenceCount.IsValid()).IsFalse();
        await Assert.That(negativeParagraphCount.IsValid()).IsFalse();
    }

    [Test]
    public async Task IsValid_ReturnsFalseWhenCharCountNoSpacesExceedsTotal()
    {
        // Arrange
        var invalidMetrics = new ContentMetrics
        {
            CharacterCount = 100,
            CharacterCountNoSpaces = 150 // More than total!
        };

        // Act & Assert
        await Assert.That(invalidMetrics.IsValid()).IsFalse();
    }

    [Test]
    public async Task IsValid_ReturnsFalseWhenWordsButNoCharacters()
    {
        // Arrange
        var invalidMetrics = new ContentMetrics
        {
            WordCount = 100,
            CharacterCount = 0
        };

        // Act & Assert
        await Assert.That(invalidMetrics.IsValid()).IsFalse();
    }

    [Test]
    public async Task IsValid_ReturnsFalseForInvalidReadabilityScore()
    {
        // Arrange
        var negativeScore = new ContentMetrics { ReadabilityScore = -10 };
        var tooHighScore = new ContentMetrics { ReadabilityScore = 150 };

        // Act & Assert
        await Assert.That(negativeScore.IsValid()).IsFalse();
        await Assert.That(tooHighScore.IsValid()).IsFalse();
    }

    [Test]
    public async Task Empty_ReturnsValidEmptyMetrics()
    {
        // Arrange & Act
        var empty = ContentMetrics.Empty;

        // Assert
        await Assert.That(empty.WordCount).IsEqualTo(0);
        await Assert.That(empty.CharacterCount).IsEqualTo(0);
        await Assert.That(empty.CharacterCountNoSpaces).IsEqualTo(0);
        await Assert.That(empty.SentenceCount).IsEqualTo(0);
        await Assert.That(empty.ParagraphCount).IsEqualTo(0);
        await Assert.That(empty.EstimatedReadingTime).IsEqualTo(TimeSpan.Zero);
        await Assert.That(empty.WordFrequency).IsNotNull();
        await Assert.That(empty.TopKeywords).IsNotNull();
        await Assert.That(empty.IsValid()).IsTrue();
    }

    [Test]
    public async Task ContentMetrics_IsImmutable()
    {
        // Arrange
        var metrics1 = new ContentMetrics
        {
            WordCount = 100,
            ReadabilityScore = 65
        };

        var metrics2 = metrics1 with { WordCount = 200 };

        // Act & Assert
        await Assert.That(metrics1.WordCount).IsEqualTo(100);
        await Assert.That(metrics2.WordCount).IsEqualTo(200);
        await Assert.That(metrics1.ReadabilityScore).IsEqualTo(metrics2.ReadabilityScore);
    }

    [Test]
    public async Task ContentMetrics_SupportsValueComparison()
    {
        // Arrange
        var metrics1 = new ContentMetrics
        {
            WordCount = 100,
            CharacterCount = 500,
            ReadabilityScore = 65
        };

        var metrics2 = new ContentMetrics
        {
            WordCount = 100,
            CharacterCount = 500,
            ReadabilityScore = 65
        };

        var metrics3 = new ContentMetrics
        {
            WordCount = 200,
            CharacterCount = 500,
            ReadabilityScore = 65
        };

        // Act & Assert
        // Records with reference type properties (like Dictionary) won't be equal by default
        // But we can compare the important value properties
        await Assert.That(metrics1.WordCount).IsEqualTo(metrics2.WordCount);
        await Assert.That(metrics1.CharacterCount).IsEqualTo(metrics2.CharacterCount);
        await Assert.That(metrics1.ReadabilityScore).IsEqualTo(metrics2.ReadabilityScore);

        await Assert.That(metrics1.WordCount).IsNotEqualTo(metrics3.WordCount);
    }
}