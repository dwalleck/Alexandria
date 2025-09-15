using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Alexandria.Domain.Specifications;

/// <summary>
/// Base implementation of the specification pattern.
/// </summary>
/// <typeparam name="T">The entity type to apply the specification to</typeparam>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = new();
    private readonly List<string> _includeStrings = new();
    private readonly List<Expression<Func<T, object>>> _thenByList = new();
    private readonly List<Expression<Func<T, object>>> _thenByDescendingList = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseSpecification{T}"/> class.
    /// </summary>
    protected BaseSpecification()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseSpecification{T}"/> class with criteria.
    /// </summary>
    /// <param name="criteria">The criteria expression</param>
    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    /// <inheritdoc />
    public Expression<Func<T, bool>>? Criteria { get; private set; }

    /// <inheritdoc />
    public List<Expression<Func<T, object>>> Includes => _includes;

    /// <inheritdoc />
    public List<string> IncludeStrings => _includeStrings;

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderBy { get; private set; }

    /// <inheritdoc />
    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    /// <inheritdoc />
    public List<Expression<Func<T, object>>> ThenByList => _thenByList;

    /// <inheritdoc />
    public List<Expression<Func<T, object>>> ThenByDescendingList => _thenByDescendingList;

    /// <inheritdoc />
    public int? Take { get; private set; }

    /// <inheritdoc />
    public int? Skip { get; private set; }

    /// <inheritdoc />
    public bool IsPagingEnabled { get; private set; }

    /// <inheritdoc />
    public bool AsNoTracking { get; private set; } = true;

    /// <inheritdoc />
    public bool AsSplitQuery { get; private set; }

    /// <inheritdoc />
    public bool IgnoreQueryFilters { get; private set; }

    /// <inheritdoc />
    public virtual bool IsSatisfiedBy(T entity)
    {
        if (Criteria == null)
            return true;

        var compiledCriteria = Criteria.Compile();
        return compiledCriteria(entity);
    }

    /// <summary>
    /// Adds a criteria expression to the specification.
    /// </summary>
    /// <param name="criteria">The criteria expression</param>
    protected void AddCriteria(Expression<Func<T, bool>> criteria)
    {
        Criteria = criteria;
    }

    /// <summary>
    /// Adds an include expression for eager loading.
    /// </summary>
    /// <param name="includeExpression">The include expression</param>
    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
        _includes.Add(includeExpression);
    }

    /// <summary>
    /// Adds an include string for eager loading.
    /// </summary>
    /// <param name="includeString">The include string</param>
    protected void AddInclude(string includeString)
    {
        _includeStrings.Add(includeString);
    }

    /// <summary>
    /// Applies ordering to the specification.
    /// </summary>
    /// <param name="orderByExpression">The order by expression</param>
    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
        OrderBy = orderByExpression;
    }

    /// <summary>
    /// Applies descending ordering to the specification.
    /// </summary>
    /// <param name="orderByDescendingExpression">The order by descending expression</param>
    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
    {
        OrderByDescending = orderByDescendingExpression;
    }

    /// <summary>
    /// Adds a secondary ordering to the specification.
    /// </summary>
    /// <param name="thenByExpression">The then by expression</param>
    protected void AddThenBy(Expression<Func<T, object>> thenByExpression)
    {
        _thenByList.Add(thenByExpression);
    }

    /// <summary>
    /// Adds a secondary descending ordering to the specification.
    /// </summary>
    /// <param name="thenByDescendingExpression">The then by descending expression</param>
    protected void AddThenByDescending(Expression<Func<T, object>> thenByDescendingExpression)
    {
        _thenByDescendingList.Add(thenByDescendingExpression);
    }

    /// <summary>
    /// Applies paging to the specification.
    /// </summary>
    /// <param name="skip">Number of items to skip</param>
    /// <param name="take">Number of items to take</param>
    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
        IsPagingEnabled = true;
    }

    /// <summary>
    /// Configures the specification to not track entities.
    /// </summary>
    protected void ApplyNoTracking()
    {
        AsNoTracking = true;
    }

    /// <summary>
    /// Configures the specification to track entities.
    /// </summary>
    protected void ApplyTracking()
    {
        AsNoTracking = false;
    }

    /// <summary>
    /// Configures the specification to use split queries.
    /// </summary>
    protected void ApplySplitQuery()
    {
        AsSplitQuery = true;
    }

    /// <summary>
    /// Configures the specification to ignore query filters.
    /// </summary>
    protected void ApplyIgnoreQueryFilters()
    {
        IgnoreQueryFilters = true;
    }

    /// <summary>
    /// Combines this specification with another using AND logic.
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new combined specification</returns>
    public ISpecification<T> And(ISpecification<T> specification)
    {
        return new AndSpecification<T>(this, specification);
    }

    /// <summary>
    /// Combines this specification with another using OR logic.
    /// </summary>
    /// <param name="specification">The specification to combine with</param>
    /// <returns>A new combined specification</returns>
    public ISpecification<T> Or(ISpecification<T> specification)
    {
        return new OrSpecification<T>(this, specification);
    }

    /// <summary>
    /// Creates a NOT specification from this specification.
    /// </summary>
    /// <returns>A new negated specification</returns>
    public ISpecification<T> Not()
    {
        return new NotSpecification<T>(this);
    }
}