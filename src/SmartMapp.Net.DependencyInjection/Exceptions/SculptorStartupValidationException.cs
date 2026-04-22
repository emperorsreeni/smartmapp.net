// SPDX-License-Identifier: MIT
// <copyright file="SculptorStartupValidationException.cs" company="SmartMapp.Net">
// Copyright (c) SmartMapp.Net contributors. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using System.Text;
using SmartMapp.Net.Diagnostics;

namespace SmartMapp.Net.DependencyInjection.Exceptions;

/// <summary>
/// Thrown from <c>IHostedService.StartAsync</c> by <see cref="SculptorStartupValidator"/> when
/// <see cref="SmartMapp.Net.Configuration.SculptorOptions.ValidateOnStartup"/> is enabled and
/// the forged <see cref="ISculptorConfiguration"/> contains one or more validation errors (or
/// warnings when <see cref="SmartMapp.Net.Configuration.SculptorOptions.StrictMode"/> is on).
/// </summary>
/// <remarks>
/// <para>
/// Introduced in Sprint 8 · S8-T05 per spec §12.1. The exception aggregates every finding
/// returned by <see cref="ISculptorConfiguration.ValidateConfiguration"/>, exposes them via
/// <see cref="Findings"/>, and produces a line-per-finding <see cref="Exception.Message"/> so
/// the default <c>IHostedService</c> failure output is immediately actionable in logs.
/// </para>
/// <para>
/// When the supplied <see cref="ValidationResult"/> contains more than one error, each finding
/// is additionally wrapped in a dedicated <see cref="InvalidOperationException"/> and stashed
/// inside <see cref="Exception.InnerException"/> as an <see cref="AggregateException"/> — this
/// lets exception viewers that render inner-exception chains (e.g. <c>dotnet-dump analyze</c>,
/// ASP.NET Core developer page) display every finding without re-parsing the aggregate message.
/// </para>
/// </remarks>
public sealed class SculptorStartupValidationException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="SculptorStartupValidationException"/> from the supplied
    /// <paramref name="result"/>.
    /// </summary>
    /// <param name="result">The validation result produced by <see cref="ISculptorConfiguration.ValidateConfiguration"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is <c>null</c>.</exception>
    public SculptorStartupValidationException(ValidationResult result)
        : base(BuildMessage(result ?? throw new ArgumentNullException(nameof(result))),
               BuildAggregate(result))
    {
        ValidationResult = result;
        Findings = result.Errors.Count > 0 ? result.Errors : result.Warnings;
    }

    /// <summary>
    /// Gets the full structured validation result — exposes both errors and warnings for
    /// callers that want to enumerate findings programmatically (e.g. custom error pages).
    /// </summary>
    public ValidationResult ValidationResult { get; }

    /// <summary>
    /// Gets the findings that triggered the exception. For the default
    /// <see cref="SmartMapp.Net.Configuration.SculptorOptions.StrictMode"/>=<c>false</c> path
    /// this contains the errors; under <see cref="SmartMapp.Net.Configuration.SculptorOptions.StrictMode"/>
    /// =<c>true</c> with zero errors it contains the warnings instead.
    /// </summary>
    public IReadOnlyList<BlueprintValidationError> Findings { get; }

    private static string BuildMessage(ValidationResult result)
    {
        var sb = new StringBuilder();
        sb.Append("SmartMapp.Net forged configuration failed validation at startup. ")
          .Append(result.Errors.Count).Append(" error(s), ")
          .Append(result.Warnings.Count).Append(" warning(s).");

        void AppendSection(string header, IReadOnlyList<BlueprintValidationError> items)
        {
            if (items.Count == 0) return;
            sb.AppendLine();
            sb.Append(header);
            foreach (var finding in items)
            {
                sb.AppendLine();
                sb.Append("  - ")
                  .Append(finding.OriginType.Name).Append(" -> ").Append(finding.TargetType.Name)
                  .Append(": ").Append(finding.Message);
            }
        }

        AppendSection("Errors:", result.Errors);
        AppendSection("Warnings:", result.Warnings);
        return sb.ToString();
    }

    private static AggregateException? BuildAggregate(ValidationResult result)
    {
        if (result.Errors.Count == 0 && result.Warnings.Count == 0)
            return null;

        var findings = result.Errors.Count > 0 ? result.Errors : result.Warnings;
        if (findings.Count == 0) return null;

        var inners = new List<Exception>(findings.Count);
        foreach (var f in findings)
        {
            inners.Add(new InvalidOperationException(
                $"[{f.Severity}] {f.OriginType.FullName} -> {f.TargetType.FullName}: {f.Message}"));
        }
        return new AggregateException(new ReadOnlyCollection<Exception>(inners));
    }
}
