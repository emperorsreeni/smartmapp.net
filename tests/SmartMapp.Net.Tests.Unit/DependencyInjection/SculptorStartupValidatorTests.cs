using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SmartMapp.Net;
using SmartMapp.Net.Configuration;
using SmartMapp.Net.DependencyInjection;
using SmartMapp.Net.DependencyInjection.Exceptions;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.Tests.Unit.DependencyInjection;

/// <summary>
/// Sprint 8 · S8-T05 — unit tests for <see cref="SculptorStartupValidator"/>. Exercises the
/// valid / invalid / warnings-only / StrictMode paths against a fake
/// <see cref="ISculptorConfiguration"/> returning canned <see cref="ValidationResult"/>s.
/// </summary>
public class SculptorStartupValidatorTests
{
    private static BlueprintValidationResult MakeResult(params (ValidationSeverity severity, string message)[] findings)
    {
        var result = new BlueprintValidationResult();
        foreach (var (sev, msg) in findings)
        {
            if (sev == ValidationSeverity.Error)
                result.AddError(typeof(int), typeof(string), msg);
            else
                result.AddWarning(typeof(int), typeof(string), msg);
        }
        return result;
    }

    private static ISculptorConfiguration FakeConfigReturning(BlueprintValidationResult inner)
    {
        var config = Substitute.For<ISculptorConfiguration>();
        config.ValidateConfiguration().Returns(new ValidationResult(inner));
        config.GetAllBlueprints().Returns(Array.Empty<Blueprint>());
        return config;
    }

