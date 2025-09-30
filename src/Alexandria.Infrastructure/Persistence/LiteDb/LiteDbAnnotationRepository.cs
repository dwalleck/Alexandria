using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Alexandria.Domain.Repositories;
using Alexandria.Domain.ValueObjects;
using Alexandria.Infrastructure.Persistence.LiteDb.Mappers;
using Alexandria.Infrastructure.Persistence.LiteDb.Models;
using LiteDB;

namespace Alexandria.Infrastructure.Persistence.LiteDb;

/// <summary>
/// Document wrapper for storing annotations with book association.
/// </summary>
internal sealed class AnnotationDocument
{
    public string Id { get; set; } = string.Empty;
    public Guid BookId { get; set; }
    public AnnotationDto Annotation { get; set; } = null!;
}

/// <summary>
/// LiteDB implementation of IAnnotationRepository.
/// Provides persistence for user annotations and highlights using LiteDB document database.
/// Uses DTOs internally for proper LiteDB serialization.
/// </summary>
public sealed class LiteDbAnnotationRepository : IAnnotationRepository
{
    private readonly LiteDbContext _context;
    private readonly ILiteCollection<AnnotationDocument> _annotations;

    public LiteDbAnnotationRepository(LiteDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _annotations = _context.Database.GetCollection<AnnotationDocument>("annotations");
    }

    public ValueTask<Annotation?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var doc = _annotations.FindOne(a => a.Id == id);
        var annotation = doc != null ? AnnotationMapper.FromDto(doc.Annotation) : null;
        return ValueTask.FromResult(annotation);
    }

    public Task<IReadOnlyList<Annotation>> GetByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _annotations
            .Query()
            .Where(a => a.BookId == bookId)
            .ToList();

        var annotations = docs.Select(d => AnnotationMapper.FromDto(d.Annotation)).ToList();
        return Task.FromResult<IReadOnlyList<Annotation>>(annotations);
    }

    public Task<IReadOnlyList<Annotation>> GetByChapterIdAsync(string chapterId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(chapterId);
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _annotations
            .Query()
            .Where(a => a.Annotation.ChapterId == chapterId)
            .ToList();

        var annotations = docs.Select(d => AnnotationMapper.FromDto(d.Annotation)).ToList();
        return Task.FromResult<IReadOnlyList<Annotation>>(annotations);
    }

    public Task<IReadOnlyList<Annotation>> GetByColorAsync(HighlightColor color, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _annotations
            .Query()
            .Where(a => a.Annotation.Color == color)
            .ToList();

        var annotations = docs.Select(d => AnnotationMapper.FromDto(d.Annotation)).ToList();
        return Task.FromResult<IReadOnlyList<Annotation>>(annotations);
    }

    public Task<IReadOnlyList<Annotation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var docs = _annotations
            .Query()
            .OrderByDescending(a => a.Annotation.CreatedAt)
            .ToList();

        var annotations = docs.Select(d => AnnotationMapper.FromDto(d.Annotation)).ToList();
        return Task.FromResult<IReadOnlyList<Annotation>>(annotations);
    }

    public Task<Annotation> AddAsync(Annotation annotation, Guid bookId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        cancellationToken.ThrowIfCancellationRequested();

        var dto = AnnotationMapper.ToDto(annotation);
        var doc = new AnnotationDocument
        {
            Id = annotation.Id,
            BookId = bookId,
            Annotation = dto
        };

        _annotations.Insert(doc);
        return Task.FromResult(annotation);
    }

    public Task<Annotation> UpdateAsync(Annotation annotation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        cancellationToken.ThrowIfCancellationRequested();

        var doc = _annotations.FindOne(a => a.Id == annotation.Id);
        if (doc == null)
        {
            throw new InvalidOperationException($"Annotation with ID {annotation.Id} not found");
        }

        doc.Annotation = AnnotationMapper.ToDto(annotation);
        _annotations.Update(doc);

        return Task.FromResult(annotation);
    }

    public Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var result = _annotations.DeleteMany(a => a.Id == id);
        return Task.FromResult(result > 0);
    }

    public Task<int> RemoveByBookIdAsync(Guid bookId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _annotations.DeleteMany(a => a.BookId == bookId);
        return Task.FromResult(count);
    }

    public ValueTask<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        cancellationToken.ThrowIfCancellationRequested();

        var exists = _annotations.Exists(a => a.Id == id);
        return ValueTask.FromResult(exists);
    }

    public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var count = _annotations.Count();
        return ValueTask.FromResult(count);
    }
}