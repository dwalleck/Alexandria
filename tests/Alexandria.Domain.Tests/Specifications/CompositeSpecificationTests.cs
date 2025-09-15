using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Alexandria.Domain.Specifications;
using TUnit.Assertions;
using TUnit.Core;

namespace Alexandria.Domain.Tests.Specifications;

public class CompositeSpecificationTests
{
    private class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public bool IsActive { get; set; }
    }

    private class SimpleSpecification : BaseSpecification<TestEntity>
    {
        public SimpleSpecification(Expression<Func<TestEntity, bool>> criteria)
            : base(criteria) { }
    }

    [Test]
    public async Task AndSpecification_WithNullLeft_ThrowsArgumentNullException()
    {
        var right = new SimpleSpecification(x => x.Id == 1);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(new AndSpecification<TestEntity>(null!, right)));
    }

    [Test]
    public async Task AndSpecification_WithNullRight_ThrowsArgumentNullException()
    {
        var left = new SimpleSpecification(x => x.Id == 1);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(new AndSpecification<TestEntity>(left, null!)));
    }

    [Test]
    public async Task AndSpecification_CombinesCriteria()
    {
        var left = new SimpleSpecification(x => x.Id > 0);
        var right = new SimpleSpecification(x => x.Name == "Test");

        var andSpec = new AndSpecification<TestEntity>(left, right);

        await Assert.That(andSpec.Criteria).IsNotNull();
    }

    [Test]
    public async Task AndSpecification_IsSatisfiedBy_BothTrue_ReturnsTrue()
    {
        var left = new SimpleSpecification(x => x.Id > 0);
        var right = new SimpleSpecification(x => x.Name == "Test");
        var andSpec = new AndSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = andSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task AndSpecification_IsSatisfiedBy_LeftFalse_ReturnsFalse()
    {
        var left = new SimpleSpecification(x => x.Id < 0);
        var right = new SimpleSpecification(x => x.Name == "Test");
        var andSpec = new AndSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = andSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AndSpecification_IsSatisfiedBy_RightFalse_ReturnsFalse()
    {
        var left = new SimpleSpecification(x => x.Id > 0);
        var right = new SimpleSpecification(x => x.Name == "Wrong");
        var andSpec = new AndSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = andSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AndSpecification_IsSatisfiedBy_BothFalse_ReturnsFalse()
    {
        var left = new SimpleSpecification(x => x.Id < 0);
        var right = new SimpleSpecification(x => x.Name == "Wrong");
        var andSpec = new AndSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = andSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task AndSpecification_MergesIncludes()
    {
        var left = new SimpleSpecification(x => x.Id > 0);
        left.Includes.Add(x => x.Name);
        left.IncludeStrings.Add("Related");
        
        var right = new SimpleSpecification(x => x.Name == "Test");
        right.Includes.Add(x => x.Value);
        right.IncludeStrings.Add("OtherRelated");

        var andSpec = new AndSpecification<TestEntity>(left, right);

        await Assert.That(andSpec.Includes.Count).IsEqualTo(2);
        await Assert.That(andSpec.IncludeStrings.Count).IsEqualTo(2);
        await Assert.That(andSpec.IncludeStrings).Contains("Related");
        await Assert.That(andSpec.IncludeStrings).Contains("OtherRelated");
    }

    [Test]
    public async Task OrSpecification_WithNullLeft_ThrowsArgumentNullException()
    {
        var right = new SimpleSpecification(x => x.Id == 1);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(new OrSpecification<TestEntity>(null!, right)));
    }

    [Test]
    public async Task OrSpecification_WithNullRight_ThrowsArgumentNullException()
    {
        var left = new SimpleSpecification(x => x.Id == 1);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(new OrSpecification<TestEntity>(left, null!)));
    }

    [Test]
    public async Task OrSpecification_CombinesCriteria()
    {
        var left = new SimpleSpecification(x => x.Id == 1);
        var right = new SimpleSpecification(x => x.Name == "Test");

        var orSpec = new OrSpecification<TestEntity>(left, right);

        await Assert.That(orSpec.Criteria).IsNotNull();
    }

    [Test]
    public async Task OrSpecification_IsSatisfiedBy_BothTrue_ReturnsTrue()
    {
        var left = new SimpleSpecification(x => x.Id > 0);
        var right = new SimpleSpecification(x => x.Name == "Test");
        var orSpec = new OrSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = orSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task OrSpecification_IsSatisfiedBy_LeftTrue_ReturnsTrue()
    {
        var left = new SimpleSpecification(x => x.Id > 0);
        var right = new SimpleSpecification(x => x.Name == "Wrong");
        var orSpec = new OrSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = orSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task OrSpecification_IsSatisfiedBy_RightTrue_ReturnsTrue()
    {
        var left = new SimpleSpecification(x => x.Id < 0);
        var right = new SimpleSpecification(x => x.Name == "Test");
        var orSpec = new OrSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = orSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task OrSpecification_IsSatisfiedBy_BothFalse_ReturnsFalse()
    {
        var left = new SimpleSpecification(x => x.Id < 0);
        var right = new SimpleSpecification(x => x.Name == "Wrong");
        var orSpec = new OrSpecification<TestEntity>(left, right);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = orSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task OrSpecification_MergesIncludes()
    {
        var left = new SimpleSpecification(x => x.Id == 1);
        left.Includes.Add(x => x.Name);
        left.IncludeStrings.Add("Related");
        
        var right = new SimpleSpecification(x => x.Name == "Test");
        right.Includes.Add(x => x.Value);
        right.IncludeStrings.Add("OtherRelated");

        var orSpec = new OrSpecification<TestEntity>(left, right);

        await Assert.That(orSpec.Includes.Count).IsEqualTo(2);
        await Assert.That(orSpec.IncludeStrings.Count).IsEqualTo(2);
        await Assert.That(orSpec.IncludeStrings).Contains("Related");
        await Assert.That(orSpec.IncludeStrings).Contains("OtherRelated");
    }

    [Test]
    public async Task NotSpecification_WithNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            Task.FromResult(new NotSpecification<TestEntity>(null!)));
    }

    [Test]
    public async Task NotSpecification_NegatesCriteria()
    {
        var spec = new SimpleSpecification(x => x.Id == 1);

        var notSpec = new NotSpecification<TestEntity>(spec);

        await Assert.That(notSpec.Criteria).IsNotNull();
    }

    [Test]
    public async Task NotSpecification_IsSatisfiedBy_OriginalTrue_ReturnsFalse()
    {
        var spec = new SimpleSpecification(x => x.Id == 1);
        var notSpec = new NotSpecification<TestEntity>(spec);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = notSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task NotSpecification_IsSatisfiedBy_OriginalFalse_ReturnsTrue()
    {
        var spec = new SimpleSpecification(x => x.Id == 2);
        var notSpec = new NotSpecification<TestEntity>(spec);
        var entity = new TestEntity { Id = 1, Name = "Test" };

        var result = notSpec.IsSatisfiedBy(entity);

        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task NotSpecification_CopiesIncludes()
    {
        var spec = new SimpleSpecification(x => x.Id == 1);
        spec.Includes.Add(x => x.Name);
        spec.IncludeStrings.Add("Related");

        var notSpec = new NotSpecification<TestEntity>(spec);

        await Assert.That(notSpec.Includes.Count).IsEqualTo(1);
        await Assert.That(notSpec.IncludeStrings.Count).IsEqualTo(1);
        await Assert.That(notSpec.IncludeStrings[0]).IsEqualTo("Related");
    }

    [Test]
    public async Task ComplexComposition_WorksCorrectly()
    {
        // (Id > 0 AND Name == "Test") OR (Value >= 100 AND NOT IsActive)
        var spec1 = new SimpleSpecification(x => x.Id > 0);
        var spec2 = new SimpleSpecification(x => x.Name == "Test");
        var spec3 = new SimpleSpecification(x => x.Value >= 100);
        var spec4 = new SimpleSpecification(x => x.IsActive);

        var andSpec1 = spec1.And(spec2);
        var andSpec2 = spec3.And(spec4.Not());
        var finalSpec = andSpec1.Or(andSpec2);

        // Test entity that matches first condition
        var entity1 = new TestEntity { Id = 1, Name = "Test", Value = 50, IsActive = true };
        await Assert.That(finalSpec.IsSatisfiedBy(entity1)).IsTrue();

        // Test entity that matches second condition
        var entity2 = new TestEntity { Id = -1, Name = "Other", Value = 100, IsActive = false };
        await Assert.That(finalSpec.IsSatisfiedBy(entity2)).IsTrue();

        // Test entity that matches neither condition
        var entity3 = new TestEntity { Id = -1, Name = "Other", Value = 50, IsActive = true };
        await Assert.That(finalSpec.IsSatisfiedBy(entity3)).IsFalse();
    }
}