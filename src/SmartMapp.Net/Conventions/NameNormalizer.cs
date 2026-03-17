namespace SmartMapp.Net.Conventions;

/// <summary>
/// Static utility that segments member names from any casing convention (PascalCase, camelCase,
/// snake_case, SCREAMING_SNAKE, kebab-case) into canonical word segments for cross-case comparison.
/// </summary>
public static class NameNormalizer
{
    /// <summary>
    /// Splits a name into word segments based on casing boundaries, underscores, and hyphens.
    /// </summary>
    /// <param name="name">The name to segment.</param>
    /// <returns>An array of word segments.</returns>
    /// <example>
    /// <c>"FirstName"</c> → <c>["First", "Name"]</c><br/>
    /// <c>"firstName"</c> → <c>["first", "Name"]</c><br/>
    /// <c>"first_name"</c> → <c>["first", "name"]</c><br/>
    /// <c>"FIRST_NAME"</c> → <c>["FIRST", "NAME"]</c><br/>
    /// <c>"XMLParser"</c> → <c>["XML", "Parser"]</c><br/>
    /// </example>
    public static string[] Segment(string name)
    {
        if (string.IsNullOrEmpty(name))
            return Array.Empty<string>();

        var segments = new List<string>();
        var start = 0;

        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];

            // Separator characters: split and skip the separator
            if (ch == '_' || ch == '-')
            {
                if (i > start)
                    segments.Add(name.Substring(start, i - start));
                start = i + 1;
                continue;
            }

            if (i == 0) continue;

            var prev = name[i - 1];

            // Transition: lowercase/digit → uppercase (e.g., "firstName" at 'N')
            if (char.IsUpper(ch) && !char.IsUpper(prev) && prev != '_' && prev != '-')
            {
                if (i > start)
                    segments.Add(name.Substring(start, i - start));
                start = i;
                continue;
            }

            // Transition: uppercase run ending (e.g., "XMLParser" — 'L' to 'P' means split before 'P')
            // Detect: current is uppercase, next is lowercase, and previous was also uppercase
            if (char.IsUpper(ch) && i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(prev))
            {
                if (i > start)
                    segments.Add(name.Substring(start, i - start));
                start = i;
                continue;
            }
        }

        // Add the remaining segment
        if (start < name.Length)
            segments.Add(name.Substring(start));

        return segments.ToArray();
    }

    /// <summary>
    /// Joins word segments into PascalCase.
    /// </summary>
    /// <param name="segments">The segments to join.</param>
    /// <returns>A PascalCase string.</returns>
    public static string ToPascalCase(string[] segments)
    {
        if (segments.Length == 0) return string.Empty;

#if NET8_0_OR_GREATER
        return string.Create(segments.Sum(s => s.Length), segments, static (span, segs) =>
        {
            var pos = 0;
            foreach (var seg in segs)
            {
                if (seg.Length == 0) continue;
                span[pos] = char.ToUpperInvariant(seg[0]);
                pos++;
                for (var i = 1; i < seg.Length; i++)
                {
                    span[pos] = char.ToLowerInvariant(seg[i]);
                    pos++;
                }
            }
        });
#else
        var chars = new char[segments.Sum(s => s.Length)];
        var pos = 0;
        foreach (var seg in segments)
        {
            if (seg.Length == 0) continue;
            chars[pos++] = char.ToUpperInvariant(seg[0]);
            for (var i = 1; i < seg.Length; i++)
                chars[pos++] = char.ToLowerInvariant(seg[i]);
        }
        return new string(chars, 0, pos);
#endif
    }

    /// <summary>
    /// Determines whether two names are equivalent by segmenting both and comparing
    /// segments case-insensitively.
    /// </summary>
    /// <param name="name1">The first name.</param>
    /// <param name="name2">The second name.</param>
    /// <returns><c>true</c> if the names have identical segments (case-insensitive); otherwise <c>false</c>.</returns>
    public static bool AreEquivalent(string name1, string name2)
    {
        if (string.Equals(name1, name2, StringComparison.OrdinalIgnoreCase))
            return true;

        var s1 = Segment(name1);
        var s2 = Segment(name2);

        if (s1.Length != s2.Length) return false;

        for (var i = 0; i < s1.Length; i++)
        {
            if (!string.Equals(s1[i], s2[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
