using System.Text.Json;
using FluentAssertions;
using SmartMapp.Net.Abstractions;
using SmartMapp.Net.Tests.Unit.TestTypes;
using SmartMapp.Net.Transformers;

namespace SmartMapp.Net.Tests.Unit.Transformers;

public class TypeTransformerRegistryTests
{
    private readonly TypeTransformerRegistry _registry = new();

    [Fact]
    public void Register_ExactMatch_GetTransformer_ReturnsIt()
    {
        _registry.Register<Guid, string>(new GuidToStringTransformer());

        var result = _registry.GetTransformer(typeof(Guid), typeof(string));

        result.Should().NotBeNull();
        result.Should().BeOfType<GuidToStringTransformer>();
    }

    [Fact]
    public void Register_ByTypePair_GetTransformer_ReturnsIt()
    {
        _registry.Register(typeof(Guid), typeof(string), new GuidToStringTransformer());

        var result = _registry.GetTransformer(typeof(Guid), typeof(string));

        result.Should().NotBeNull();
        result.Should().BeOfType<GuidToStringTransformer>();
    }

    [Fact]
    public void GetTransformer_NoMatch_ReturnsNull()
    {
        var result = _registry.GetTransformer(typeof(int), typeof(Uri));

        result.Should().BeNull();
    }

    [Fact]
    public void RegisterOpen_CanTransformScan_ReturnsMatch()
    {
        _registry.RegisterOpen(new NullableWrapTransformer());

        var result = _registry.GetTransformer(typeof(int), typeof(int?));

        result.Should().NotBeNull();
        result.Should().BeOfType<NullableWrapTransformer>();
    }

    [Fact]
    public void ExactMatch_TakesPrecedence_OverOpenTransformer()
    {
        _registry.Register<string, Guid>(new StringToGuidTransformer());
        _registry.RegisterOpen(new ParsableTransformer());

        var result = _registry.GetTransformer(typeof(string), typeof(Guid));

        result.Should().BeOfType<StringToGuidTransformer>();
    }

    [Fact]
    public void HasTransformer_ReturnsTrue_WhenExists()
    {
        _registry.Register<bool, int>(new BoolToIntTransformer());

        _registry.HasTransformer(typeof(bool), typeof(int)).Should().BeTrue();
    }

    [Fact]
    public void HasTransformer_ReturnsFalse_WhenMissing()
    {
        _registry.HasTransformer(typeof(decimal), typeof(Uri)).Should().BeFalse();
    }

    [Fact]
    public void GetRegisteredPairs_ReturnsAllExactPairs()
    {
        _registry.Register<Guid, string>(new GuidToStringTransformer());
        _registry.Register<bool, int>(new BoolToIntTransformer());

        var pairs = _registry.GetRegisteredPairs();

        pairs.Should().HaveCount(2);
        pairs.Should().Contain(TypePair.Of<Guid, string>());
        pairs.Should().Contain(TypePair.Of<bool, int>());
    }

