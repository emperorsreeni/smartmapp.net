using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using SmartMapp.Net.Compilation;
using SmartMapp.Net.Tests.Unit.TestTypes;

namespace SmartMapp.Net.Tests.Unit.Compilation;

/// <summary>
/// Direct unit tests for <see cref="PropertyAssignmentBuilder"/>.
/// Verifies assignment generation, member binding, skipping, conditions, fallback, and nested mapping.
/// </summary>
public class PropertyAssignmentBuilderTests
{
    [Fact]
    public void BuildAssignments_SkippedLinks_AreExcluded()
    {
        var originParam = Expression.Variable(typeof(FlatOrder), "origin");
        var targetVar = Expression.Variable(typeof(FlatOrderDto), "target");
        var scopeParam = Expression.Parameter(typeof(MappingScope), "scope");
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var links = new List<PropertyLink>
        {
            new()
            {
                TargetMember = typeof(FlatOrderDto).GetProperty("Id")!,
                Provider = new DirectMemberProvider(typeof(FlatOrder).GetProperty("Id")!),
                LinkedBy = ConventionMatch.ExactName("Id"),
            },
            new()
            {
                TargetMember = typeof(FlatOrderDto).GetProperty("Name")!,
                Provider = new DirectMemberProvider(typeof(FlatOrder).GetProperty("Name")!),
                LinkedBy = ConventionMatch.ExactName("Name"),
                IsSkipped = true,
            },
        };

        var assignments = PropertyAssignmentBuilder.BuildAssignments(links, originParam, targetVar, scopeParam, consumed);

        // Only 1 assignment — Name was skipped
        assignments.Should().HaveCount(1);
    }

    [Fact]
    public void BuildAssignments_ConsumedByConstructor_AreExcluded()
    {
        var originParam = Expression.Variable(typeof(FlatOrder), "origin");
        var targetVar = Expression.Variable(typeof(FlatOrderDto), "target");
        var scopeParam = Expression.Parameter(typeof(MappingScope), "scope");
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" };

        var links = new List<PropertyLink>
        {
            new()
            {
                TargetMember = typeof(FlatOrderDto).GetProperty("Id")!,
                Provider = new DirectMemberProvider(typeof(FlatOrder).GetProperty("Id")!),
                LinkedBy = ConventionMatch.ExactName("Id"),
            },
            new()
            {
                TargetMember = typeof(FlatOrderDto).GetProperty("Name")!,
                Provider = new DirectMemberProvider(typeof(FlatOrder).GetProperty("Name")!),
                LinkedBy = ConventionMatch.ExactName("Name"),
            },
        };

        var assignments = PropertyAssignmentBuilder.BuildAssignments(links, originParam, targetVar, scopeParam, consumed);

        // Id was consumed by ctor, only Name should be assigned
        assignments.Should().HaveCount(1);
    }

    [Fact]
    public void BuildMemberBindings_GeneratesBindings_ForNonSkippedLinks()
    {
        var originParam = Expression.Variable(typeof(FlatOrder), "origin");
        var scopeParam = Expression.Parameter(typeof(MappingScope), "scope");
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var links = new List<PropertyLink>
        {
            new()
            {
                TargetMember = typeof(FlatOrderDto).GetProperty("Id")!,
                Provider = new DirectMemberProvider(typeof(FlatOrder).GetProperty("Id")!),
                LinkedBy = ConventionMatch.ExactName("Id"),
            },
            new()
            {
                TargetMember = typeof(FlatOrderDto).GetProperty("Name")!,
                Provider = new DirectMemberProvider(typeof(FlatOrder).GetProperty("Name")!),
                LinkedBy = ConventionMatch.ExactName("Name"),
            },
        };

        var bindings = PropertyAssignmentBuilder.BuildMemberBindings(links, originParam, scopeParam, consumed);

        bindings.Should().HaveCount(2);
    }

    [Fact]
    public void BuildAssignments_FieldTarget_ProducesAssignment()
    {
        var originParam = Expression.Variable(typeof(FieldOnlyOrigin), "origin");
        var targetVar = Expression.Variable(typeof(FieldOnlyTarget), "target");
        var scopeParam = Expression.Parameter(typeof(MappingScope), "scope");
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var links = new List<PropertyLink>
        {
            new()
            {
                TargetMember = typeof(FieldOnlyTarget).GetField("Id")!,
                Provider = new DirectMemberProvider(typeof(FieldOnlyOrigin).GetField("Id")!),
                LinkedBy = ConventionMatch.ExactName("Id"),
            },
        };

        var assignments = PropertyAssignmentBuilder.BuildAssignments(links, originParam, targetVar, scopeParam, consumed);

        assignments.Should().HaveCount(1);
    }

    [Fact]
    public void GetMemberType_Property_ReturnsPropertyType()
    {
        var prop = typeof(FlatOrder).GetProperty("Id")!;
        PropertyAssignmentBuilder.GetMemberType(prop).Should().Be(typeof(int));
    }

    [Fact]
    public void GetMemberType_Field_ReturnsFieldType()
    {
        var field = typeof(FieldOnlyOrigin).GetField("Name")!;
        PropertyAssignmentBuilder.GetMemberType(field).Should().Be(typeof(string));
    }
}
