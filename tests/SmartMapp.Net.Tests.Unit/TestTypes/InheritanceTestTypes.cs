namespace SmartMapp.Net.Tests.Unit.TestTypes;

// --- Inheritance hierarchy for polymorphic mapping tests ---

// Base domain model
public class Shape
{
    public int Id { get; set; }
    public string Color { get; set; } = string.Empty;
    public string ShapeType { get; set; } = string.Empty;
}

// Derived domain models
public class Circle : Shape
{
    public double Radius { get; set; }
}

public class Rectangle : Shape
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public class Square : Rectangle
{
    public double Side { get; set; }
}

// 3-level hierarchy: Shape → Circle → FilledCircle (spec requirement)
public class FilledCircle : Circle
{
    public string FillColor { get; set; } = string.Empty;
    public double FillOpacity { get; set; } = 1.0;
}

// Base DTO
public class ShapeDto
{
    public int Id { get; set; }
    public string Color { get; set; } = string.Empty;
}

// Derived DTOs
public class CircleDto : ShapeDto
{
    public double Radius { get; set; }
}

public class RectangleDto : ShapeDto
{
    public double Width { get; set; }
    public double Height { get; set; }
}

public class SquareDto : RectangleDto
{
    public double Side { get; set; }
}

// 3-level DTO hierarchy: ShapeDto → CircleDto → FilledCircleDto
public class FilledCircleDto : CircleDto
{
    public string FillColor { get; set; } = string.Empty;
    public double FillOpacity { get; set; }
}

// --- Interface target types ---

public interface IPersonDto
{
    string FirstName { get; set; }
    string LastName { get; set; }
    int Age { get; set; }
}

public class PersonSource
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class PersonDtoImpl : IPersonDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
}

// --- Abstract target types ---

public abstract class AbstractAnimal
{
    public abstract string Name { get; set; }
    public abstract int Legs { get; set; }
}

public class ConcreteAnimal : AbstractAnimal
{
    public override string Name { get; set; } = string.Empty;
    public override int Legs { get; set; }
}

public class AnimalSource
{
    public string Name { get; set; } = string.Empty;
    public int Legs { get; set; }
}

// --- Bidirectional mapping types ---

public class BidiProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class BidiProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// --- Blueprint inheritance types ---

public class Vehicle
{
    public int Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
}

public class Car : Vehicle
{
    public int Doors { get; set; }
}

public class VehicleDto
{
    public int Id { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
}

public class CarDto : VehicleDto
{
    public int Doors { get; set; }
}

// --- Discriminator-based mapping types ---

public class Notification
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class NotificationDto
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class EmailNotificationDto : NotificationDto
{
    public string EmailAddress { get; set; } = string.Empty;
}

public class SmsNotificationDto : NotificationDto
{
    public string PhoneNumber { get; set; } = string.Empty;
}