    [Fact]
    public async Task StartAsync_ValidConfiguration_CompletesWithoutThrow_AndLogsInformation()
    {
        var config = FakeConfigReturning(MakeResult());
        var options = new SculptorOptions { ValidateOnStartup = true };
        var logger = Substitute.For<ILogger<SculptorStartupValidator>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var validator = new SculptorStartupValidator(config, options, logger);

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_InvalidConfiguration_Throws_SculptorStartupValidationException_AndLogsErrorBeforeThrow()
    {
        // Spec §S8-T05 Unit-Tests bullet 3: "ILogger assertions: Information on success,
        // Warning for warnings, Error before throw." Exercise the full error-log path here.
        var inner = MakeResult(
            (ValidationSeverity.Error, "pair 1 error"),
            (ValidationSeverity.Error, "pair 2 error"));
        var config = FakeConfigReturning(inner);
        var options = new SculptorOptions { ValidateOnStartup = true };
        var logger = Substitute.For<ILogger<SculptorStartupValidator>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var validator = new SculptorStartupValidator(config, options, logger);

        var act = () => validator.StartAsync(Xunit.TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<SculptorStartupValidationException>();
        ex.Which.Findings.Should().HaveCount(2);
        ex.Which.Message.Should().Contain("pair 1 error");
        ex.Which.Message.Should().Contain("pair 2 error");
        ex.Which.ValidationResult.Errors.Should().HaveCount(2);
        ex.Which.InnerException.Should().BeOfType<AggregateException>()
            .Which.InnerExceptions.Should().HaveCount(2);

        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_IsIdempotent_ReRunningValidationReturnsCachedResult()
    {
        // Spec §S8-T05 Tech-Cons. bullet 3: "Document that validation is idempotent; re-running
        // Validate() mid-app returns cached results from Sprint 7's cache." Guard by invoking
        // StartAsync twice (as if the host restarted) and asserting ValidateConfiguration is
        // called on every StartAsync but the underlying implementation (ForgedSculptorConfiguration
        // in Sprint 7) returns a cached ValidationResult — the fake mirrors that contract by
        // returning the same instance every time.
        var sharedResult = new ValidationResult(MakeResult());
        var callCount = 0;
        var returnedInstances = new List<ValidationResult>();
        var config = Substitute.For<ISculptorConfiguration>();
        config.ValidateConfiguration().Returns(_ =>
        {
            Interlocked.Increment(ref callCount);
            returnedInstances.Add(sharedResult);
            return sharedResult;
        });
        config.GetAllBlueprints().Returns(Array.Empty<Blueprint>());

        var options = new SculptorOptions { ValidateOnStartup = true };
        var validator = new SculptorStartupValidator(config, options, NullLogger<SculptorStartupValidator>.Instance);
        var ct = Xunit.TestContext.Current.CancellationToken;

        await validator.StartAsync(ct);
        await validator.StartAsync(ct);

        callCount.Should().Be(2, "each StartAsync invokes ValidateConfiguration once.");
        returnedInstances.Should().HaveCount(2);
        returnedInstances.Should().AllSatisfy(r => r.Should().BeSameAs(sharedResult),
            "the cached ValidationResult instance is returned on every re-validate (idempotent) — mirrors Sprint 7's ForgedSculptorConfiguration.CachedValidation.");
    }

    [Fact]
    public async Task StartAsync_WarningsOnly_DefaultMode_DoesNotThrow_ButLogsWarnings()
    {
        var inner = MakeResult((ValidationSeverity.Warning, "soft finding"));
        var config = FakeConfigReturning(inner);
        var options = new SculptorOptions { ValidateOnStartup = true };
        var logger = Substitute.For<ILogger<SculptorStartupValidator>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var validator = new SculptorStartupValidator(config, options, logger);

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_WarningsOnly_StrictMode_Throws_WithWarningsPromoted()
    {
        var inner = MakeResult((ValidationSeverity.Warning, "strict warn"));
        var config = FakeConfigReturning(inner);
        var options = new SculptorOptions { ValidateOnStartup = true, StrictMode = true };
        var validator = new SculptorStartupValidator(config, options, NullLogger<SculptorStartupValidator>.Instance);

        var act = () => validator.StartAsync(Xunit.TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<SculptorStartupValidationException>();
        ex.Which.Findings.Should().ContainSingle(f => f.Severity == ValidationSeverity.Warning && f.Message == "strict warn");
    }

    [Fact]
    public async Task StartAsync_ValidateOnStartupFalse_ShortCircuits_NoValidateCalled()
    {
        var config = Substitute.For<ISculptorConfiguration>();
        var options = new SculptorOptions { ValidateOnStartup = false };

        var validator = new SculptorStartupValidator(config, options, NullLogger<SculptorStartupValidator>.Instance);

        await validator.StartAsync(Xunit.TestContext.Current.CancellationToken);

        config.DidNotReceive().ValidateConfiguration();
        config.DidNotReceive().GetAllBlueprints();
    }

    [Fact]
    public async Task StartAsync_ValidatorThrowsUnexpectedly_PropagatesAndLogsError()
    {
        var config = Substitute.For<ISculptorConfiguration>();
        var boom = new InvalidOperationException("forge crash");
        config.ValidateConfiguration().Returns<ValidationResult>(_ => throw boom);
        var options = new SculptorOptions { ValidateOnStartup = true };
        var logger = Substitute.For<ILogger<SculptorStartupValidator>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        var validator = new SculptorStartupValidator(config, options, logger);

        var act = () => validator.StartAsync(Xunit.TestContext.Current.CancellationToken);

        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Should().BeSameAs(boom);
        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            boom,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StopAsync_IsNoOp_ReturnsCompletedTask()
    {
        var validator = new SculptorStartupValidator(
            Substitute.For<ISculptorConfiguration>(),
            new SculptorOptions(),
            NullLogger<SculptorStartupValidator>.Instance);

        await validator.StopAsync(Xunit.TestContext.Current.CancellationToken);
        // Test passes if StopAsync returns without side effects or exceptions.
    }

    [Fact]
    public void Ctor_NullArguments_Throw()
    {
        var config = Substitute.For<ISculptorConfiguration>();
        var options = new SculptorOptions();
        var logger = NullLogger<SculptorStartupValidator>.Instance;

        ((Action)(() => new SculptorStartupValidator(null!, options, logger)))
            .Should().Throw<ArgumentNullException>().WithParameterName("configuration");
        ((Action)(() => new SculptorStartupValidator(config, null!, logger)))
            .Should().Throw<ArgumentNullException>().WithParameterName("options");
        ((Action)(() => new SculptorStartupValidator(config, options, null!)))
            .Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }
}
