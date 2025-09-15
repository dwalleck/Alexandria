using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Alexandria.Domain;

/// <summary>
/// Specification pattern interface for building complex query criteria.
/// </summary>
/// <typeparam name="T">The entity type to apply the specification to</typeparam>
/// <remarks>
/// The Specification pattern encapsulates query logic into reusable, composable units.
/// This enables:
/// - Testable query logic
/// - Reusable query components
/// - Complex queries through composition
/// - Separation of query concerns from repository implementation
/// </remarks>
public interface ISpecification<T>
{
    /// <summary>
    /// Gets the criteria expression for filtering entities.
    /// </summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>
    /// Gets the list of include expressions for eager loading related data.
    /// </summary>
    List<Expression<Func<T, object>>> Includes { get; }

    /// <summary>
    /// Gets the list of include strings for eager loading related data (string-based).
    /// </summary>
    List<string> IncludeStrings { get; }

    /// <summary>
    /// Gets the order by expression for sorting results.
    /// </summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>
    /// Gets the order by descending expression for sorting results.
    /// </summary>
    Expression<Func<T, object>>? OrderByDescending { get; }

    /// <summary>
    /// Gets additional order by expressions for multi-level sorting.
    /// </summary>
    List<Expression<Func<T, object>>> ThenByList { get; }

    /// <summary>
    /// Gets additional order by descending expressions for multi-level sorting.
    /// </summary>
    List<Expression<Func<T, object>>> ThenByDescendingList { get; }

    /// <summary>
    /// Gets the number of items to take (for limiting results).
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Gets the number of items to skip (for pagination).
    /// </summary>
    int? Skip { get; }

    /// <summary>
    /// Gets whether paging is enabled for this specification.
    /// </summary>
    bool IsPagingEnabled { get; }

    /// <summary>
    /// Gets whether to track entities for changes (EF Core specific).
    /// </summary>
    bool AsNoTracking { get; }

    /// <summary>
    /// Gets whether to use split queries for includes (EF Core specific).
    /// </summary>
    bool AsSplitQuery { get; }

    /// <summary>
    /// Gets whether to ignore query filters (EF Core specific).
    /// </summary>
    bool IgnoreQueryFilters { get; }

    /// <summary>
    /// Evaluates whether an entity satisfies the specification.
    /// </summary>
    /// <param name="entity">The entity to evaluate</param>
    /// <returns>True if the entity satisfies the specification</returns>
    bool IsSatisfiedBy(T entity);

    /// <summary>
    /// Combines this specification with another using AND logic.
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new combined specification</returns>
    ISpecification<T> And(ISpecification<T> specification);

    /// <summary>
    /// Combines this specification with another using OR logic.
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new combined specification</returns>
    ISpecification<T> Or(ISpecification<T> specification);

    /// <summary>
    /// Creates a NOT specification from this specification.
    /// </summary>
    /// <returns>A new negated specification</returns>
    ISpecification<T> Not();
}