    [Fact]
    public void RegisterDefaults_RegistersAllBuiltInTransformers()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);

        // Exact matches
        _registry.GetTransformer(typeof(Guid), typeof(string)).Should().BeOfType<GuidToStringTransformer>();
        _registry.GetTransformer(typeof(string), typeof(Guid)).Should().BeOfType<StringToGuidTransformer>();
        _registry.GetTransformer(typeof(bool), typeof(int)).Should().BeOfType<BoolToIntTransformer>();
        _registry.GetTransformer(typeof(int), typeof(bool)).Should().BeOfType<IntToBoolTransformer>();
        _registry.GetTransformer(typeof(byte[]), typeof(string)).Should().BeOfType<ByteArrayToBase64Transformer>();
        _registry.GetTransformer(typeof(string), typeof(byte[])).Should().BeOfType<Base64ToByteArrayTransformer>();
        _registry.GetTransformer(typeof(DateTime), typeof(DateTimeOffset)).Should().BeOfType<DateTimeToDateTimeOffsetTransformer>();
        _registry.GetTransformer(typeof(DateTimeOffset), typeof(DateTime)).Should().BeOfType<DateTimeOffsetToDateTimeTransformer>();

        // Open matches
        _registry.GetTransformer(typeof(int), typeof(int?)).Should().BeOfType<NullableWrapTransformer>();
        _registry.GetTransformer(typeof(string), typeof(int)).Should().BeOfType<ParsableTransformer>();
        _registry.GetTransformer(typeof(int), typeof(string)).Should().BeOfType<ToStringTransformer>();

        // 11 exact-match on netstandard2.1 + 4 NET6_0_OR_GREATER = 15 total
        _registry.Count.Should().Be(15);
        // 10 open transformers in priority order
        _registry.OpenCount.Should().Be(10);
    }

    [Fact]
    public void Clear_RemovesAllTransformers()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);
        _registry.Count.Should().BeGreaterThan(0);

        _registry.Clear();

        _registry.Count.Should().Be(0);
        _registry.OpenCount.Should().Be(0);
        _registry.GetTransformer(typeof(Guid), typeof(string)).Should().BeNull();
    }

    [Fact]
    public void RegisterDefaults_EnumOpenLookup_ReturnsEnumToStringTransformer()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);

        _registry.GetTransformer(typeof(OrderStatus), typeof(string)).Should().BeOfType<EnumToStringTransformer>();
    }

    [Fact]
    public void RegisterDefaults_OperatorOpenLookup_ReturnsOperatorTransformer()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);

        _registry.GetTransformer(typeof(Money), typeof(decimal))
            .Should().BeOfType<ImplicitExplicitOperatorTransformer>();
    }

    [Fact]
    public void RegisterDefaults_IsIdempotent_NoOpenDuplicates()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);
        var firstOpenCount = _registry.OpenCount;

        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);

        _registry.OpenCount.Should().Be(firstOpenCount);
    }

    [Fact]
    public void RegisterDefaults_AllSection71Pairs_ResolveCorrectly()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);

        // Exact-match transformers
        _registry.GetTransformer(typeof(Guid), typeof(string)).Should().BeOfType<GuidToStringTransformer>();
        _registry.GetTransformer(typeof(string), typeof(Guid)).Should().BeOfType<StringToGuidTransformer>();
        _registry.GetTransformer(typeof(string), typeof(Uri)).Should().BeOfType<StringToUriTransformer>();
        _registry.GetTransformer(typeof(Uri), typeof(string)).Should().BeOfType<UriToStringTransformer>();
        _registry.GetTransformer(typeof(bool), typeof(int)).Should().BeOfType<BoolToIntTransformer>();
        _registry.GetTransformer(typeof(int), typeof(bool)).Should().BeOfType<IntToBoolTransformer>();
        _registry.GetTransformer(typeof(byte[]), typeof(string)).Should().BeOfType<ByteArrayToBase64Transformer>();
        _registry.GetTransformer(typeof(string), typeof(byte[])).Should().BeOfType<Base64ToByteArrayTransformer>();
        _registry.GetTransformer(typeof(DateTime), typeof(DateTimeOffset)).Should().BeOfType<DateTimeToDateTimeOffsetTransformer>();
        _registry.GetTransformer(typeof(DateTimeOffset), typeof(DateTime)).Should().BeOfType<DateTimeOffsetToDateTimeTransformer>();
        _registry.GetTransformer(typeof(string), typeof(DateTime)).Should().BeOfType<StringToDateTimeTransformer>();

        // NET6+ exact-match
        _registry.GetTransformer(typeof(DateTime), typeof(DateOnly)).Should().BeOfType<DateTimeToDateOnlyTransformer>();
        _registry.GetTransformer(typeof(DateTime), typeof(TimeOnly)).Should().BeOfType<DateTimeToTimeOnlyTransformer>();
        _registry.GetTransformer(typeof(DateOnly), typeof(DateTime)).Should().BeOfType<DateOnlyToDateTimeTransformer>();
        _registry.GetTransformer(typeof(TimeOnly), typeof(TimeSpan)).Should().BeOfType<TimeOnlyToTimeSpanTransformer>();

        // Open transformer lookups
        _registry.GetTransformer(typeof(int), typeof(int?)).Should().BeOfType<NullableWrapTransformer>();
        _registry.GetTransformer(typeof(int?), typeof(int)).Should().BeOfType<NullableUnwrapTransformer>();
        _registry.GetTransformer(typeof(OrderStatus), typeof(string)).Should().BeOfType<EnumToStringTransformer>();
        _registry.GetTransformer(typeof(string), typeof(OrderStatus)).Should().BeOfType<StringToEnumTransformer>();
        _registry.GetTransformer(typeof(OrderStatus), typeof(OrderStatusDto)).Should().BeOfType<EnumToEnumTransformer>();
        _registry.GetTransformer(typeof(Money), typeof(decimal)).Should().BeOfType<ImplicitExplicitOperatorTransformer>();
        _registry.GetTransformer(typeof(JsonElement), typeof(JsonTestDto)).Should().BeOfType<JsonElementToObjectTransformer>();
        _registry.GetTransformer(typeof(JsonTestDto), typeof(JsonElement)).Should().BeOfType<ObjectToJsonElementTransformer>();
        _registry.GetTransformer(typeof(string), typeof(int)).Should().BeOfType<ParsableTransformer>();
        _registry.GetTransformer(typeof(string), typeof(decimal)).Should().BeOfType<ParsableTransformer>();
        _registry.GetTransformer(typeof(int), typeof(string)).Should().BeOfType<ToStringTransformer>();
    }

    [Fact]
    public void ConcurrentLookups_ThreadSafe()
    {
        TypeTransformerRegistryDefaults.RegisterDefaults(_registry);

        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 100, _ =>
        {
            try
            {
                _registry.GetTransformer(typeof(Guid), typeof(string)).Should().NotBeNull();
                _registry.GetTransformer(typeof(string), typeof(int)).Should().NotBeNull();
                _registry.GetTransformer(typeof(int), typeof(int?)).Should().NotBeNull();
                _registry.GetTransformer(typeof(OrderStatus), typeof(string)).Should().NotBeNull();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }
}
