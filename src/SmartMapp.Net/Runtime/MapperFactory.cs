namespace SmartMapp.Net.Runtime;

/// <summary>
/// Internal factory shared by <see cref="Mapper{TOrigin,TTarget}"/> and the upcoming
/// Sprint 8 DI package. Centralises construction of <see cref="Mapper{TOrigin,TTarget}"/>
/// so all creation paths share identical configuration wiring.
/// </summary>
internal static class MapperFactory
{
    /// <summary>
    /// Creates a strongly-typed mapper over the forged configuration.
    /// Throws <see cref="MappingConfigurationException"/> when no blueprint exists for the pair.
    /// </summary>
    internal static Mapper<TOrigin, TTarget> Create<TOrigin, TTarget>(ForgedSculptorConfiguration config)
    {
        return new Mapper<TOrigin, TTarget>(config);
    }
}
