using SmartMapp.Net.Samples.Console.Models;

namespace SmartMapp.Net.Samples.Console.Output;

/// <summary>
/// Shared presentation helpers for the sample scenarios. Kept in a dedicated file per
/// spec §S8-T09 Technical Considerations bullet 2 — "Use top-level statements for minimal
/// ceremony but keep helpers in separate files".
/// </summary>
internal static class ConsoleOutput
{
    /// <summary>
    /// Prints a clearly delimited section header and runs the body. Any exception thrown by
    /// <paramref name="body"/> bubbles out to the top-level <c>try/catch</c> in
    /// <c>Program.cs</c> so the sample exits with code 1 as required by
    /// spec §S8-T09 Acceptance bullet 1.
    /// </summary>
    internal static void Section(string title, Action body)
    {
        System.Console.WriteLine();
        System.Console.WriteLine(new string('=', 72));
        System.Console.WriteLine(title);
        System.Console.WriteLine(new string('=', 72));
        body();
    }

    /// <summary>
    /// Prints a trivially comparable before/after rendering for flat records — the
    /// zero-config scenario's primary evidence that the convention engine linked every
    /// property by name.
    /// </summary>
    internal static void PrintBeforeAfter(Customer source, CustomerDto target)
    {
        System.Console.WriteLine($"  Source : Customer  Id={source.Id}, Name={source.Name}, Email={source.Email}");
        System.Console.WriteLine($"  Target : DTO       Id={target.Id}, Name={target.Name}, Email={target.Email}");
    }
}
