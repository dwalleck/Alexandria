using System;
using System.Collections.Generic;
using LiteDB;

namespace Alexandria.Infrastructure.Persistence.LiteDb.Models;

/// <summary>
/// Data Transfer Object for Book persistence in LiteDB.
/// Mutable structure that can be serialized/deserialized by LiteDB.
/// </summary>
internal sealed class BookDto
{
    [BsonId]
    public Guid Id { get; set; }
    public BookTitleDto Title { get; set; } = null!;
    public List<BookTitleDto> AlternateTitles { get; set; } = new();
    public List<AuthorDto> Authors { get; set; } = new();
    public List<ChapterDto> Chapters { get; set; } = new();
    public List<BookIdentifierDto> Identifiers { get; set; } = new();
    public LanguageDto Language { get; set; } = null!;
    public BookMetadataDto Metadata { get; set; } = null!;
    public NavigationStructureDto? NavigationStructure { get; set; }
    public ResourceCollectionDto? Resources { get; set; }
}

internal sealed class BookTitleDto
{
    public string Value { get; set; } = string.Empty;
}

internal sealed class AuthorDto
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? FileAs { get; set; }
}

internal sealed class ChapterDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Order { get; set; }
    public string? Href { get; set; }
}

internal sealed class BookIdentifierDto
{
    public string? Scheme { get; set; }
    public string Value { get; set; } = string.Empty;
}

internal sealed class LanguageDto
{
    public string Code { get; set; } = string.Empty;
}

internal sealed class BookMetadataDto
{
    public string? Publisher { get; set; }
    public DateTime? PublicationDate { get; set; }
    public string? Description { get; set; }
    public string? Rights { get; set; }
    public string? Subject { get; set; }
    public string? Coverage { get; set; }
    public string? Isbn { get; set; }
    public string? Series { get; set; }
    public int? SeriesNumber { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? EpubVersion { get; set; }
    public Dictionary<string, string>? CustomMetadata { get; set; }
}

internal sealed class NavigationStructureDto
{
    public List<NavigationItemDto> Items { get; set; } = new();
}

internal sealed class NavigationItemDto
{
    public string? Label { get; set; }
    public string? Href { get; set; }
    public int Level { get; set; }
    public List<NavigationItemDto> Children { get; set; } = new();
}

internal sealed class ResourceCollectionDto
{
    public List<EpubResourceDto> Resources { get; set; } = new();
    public ImageResourceDto? CoverImage { get; set; }
}

internal class EpubResourceDto
{
    public string Id { get; set; } = string.Empty;
    public string Href { get; set; } = string.Empty;
    public string MediaType { get; set; } = string.Empty;
    public byte[]? Content { get; set; }
}

internal sealed class ImageResourceDto : EpubResourceDto
{
    public bool IsCoverImage { get; set; }
}