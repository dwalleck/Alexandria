using Alexandria.Domain.ValueObjects;

namespace Alexandria.Domain.Tests.ValueObjects;

public class NavigationStructureTests
{
    private static List<NavigationItem> CreateTestNavigationItems()
    {
        var ch1_1 = new NavigationItem("ch1.1", "Section 1.1", "ch1.xhtml#s1", 2, 1);
        var ch1_2 = new NavigationItem("ch1.2", "Section 1.2", "ch1.xhtml#s2", 3, 1);
        var ch1 = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0, new[] { ch1_1, ch1_2 });

        var ch2_1 = new NavigationItem("ch2.1", "Section 2.1", "ch2.xhtml#s1", 5, 1);
        var ch2 = new NavigationItem("ch2", "Chapter 2", "chapter2.xhtml", 4, 0, new[] { ch2_1 });

        var ch3 = new NavigationItem("ch3", "Chapter 3", "chapter3.xhtml", 6, 0);

        return [ch1, ch2, ch3];
    }

    [Test]
    public async Task Should_Create_NavigationStructure_With_Valid_Data()
    {
        // Arrange
        var items = CreateTestNavigationItems();

        // Act
        var structure = new NavigationStructure(
            title: "My Book TOC",
            items: items,
            tocNcxPath: "OEBPS/toc.ncx",
            navPath: null);

        // Assert
        await Assert.That(structure.Title).IsEqualTo("My Book TOC");
        await Assert.That(structure.TocNcxPath).IsEqualTo("OEBPS/toc.ncx");
        await Assert.That(structure.NavPath).IsNull();
        await Assert.That(structure.Items).HasCount(3);
    }

    [Test]
    public async Task Should_Use_Default_Title_When_Null()
    {
        // Arrange
        var items = CreateTestNavigationItems();

        // Act
        var structure = new NavigationStructure(null, items);

        // Assert
        await Assert.That(structure.Title).IsEqualTo("Table of Contents");
    }

    [Test]
    public async Task Should_Throw_When_Items_Is_Null()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new NavigationStructure("Title", null!))
            .Throws<ArgumentNullException>()
            .WithMessage("Value cannot be null. (Parameter 'items')");
    }

    [Test]
    public async Task Should_Throw_When_Items_Is_Empty()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new NavigationStructure("Title", new List<NavigationItem>()))
            .Throws<ArgumentException>()
            .WithMessage("Navigation structure must have at least one item (Parameter 'items')");
    }

    [Test]
    public async Task Should_Throw_When_Root_Item_Has_Non_Zero_Level()
    {
        // Arrange
        var invalidItem = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 1); // Level 1 instead of 0

        // Act & Assert
        await Assert.That(() => new NavigationStructure("Title", new[] { invalidItem }))
            .Throws<ArgumentException>()
            .WithMessage("Root navigation items must have level 0 (Parameter 'items')");
    }

    [Test]
    public async Task Should_Calculate_Total_Item_Count()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var count = structure.TotalItemCount;

        // Assert
        await Assert.That(count).IsEqualTo(6); // 3 chapters + 3 sections
    }

    [Test]
    public async Task Should_Calculate_Max_Depth()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var depth = structure.MaxDepth;

        // Assert
        await Assert.That(depth).IsEqualTo(2); // Level 0 and Level 1
    }

    [Test]
    public async Task Should_Find_Item_By_Id()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var found = structure.FindById("ch1.1");

        // Assert
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Title).IsEqualTo("Section 1.1");
    }

    [Test]
    public async Task Should_Find_Item_By_Href()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var found = structure.FindByHref("chapter2.xhtml");

        // Assert
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Title).IsEqualTo("Chapter 2");
    }

    [Test]
    public async Task Should_Get_All_Items_Flattened()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var allItems = structure.GetAllItems().ToList();

        // Assert
        await Assert.That(allItems).HasCount(6);
        var ids = allItems.Select(i => i.Id).ToList();
        await Assert.That(ids).Contains("ch1");
        await Assert.That(ids).Contains("ch1.1");
        await Assert.That(ids).Contains("ch1.2");
        await Assert.That(ids).Contains("ch2");
        await Assert.That(ids).Contains("ch2.1");
        await Assert.That(ids).Contains("ch3");
    }

    [Test]
    public async Task Should_Get_Items_At_Specific_Level()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var level0Items = structure.GetItemsAtLevel(0).ToList();
        var level1Items = structure.GetItemsAtLevel(1).ToList();

        // Assert
        await Assert.That(level0Items).HasCount(3);
        await Assert.That(level1Items).HasCount(3);
        await Assert.That(level0Items.All(i => i.Level == 0)).IsTrue();
        await Assert.That(level1Items.All(i => i.Level == 1)).IsTrue();
    }

    [Test]
    public async Task Should_Throw_When_Getting_Items_At_Negative_Level()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act & Assert
        await Assert.That(() => structure.GetItemsAtLevel(-1))
            .Throws<ArgumentOutOfRangeException>()
            .WithMessage("Level must be non-negative (Parameter 'level')");
    }

    [Test]
    public async Task Should_Get_Path_To_Item()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var path = structure.GetPathToItem("ch1.1")?.ToList();

        // Assert
        await Assert.That(path).IsNotNull();
        await Assert.That(path!).HasCount(2);
        await Assert.That(path[0].Id).IsEqualTo("ch1");
        await Assert.That(path[1].Id).IsEqualTo("ch1.1");
    }

    [Test]
    public async Task Should_Return_Null_Path_For_Non_Existent_Item()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure = new NavigationStructure("Title", items);

        // Act
        var path = structure.GetPathToItem("nonexistent");

        // Assert
        await Assert.That(path).IsNull();
    }

    [Test]
    public async Task Should_Validate_Epub2_Structure()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var epub2Structure = new NavigationStructure("Title", items, tocNcxPath: "OEBPS/toc.ncx", navPath: null);
        var epub3Structure = new NavigationStructure("Title", items, tocNcxPath: null, navPath: "OEBPS/nav.xhtml");

        // Act & Assert
        await Assert.That(epub2Structure.IsValidEpub2Structure()).IsTrue();
        await Assert.That(epub2Structure.IsValidEpub3Structure()).IsFalse();
        await Assert.That(epub3Structure.IsValidEpub2Structure()).IsFalse();
        await Assert.That(epub3Structure.IsValidEpub3Structure()).IsTrue();
    }

    [Test]
    public async Task Should_Be_Equal_For_Same_Values()
    {
        // Arrange
        var items1 = CreateTestNavigationItems();
        var items2 = CreateTestNavigationItems();
        var structure1 = new NavigationStructure("Title", items1, "toc.ncx", null);
        var structure2 = new NavigationStructure("Title", items2, "toc.ncx", null);

        // Act & Assert
        await Assert.That(structure1.Equals(structure2)).IsTrue();
        await Assert.That(structure1.GetHashCode()).IsEqualTo(structure2.GetHashCode());
    }

    [Test]
    public async Task Should_Not_Be_Equal_For_Different_Values()
    {
        // Arrange
        var items = CreateTestNavigationItems();
        var structure1 = new NavigationStructure("Title 1", items, "toc.ncx", null);
        var structure2 = new NavigationStructure("Title 2", items, "toc.ncx", null);

        // Act & Assert
        await Assert.That(structure1.Equals(structure2)).IsFalse();
    }
}