namespace SmartMapp.Net.Transformers;

/// <summary>
/// Provides default registration of all built-in transformers into a <see cref="TypeTransformerRegistry"/>.
/// Called during <c>SculptorBuilder.Forge()</c> to populate the registry with all §7.1 conversions.
/// </summary>
public static class TypeTransformerRegistryDefaults
{
    /// <summary>
    /// Registers all built-in transformers into the specified registry.
    /// Exact-match transformers are registered first (dictionary lookup), followed by
    /// open transformers in priority order (scanned via <c>CanTransform</c>).
    /// <para>
    /// This method is idempotent — calling it multiple times on the same registry
    /// will overwrite registrations but not duplicate open transformers.
    /// </para>
    /// </summary>
    /// <param name="registry">The registry to populate.</param>
    /// <param name="enumOptions">Optional enum transformer options. Uses defaults if <c>null</c>.</param>
    public static void RegisterDefaults(
        TypeTransformerRegistry registry,
        EnumTransformerOptions? enumOptions = null)
    {
        if (registry is null) throw new ArgumentNullException(nameof(registry));

        var eo = enumOptions ?? new EnumTransformerOptions();

        // ── Exact-match transformers (dictionary lookup — O(1)) ──────────────

        // Guid ↔ string
        registry.Register<Guid, string>(new GuidToStringTransformer());
        registry.Register<string, Guid>(new StringToGuidTransformer());

        // Uri ↔ string
        registry.Register<string, Uri>(new StringToUriTransformer());
        registry.Register<Uri, string>(new UriToStringTransformer());

        // bool ↔ int
        registry.Register<bool, int>(new BoolToIntTransformer());
        registry.Register<int, bool>(new IntToBoolTransformer());

        // byte[] ↔ string (Base64)
        registry.Register<byte[], string>(new ByteArrayToBase64Transformer());
        registry.Register<string, byte[]>(new Base64ToByteArrayTransformer());

        // DateTime family
        registry.Register<DateTime, DateTimeOffset>(new DateTimeToDateTimeOffsetTransformer());
        registry.Register<DateTimeOffset, DateTime>(new DateTimeOffsetToDateTimeTransformer());
        registry.Register<string, DateTime>(new StringToDateTimeTransformer());

#if NET6_0_OR_GREATER
        registry.Register<DateTime, DateOnly>(new DateTimeToDateOnlyTransformer());
        registry.Register<DateTime, TimeOnly>(new DateTimeToTimeOnlyTransformer());
        registry.Register<DateOnly, DateTime>(new DateOnlyToDateTimeTransformer());
        registry.Register<TimeOnly, TimeSpan>(new TimeOnlyToTimeSpanTransformer());
#endif

        // ── Open transformers (CanTransform scan — order matters) ────────────
        // Clear existing open transformers to ensure idempotency on re-call.
        registry.ClearOpen();
        // More specific first, more generic last.

        // Nullable wrap/unwrap (very specific pattern)
        registry.RegisterOpen(new NullableWrapTransformer());
        registry.RegisterOpen(new NullableUnwrapTransformer());

        // Enum transformers
        registry.RegisterOpen(new EnumToStringTransformer(eo));
        registry.RegisterOpen(new StringToEnumTransformer(eo));
        registry.RegisterOpen(new EnumToEnumTransformer(eo));

        // Implicit/explicit operator detection
        registry.RegisterOpen(new ImplicitExplicitOperatorTransformer());

        // JsonElement ↔ T
        registry.RegisterOpen(new JsonElementToObjectTransformer());
        registry.RegisterOpen(new ObjectToJsonElementTransformer());

        // Generic parsable (string → any IParsable<T> or TypeConverter-compatible)
        registry.RegisterOpen(new ParsableTransformer());

        // ToString fallback (lowest priority — always matches target == string)
        registry.RegisterOpen(new ToStringTransformer());
    }
}
