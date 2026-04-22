using FluentAssertions;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T05 — tests for the message / findings / aggregate-inner-exception shape of
/// <see cref="SculptorStartupValidationException"/>.
/// </summary>
public class SculptorStartupValidationExceptionTests
{
    private static BlueprintValidationResult BuildErrors(params string[] errorMessages)
    {
        var r = new BlueprintValidationResult();
        foreach (var m in errorMessages)
            r.AddError(typeof(int), typeof(string), m);
        return r;
    }

    [Fact]
    public void Ctor_NullResult_Throws()
    {
        var act = () => new SculptorStartupValidationException(null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("result");
    }

    [Fact]
    public void Message_IncludesEachErrorOriginTargetAndText()
    {
        var inner = BuildErrors("first error", "second error");
        var ex = new SculptorStartupValidationException(new ValidationResult(inner));

        ex.Message.Should().Contain("2 error(s)");
        ex.Message.Should().Contain("first error");
        ex.Message.Should().Contain("second error");
        ex.Message.Should().Contain(nameof(Int32));
        ex.Message.Should().Contain(nameof(String));
    }

    [Fact]
    public void InnerException_IsAggregate_WithOneEntryPerError()
    {
        var inner = BuildErrors("err A", "err B", "err C");
        var ex = new SculptorStartupValidationException(new ValidationResult(inner));

        ex.InnerException.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().HaveCount(3)
            .And.AllBeOfType<InvalidOperationException>();

        var aggregateMessages = ((AggregateException)ex.InnerException!).InnerExceptions
            .Select(e => e.Message)
            .ToArray();
        aggregateMessages.Should().Contain(m => m.Contains("err A"));
        aggregateMessages.Should().Contain(m => m.Contains("err B"));
        aggregateMessages.Should().Contain(m => m.Contains("err C"));
    }

    [Fact]
    public void Findings_ContainsErrors_WhenErrorsPresent()
    {
        var inner = BuildErrors("only error");
        var ex = new SculptorStartupValidationException(new ValidationResult(inner));

        ex.Findings.Should().ContainSingle()
            .Which.Message.Should().Be("only error");
        ex.Findings[0].Severity.Should().Be(ValidationSeverity.Error);
    }

    [Fact]
    public void Findings_ContainsWarnings_WhenNoErrorsButWarningsPresent()
    {
        var inner = new BlueprintValidationResult();
        inner.AddWarning(typeof(int), typeof(string), "soft issue");
        var ex = new SculptorStartupValidationException(new ValidationResult(inner));

        ex.Findings.Should().ContainSingle()
            .Which.Severity.Should().Be(ValidationSeverity.Warning);
        ex.Findings[0].Message.Should().Be("soft issue");
    }

    [Fact]
    public void ValidationResult_IsExposedForProgrammaticInspection()
    {
        var inner = BuildErrors("one");
        inner.AddWarning(typeof(bool), typeof(decimal), "warn");
        var result = new ValidationResult(inner);

        var ex = new SculptorStartupValidationException(result);

        ex.ValidationResult.Should().BeSameAs(result);
        ex.ValidationResult.Errors.Should().HaveCount(1);
        ex.ValidationResult.Warnings.Should().HaveCount(1);
    }
}
