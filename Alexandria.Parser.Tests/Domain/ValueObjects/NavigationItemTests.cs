using Alexandria.Parser.Domain.ValueObjects;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Alexandria.Parser.Tests.Domain.ValueObjects;

public class NavigationItemTests
{
    [Test]
    public async Task Should_Create_NavigationItem_With_Valid_Data()
    {
        // Arrange & Act
        var item = new NavigationItem(
            id: "ch1",
            title: "Chapter 1",
            href: "chapter1.xhtml",
            playOrder: 1,
            level: 0);

        // Assert
        await Assert.That(item.Id).IsEqualTo("ch1");
        await Assert.That(item.Title).IsEqualTo("Chapter 1");
        await Assert.That(item.Href).IsEqualTo("chapter1.xhtml");
        await Assert.That(item.PlayOrder).IsEqualTo(1);
        await Assert.That(item.Level).IsEqualTo(0);
        await Assert.That(item.HasChildren).IsFalse();
    }

    [Test]
    public async Task Should_Create_NavigationItem_With_Children()
    {
        // Arrange
        var child1 = new NavigationItem("ch1.1", "Section 1.1", "ch1.xhtml#s1", 2, 1);
        var child2 = new NavigationItem("ch1.2", "Section 1.2", "ch1.xhtml#s2", 3, 1);

        // Act
        var parent = new NavigationItem(
            id: "ch1",
            title: "Chapter 1",
            href: "chapter1.xhtml",
            playOrder: 1,
            level: 0,
            children: new[] { child1, child2 });

        // Assert
        await Assert.That(parent.HasChildren).IsTrue();
        await Assert.That(parent.Children).HasCount(2);
        await Assert.That(parent.Children[0].Id).IsEqualTo("ch1.1");
        await Assert.That(parent.Children[1].Id).IsEqualTo("ch1.2");
    }

    [Test]
    public async Task Should_Throw_When_Id_Is_Empty()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new NavigationItem("", "Title", "href", 1, 0))
            .Throws<ArgumentException>()
            .WithMessage("Navigation item ID cannot be empty (Parameter 'id')");
    }

    [Test]
    public async Task Should_Throw_When_Title_Is_Empty()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new NavigationItem("id", "", "href", 1, 0))
            .Throws<ArgumentException>()
            .WithMessage("Navigation item title cannot be empty (Parameter 'title')");
    }

    [Test]
    public async Task Should_Throw_When_PlayOrder_Is_Negative()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new NavigationItem("id", "Title", "href", -1, 0))
            .Throws<ArgumentOutOfRangeException>()
            .WithMessage("Play order must be non-negative (Parameter 'playOrder')");
    }

    [Test]
    public async Task Should_Throw_When_Level_Is_Negative()
    {
        // Arrange & Act & Assert
        await Assert.That(() => new NavigationItem("id", "Title", "href", 1, -1))
            .Throws<ArgumentOutOfRangeException>()
            .WithMessage("Level must be non-negative (Parameter 'level')");
    }

    [Test]
    public async Task Should_Throw_When_Child_Level_Not_Greater_Than_Parent()
    {
        // Arrange
        var invalidChild = new NavigationItem("ch1.1", "Section 1.1", "ch1.xhtml#s1", 2, 0); // Same level as parent

        // Act & Assert
        await Assert.That(() => new NavigationItem(
            id: "ch1",
            title: "Chapter 1",
            href: "chapter1.xhtml",
            playOrder: 1,
            level: 0,
            children: new[] { invalidChild }))
            .Throws<ArgumentException>()
            .WithMessage("Child navigation item must have a level greater than parent level 0 (Parameter 'children')");
    }

    [Test]
    public async Task Should_Find_Item_By_Id()
    {
        // Arrange
        var child = new NavigationItem("ch1.1", "Section 1.1", "ch1.xhtml#s1", 2, 1);
        var parent = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0, new[] { child });

        // Act
        var found = parent.FindById("ch1.1");

        // Assert
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Id).IsEqualTo("ch1.1");
    }

    [Test]
    public async Task Should_Return_Null_When_Id_Not_Found()
    {
        // Arrange
        var item = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0);

        // Act
        var found = item.FindById("nonexistent");

        // Assert
        await Assert.That(found).IsNull();
    }

    [Test]
    public async Task Should_Find_Item_By_Href()
    {
        // Arrange
        var child = new NavigationItem("ch1.1", "Section 1.1", "ch1.xhtml#s1", 2, 1);
        var parent = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0, new[] { child });

        // Act
        var found = parent.FindByHref("ch1.xhtml#s1");

        // Assert
        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Href).IsEqualTo("ch1.xhtml#s1");
    }

    [Test]
    public async Task Should_Flatten_Navigation_Structure()
    {
        // Arrange
        var grandchild = new NavigationItem("ch1.1.1", "Subsection 1.1.1", "ch1.xhtml#ss1", 3, 2);
        var child = new NavigationItem("ch1.1", "Section 1.1", "ch1.xhtml#s1", 2, 1, new[] { grandchild });
        var parent = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0, new[] { child });

        // Act
        var flattened = parent.Flatten().ToList();

        // Assert
        await Assert.That(flattened).HasCount(3);
        await Assert.That(flattened[0].Id).IsEqualTo("ch1");
        await Assert.That(flattened[1].Id).IsEqualTo("ch1.1");
        await Assert.That(flattened[2].Id).IsEqualTo("ch1.1.1");
    }

    [Test]
    public async Task Should_Be_Equal_For_Same_Values()
    {
        // Arrange
        var item1 = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0);
        var item2 = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0);

        // Act & Assert
        await Assert.That(item1.Equals(item2)).IsTrue();
        await Assert.That(item1.GetHashCode()).IsEqualTo(item2.GetHashCode());
    }

    [Test]
    public async Task Should_Not_Be_Equal_For_Different_Values()
    {
        // Arrange
        var item1 = new NavigationItem("ch1", "Chapter 1", "chapter1.xhtml", 1, 0);
        var item2 = new NavigationItem("ch2", "Chapter 2", "chapter2.xhtml", 2, 0);

        // Act & Assert
        await Assert.That(item1.Equals(item2)).IsFalse();
    }
}