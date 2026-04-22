using System.Runtime.CompilerServices;

namespace SmartMapp.Net.Runtime;

/// <summary>
/// Resolves delegates from the forged configuration and invokes them with a correctly sized
/// <see cref="MappingScope"/>. Shared by <see cref="Sculptor"/> and <see cref="Mapper{TOrigin,TTarget}"/>.
/// </summary>
internal static class MappingExecutor
{
    /// <summary>
    /// Retrieves the compiled delegate for <paramref name="pair"/> or compiles it on demand.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Func<object, MappingScope, object> GetOrCompile(
        ForgedSculptorConfiguration config, TypePair pair)
    {
        return config.DelegateCache.GetOrCompile(pair, _ =>
        {
            var bp = config.TryGetBlueprint(pair)
                ?? throw BuildUnknownPairException(config, pair);
            return config.Compiler.Compile(bp);
        });
    }

    /// <summary>
    /// Builds a <see cref="MappingConfigurationException"/> for an unknown type pair, including
    /// nearest fuzzy-match suggestions from the registered blueprints so users can diagnose
    /// typos (T07 AC).
    /// </summary>
    internal static MappingConfigurationException BuildUnknownPairException(
        ForgedSculptorConfiguration config, TypePair pair)
    {
        var message = new System.Text.StringBuilder();
        message.Append($"No blueprint registered for type pair '{pair}'. ")
               .Append("Register it via SculptorBuilder.Bind<TOrigin, TTarget>() or add a MappingBlueprint subclass.");

        var suggestions = FindNearestPairs(config, pair, maxSuggestions: 3);
        if (suggestions.Count > 0)
        {
            message.Append(" Did you mean: ")
                   .Append(string.Join(", ", suggestions))
                   .Append('?');
        }

        return new MappingConfigurationException(message.ToString(), pair);
    }

    private static List<string> FindNearestPairs(
        ForgedSculptorConfiguration config,
        TypePair pair,
        int maxSuggestions)
    {
        var originName = pair.OriginType.Name;
        var targetName = pair.TargetType.Name;
        var scored = new List<(TypePair Pair, int Score)>();

        foreach (var bp in config.Blueprints)
        {
            var score = Similarity(originName, bp.OriginType.Name)
                      + Similarity(targetName, bp.TargetType.Name);
            if (score > 0) scored.Add((bp.TypePair, score));
        }

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));

        var results = new List<string>(Math.Min(maxSuggestions, scored.Count));
        for (var i = 0; i < scored.Count && results.Count < maxSuggestions; i++)
        {
            results.Add($"'{scored[i].Pair}'");
        }
        return results;
    }

    // Very cheap similarity heuristic: count of shared case-insensitive characters. Adequate for
    // identifying typos / near-misses in the error message without pulling in a Levenshtein dep.
    private static int Similarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
        var lowerB = b.ToLowerInvariant();
        var score = 0;
        foreach (var c in a.ToLowerInvariant())
        {
            if (lowerB.Contains(c)) score++;
        }
        return score;
    }

    /// <summary>
    /// Creates a fresh <see cref="MappingScope"/> initialised from the forged options.
    /// When <paramref name="serviceProvider"/> is <c>null</c>, falls back to the current
    /// <see cref="AmbientServiceProvider.Current"/> value — populated by the DI package
    /// immediately before invoking <c>Map</c> — so sculptors resolved from DI automatically
    /// flow their request scope into value providers and type transformers without the
    /// caller needing to thread <see cref="IServiceProvider"/> manually (spec §11.4, S8-T04).
    /// The scope's <see cref="MappingScope.ProviderResolver"/> is populated from
    /// <see cref="Configuration.SculptorOptions.ProviderResolver"/> so downstream
    /// <see cref="Abstractions.IValueProvider"/>-deferred activations honour the user's
    /// configured resolver strategy.
    /// </summary>
    internal static MappingScope CreateScope(ForgedSculptorConfiguration config, IServiceProvider? serviceProvider = null)
    {
        return new MappingScope
        {
            MaxDepth = config.Options.MaxRecursionDepth,
            ServiceProvider = serviceProvider ?? AmbientServiceProvider.Current,
            ProviderResolver = config.Options.ProviderResolver,
        };
    }
}
