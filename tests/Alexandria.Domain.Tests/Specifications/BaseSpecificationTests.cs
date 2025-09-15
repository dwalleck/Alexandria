using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Alexandria.Domain.Specifications;
using TUnit.Assertions;
using TUnit.Core;

namespace Alexandria.Domain.Tests.Specifications;

public class BaseSpecificationTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsActive { get; set; }
        public TestRelatedEntity? Related { get; set; }
    }

    private class TestRelatedEntity
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    private class TestSpecification : BaseSpecification<TestEntity>
    {
        public TestSpecification() { }
        
        public TestSpecification(Expression<Func<TestEntity, bool>> criteria)
            : base(criteria) { }

        public void AddCriteriaPublic(Expression<Func<TestEntity, bool>> criteria)
        {
            AddCriteria(criteria);
        }

        public void AddIncludePublic(Expression<Func<TestEntity, object>> includeExpression)
        {
            AddInclude(includeExpression);
        }

        public void AddIncludePublic(string includeString)
        {
            AddInclude(includeString);
        }

        public void ApplyOrderByPublic(Expression<Func<TestEntity, object>> orderByExpression)
        {
            ApplyOrderBy(orderByExpression);
        }

        public void ApplyOrderByDescendingPublic(Expression<Func<TestEntity, object>> orderByDescendingExpression)
        {
            ApplyOrderByDescending(orderByDescendingExpression);
        }

        public void AddThenByPublic(Expression<Func<TestEntity, object>> thenByExpression)
        {
            AddThenBy(thenByExpression);
        }

        public void AddThenByDescendingPublic(Expression<Func<TestEntity, object>> thenByDescendingExpression)
        {
            AddThenByDescending(thenByDescendingExpression);
        }

        public void ApplyPagingPublic(int skip, int take)
        {
            ApplyPaging(skip, take);
        }

        public void ApplyNoTrackingPublic()
        {
            ApplyNoTracking();
        }

        public void ApplyTrackingPublic()
        {
            ApplyTracking();
        }

        public void ApplySplitQueryPublic()
        {
            ApplySplitQuery();
        }

        public void ApplyIgnoreQueryFiltersPublic()
        {
            ApplyIgnoreQueryFilters();
        }
    }

    [Test]
    public async Task Constructor_WithoutCriteria_CreateEmptySpecification()
    {
        var spec = new TestSpecification();

        await Assert.That(spec.Criteria).IsNull();
        await Assert.That(spec.Includes).IsEmpty();
        await Assert.That(spec.IncludeStrings).IsEmpty();
        await Assert.That(spec.OrderBy).IsNull();
        await Assert.That(spec.OrderByDescending).IsNull();
        await Assert.That(spec.IsPagingEnabled).IsFalse();
        await Assert.That(spec.AsNoTracking).IsTrue();
        await Assert.That(spec.AsSplitQuery).IsFalse();
        await Assert.That(spec.IgnoreQueryFilters).IsFalse();
    }

    [Test]
    public async Task Constructor_WithCriteria_SetsCriteria()
    {
        Expression<Func<TestEntity, bool>> criteria = x => x.Id == 1;
        var spec = new TestSpecification(criteria);

        await Assert.That(spec.Criteria).IsNotNull();
        await Assert.That(spec.Criteria).IsEqualTo(criteria);
    }

    [Test]
    public async Task AddCriteria_SetsCriteria()
    {
        var spec = new TestSpecification();
        Expression<Func<TestEntity, bool>> criteria = x => x.Name == "Test";
        
        spec.AddCriteriaPublic(criteria);

        await Assert.That(spec.Criteria).IsNotNull();
        await Assert.That(spec.Criteria).IsEqualTo(criteria);
    }

    [Test]
    public async Task AddInclude_WithExpression_AddsToIncludesList()
    {
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> include = x => x.Related!;
        
        spec.AddIncludePublic(include);

        await Assert.That(spec.Includes.Count).IsEqualTo(1);
        await Assert.That(spec.Includes[0]).IsEqualTo(include);
    }

    [Test]
    public async Task AddInclude_WithString_AddsToIncludeStringsList()
    {
        var spec = new TestSpecification();
        const string includeString = "Related.SubRelated";
        
        spec.AddIncludePublic(includeString);

        await Assert.That(spec.IncludeStrings.Count).IsEqualTo(1);
        await Assert.That(spec.IncludeStrings[0]).IsEqualTo(includeString);
    }

    [Test]
    public async Task ApplyOrderBy_SetsOrderByExpression()
    {
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> orderBy = x => x.Name;
        
        spec.ApplyOrderByPublic(orderBy);

        await Assert.That(spec.OrderBy).IsNotNull();
        await Assert.That(spec.OrderBy).IsEqualTo(orderBy);
    }

    [Test]
    public async Task ApplyOrderByDescending_SetsOrderByDescendingExpression()
    {
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> orderByDesc = x => x.Value;
        
        spec.ApplyOrderByDescendingPublic(orderByDesc);

        await Assert.That(spec.OrderByDescending).IsNotNull();
        await Assert.That(spec.OrderByDescending).IsEqualTo(orderByDesc);
    }

    [Test]
    public async Task AddThenBy_AddsToThenByList()
    {
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> thenBy1 = x => x.Name;
        Expression<Func<TestEntity, object>> thenBy2 = x => x.Value;
        
        spec.AddThenByPublic(thenBy1);
        spec.AddThenByPublic(thenBy2);

        await Assert.That(spec.ThenByList.Count).IsEqualTo(2);
        await Assert.That(spec.ThenByList[0]).IsEqualTo(thenBy1);
        await Assert.That(spec.ThenByList[1]).IsEqualTo(thenBy2);
    }

    [Test]
    public async Task AddThenByDescending_AddsToThenByDescendingList()
    {
        var spec = new TestSpecification();
        Expression<Func<TestEntity, object>> thenByDesc = x => x.CreatedDate;
        
        spec.AddThenByDescendingPublic(thenByDesc);

        await Assert.That(spec.ThenByDescendingList.Count).IsEqualTo(1);
        await Assert.That(spec.ThenByDescendingList[0]).IsEqualTo(thenByDesc);
    }

    [Test]
    public async Task ApplyPaging_SetsPagingProperties()
    {
        var spec = new TestSpecification();
        
        spec.ApplyPagingPublic(10, 20);

        await Assert.That(spec.Skip).IsEqualTo(10);
        await Assert.That(spec.Take).IsEqualTo(20);
        await Assert.That(spec.IsPagingEnabled).IsTrue();
    }

    [Test]
    public async Task ApplyNoTracking_SetsAsNoTrackingToTrue()
    {
        var spec = new TestSpecification();
        spec.ApplyTrackingPublic(); // First set to false
        
        spec.ApplyNoTrackingPublic();

        await Assert.That(spec.AsNoTracking).IsTrue();
    }

    [Test]
    public async Task ApplyTracking_SetsAsNoTrackingToFalse()
    {
        var spec = new TestSpecification();
        
        spec.ApplyTrackingPublic();

        await Assert.That(spec.AsNoTracking).IsFalse();
    }

    [Test]
    public async Task ApplySplitQuery_SetsAsSplitQueryToTrue()
    {
        var spec = new TestSpecification();
        
        spec.ApplySplitQueryPublic();

        await Assert.That(spec.AsSplitQuery).IsTrue();
    }

    [Test]
    public async Task ApplyIgnoreQueryFilters_SetsIgnoreQueryFiltersToTrue()
    {
        var spec = new TestSpecification();
        
        spec.ApplyIgnoreQueryFiltersPublic();

        await Assert.That(spec.IgnoreQueryFilters).IsTrue();
    }

    [Test]
    public async Task IsSatisfiedBy_WithNoCriteria_ReturnsTrue()
    {
        var spec = new TestSpecification();
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = spec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsSatisfiedBy_WithMatchingCriteria_ReturnsTrue()
    {
        var spec = new TestSpecification(x => x.Id == 1);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = spec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsSatisfiedBy_WithNonMatchingCriteria_ReturnsFalse()
    {
        var spec = new TestSpecification(x => x.Id == 2);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = spec.IsSatisfiedBy(entity);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task And_CombinesTwoSpecifications()
    {
        var spec1 = new TestSpecification(x => x.Id > 0);
        var spec2 = new TestSpecification(x => x.Name == "Test");

        var combined = spec1.And(spec2);

        await Assert.That(combined).IsNotNull();
        await Assert.That(combined).IsTypeOf<AndSpecification<TestEntity>>();
    }

    [Test]
    public async Task Or_CombinesTwoSpecifications()
    {
        var spec1 = new TestSpecification(x => x.Id == 1);
        var spec2 = new TestSpecification(x => x.Name == "Test");

        var combined = spec1.Or(spec2);

        await Assert.That(combined).IsNotNull();
        await Assert.That(combined).IsTypeOf<OrSpecification<TestEntity>>();
    }

    [Test]
    public async Task Not_NegatesSpecification()
    {
        var spec = new TestSpecification(x => x.Id == 1);

        var negated = spec.Not();

        await Assert.That(negated).IsNotNull();
        await Assert.That(negated).IsTypeOf<NotSpecification<TestEntity>>();
    }
}