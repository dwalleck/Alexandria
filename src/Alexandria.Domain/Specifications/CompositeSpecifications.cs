using System;
using System.Linq;
using System.Linq.Expressions;

namespace Alexandria.Domain.Specifications;

/// <summary>
/// Specification that combines two specifications using AND logic.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class AndSpecification<T> : BaseSpecification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    /// <summary>
    /// Initializes a new instance of the <see cref="AndSpecification{T}"/> class.
    /// </summary>
    /// <param name="left">The left specification</param>
    /// <param name="right">The right specification</param>
    public AndSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));

        // Combine criteria
        if (_left.Criteria != null && _right.Criteria != null)
        {
            AddCriteria(CombineAnd(_left.Criteria, _right.Criteria));
        }
        else if (_left.Criteria != null)
        {
            AddCriteria(_left.Criteria);
        }
        else if (_right.Criteria != null)
        {
            AddCriteria(_right.Criteria);
        }

        // Merge includes
        foreach (var include in _left.Includes.Concat(_right.Includes))
        {
            AddInclude(include);
        }

        foreach (var includeString in _left.IncludeStrings.Concat(_right.IncludeStrings))
        {
            AddInclude(includeString);
        }
    }

    /// <inheritdoc />
    public override bool IsSatisfiedBy(T entity)
    {
        return _left.IsSatisfiedBy(entity) && _right.IsSatisfiedBy(entity);
    }

    private static Expression<Func<T, bool>> CombineAnd(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T));
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        var body = Expression.AndAlso(leftBody, rightBody);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}

/// <summary>
/// Specification that combines two specifications using OR logic.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class OrSpecification<T> : BaseSpecification<T>
{
    private readonly ISpecification<T> _left;
    private readonly ISpecification<T> _right;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrSpecification{T}"/> class.
    /// </summary>
    /// <param name="left">The left specification</param>
    /// <param name="right">The right specification</param>
    public OrSpecification(ISpecification<T> left, ISpecification<T> right)
    {
        _left = left ?? throw new ArgumentNullException(nameof(left));
        _right = right ?? throw new ArgumentNullException(nameof(right));

        // Combine criteria
        if (_left.Criteria != null && _right.Criteria != null)
        {
            AddCriteria(CombineOr(_left.Criteria, _right.Criteria));
        }
        else if (_left.Criteria != null)
        {
            AddCriteria(_left.Criteria);
        }
        else if (_right.Criteria != null)
        {
            AddCriteria(_right.Criteria);
        }

        // Merge includes
        foreach (var include in _left.Includes.Concat(_right.Includes))
        {
            AddInclude(include);
        }

        foreach (var includeString in _left.IncludeStrings.Concat(_right.IncludeStrings))
        {
            AddInclude(includeString);
        }
    }

    /// <inheritdoc />
    public override bool IsSatisfiedBy(T entity)
    {
        return _left.IsSatisfiedBy(entity) || _right.IsSatisfiedBy(entity);
    }

    private static Expression<Func<T, bool>> CombineOr(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T));
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        var body = Expression.OrElse(leftBody, rightBody);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private static Expression ReplaceParameter(Expression expression, ParameterExpression oldParameter, ParameterExpression newParameter)
    {
        return new ParameterReplacer(oldParameter, newParameter).Visit(expression);
    }

    private class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _oldParameter ? _newParameter : base.VisitParameter(node);
        }
    }
}

/// <summary>
/// Specification that negates another specification.
/// </summary>
/// <typeparam name="T">The entity type</typeparam>
public class NotSpecification<T> : BaseSpecification<T>
{
    private readonly ISpecification<T> _specification;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotSpecification{T}"/> class.
    /// </summary>
    /// <param name="specification">The specification to negate</param>
    public NotSpecification(ISpecification<T> specification)
    {
        _specification = specification ?? throw new ArgumentNullException(nameof(specification));

        if (_specification.Criteria != null)
        {
            AddCriteria(Negate(_specification.Criteria));
        }

        // Copy includes
        foreach (var include in _specification.Includes)
        {
            AddInclude(include);
        }

        foreach (var includeString in _specification.IncludeStrings)
        {
            AddInclude(includeString);
        }
    }

    /// <inheritdoc />
    public override bool IsSatisfiedBy(T entity)
    {
        return !_specification.IsSatisfiedBy(entity);
    }

    private static Expression<Func<T, bool>> Negate(Expression<Func<T, bool>> expression)
    {
        var parameter = expression.Parameters[0];
        var body = Expression.Not(expression.Body);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }
}