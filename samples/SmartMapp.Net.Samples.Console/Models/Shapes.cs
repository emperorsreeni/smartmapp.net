namespace SmartMapp.Net.Samples.Console.Models;

/// <summary>
/// Base origin shape — used with <see cref="Circle"/> and <see cref="Rectangle"/> to demonstrate
/// polymorphic / inheritance-aware mapping. Uses domain-model naming (the spec memo notes
/// <c>Shape</c>/<c>Circle</c>/<c>Rectangle</c> are domain terms, not library terminology).
/// </summary>
public abstract class Shape
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class Circle : Shape
{
    public double Radius { get; init; }
}

public sealed class Rectangle : Shape
{
    public double Width { get; init; }
    public double Height { get; init; }
}

/// <summary>Base target DTO for <see cref="Shape"/>.</summary>
public class ShapeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CircleDto : ShapeDto
{
    public double Radius { get; set; }
}

public sealed class RectangleDto : ShapeDto
{
    public double Width { get; set; }
    public double Height { get; set; }
}
