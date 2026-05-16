using SmartMapp.Net.Attributes;

namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Attribute-based origin/target demo. <see cref="ProductDto"/> opts in via
/// <c>[MappedBy&lt;Product&gt;]</c>; <see cref="ProductDto.InternalNotes"/> is ignored via
/// <c>[Unmapped]</c>. No fluent or blueprint configuration required.
/// </summary>
public sealed class Product
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string InternalNotes { get; init; } = string.Empty;
}

[MappedBy<Product>]
public sealed class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }

    [Unmapped]
    public string InternalNotes { get; set; } = string.Empty;
}
