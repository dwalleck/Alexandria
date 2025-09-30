using System;
using System.Linq;
using System.Collections.Generic;
using Alexandria.Domain.Entities;
using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb.Models;

namespace Alexandria.Infrastructure.Persistence.LiteDb.Mappers;

/// <summary>
/// Maps between Book domain entity and BookDto persistence model.
/// </summary>
internal static class BookMapper
{
    public static BookDto ToDto(Book book)
    {
        return new BookDto
        {
            Id = book.Id,
            Title = new BookTitleDto { Value = book.Title.Value },
            AlternateTitles = book.AlternateTitles.Select(t => new BookTitleDto { Value = t.Value }).ToList(),
            Authors = book.Authors.Select(a => new AuthorDto
            {
                Name = a.Name,
                Role = a.Role,
                FileAs = a.FileAs
            }).ToList(),
            Chapters = book.Chapters.Select(c => new ChapterDto
            {
                Id = c.Id,
                Title = c.Title,
                Content = c.Content,
                Order = c.Order,
                Href = c.Href
            }).ToList(),
            Identifiers = book.Identifiers.Select(i => new BookIdentifierDto
            {
                Scheme = i.Scheme,
                Value = i.Value
            }).ToList(),
            Language = new LanguageDto { Code = book.Language.Code },
            Metadata = new BookMetadataDto
            {
                Publisher = book.Metadata.Publisher,
                PublicationDate = book.Metadata.PublicationDate,
                Description = book.Metadata.Description,
                Rights = book.Metadata.Rights,
                Subject = book.Metadata.Subject,
                Coverage = book.Metadata.Coverage,
                Isbn = book.Metadata.Isbn,
                Series = book.Metadata.Series,
                SeriesNumber = book.Metadata.SeriesNumber,
                Tags = book.Metadata.Tags.ToList(),
                EpubVersion = book.Metadata.EpubVersion,
                CustomMetadata = book.Metadata.CustomMetadata != null
                    ? new Dictionary<string, string>(book.Metadata.CustomMetadata)
                    : null
            },
            NavigationStructure = null, // Simplified - skip navigation for now
            Resources = null // Simplified - skip resources for now
        };
    }

    public static Book FromDto(BookDto dto)
    {
        var title = new BookTitle(dto.Title.Value);
        var alternateTitles = dto.AlternateTitles.Select(t => new BookTitle(t.Value));
        var authors = dto.Authors.Select(a => new Author(a.Name, a.Role, a.FileAs));
        var chapters = dto.Chapters.Select(c => new Chapter(c.Id, c.Title, c.Content, c.Order, c.Href));
        var identifiers = dto.Identifiers.Select(i => new BookIdentifier(i.Scheme, i.Value));
        var language = new Language(dto.Language.Code);
        var metadata = new BookMetadata(
            publisher: dto.Metadata.Publisher,
            publicationDate: dto.Metadata.PublicationDate,
            description: dto.Metadata.Description,
            rights: dto.Metadata.Rights,
            subject: dto.Metadata.Subject,
            coverage: dto.Metadata.Coverage,
            isbn: dto.Metadata.Isbn,
            series: dto.Metadata.Series,
            seriesNumber: dto.Metadata.SeriesNumber,
            tags: dto.Metadata.Tags,
            epubVersion: dto.Metadata.EpubVersion,
            customMetadata: dto.Metadata.CustomMetadata
        );

        var book = new Book(
            title: title,
            alternateTitles: alternateTitles,
            authors: authors,
            chapters: chapters,
            identifiers: identifiers,
            language: language,
            metadata: metadata,
            navigationStructure: null, // Simplified
            resources: null, // Simplified
            id: dto.Id
        );

        return book;
    }
}