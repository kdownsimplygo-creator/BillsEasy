using System;
using BillsEasy.Domain.Common;
using FluentAssertions;
using Xunit;

namespace BillsEasy.UnitTests.Domain.Common;

public class TestEntity : AuditableEntity<Guid>
{
    public string Name { get; set; } = string.Empty;

    public TestEntity(Guid id, string name) : base(id)
    {
        Name = name;
    }
}

public class AuditableEntityTests
{
    [Fact]
    public void Entities_WithSameIdAndType_AreEqual()
    {
        var id = Guid.NewGuid();
        var e1 = new TestEntity(id, "Alpha");
        var e2 = new TestEntity(id, "Beta");

        (e1 == e2).Should().BeTrue();
        e1.Equals(e2).Should().BeTrue();
        e1.GetHashCode().Should().Be(e2.GetHashCode());
    }

    [Fact]
    public void Entities_WithDifferentIds_AreNotEqual()
    {
        var e1 = new TestEntity(Guid.NewGuid(), "Alpha");
        var e2 = new TestEntity(Guid.NewGuid(), "Alpha");

        (e1 == e2).Should().BeFalse();
        e1.Equals(e2).Should().BeFalse();
    }

    [Fact]
    public void AuditableProperties_CanBeSetAndRetrieved()
    {
        var now = DateTime.UtcNow;
        var entity = new TestEntity(Guid.NewGuid(), "Test")
        {
            CreatedAt = now,
            CreatedBy = "admin",
            ModifiedAt = now.AddMinutes(5),
            ModifiedBy = "manager"
        };

        entity.CreatedAt.Should().Be(now);
        entity.CreatedBy.Should().Be("admin");
        entity.ModifiedAt.Should().Be(now.AddMinutes(5));
        entity.ModifiedBy.Should().Be("manager");
    }
}
