// Sprint 8 · S8-T09 — runnable console sample exercising every headline v1.0 SmartMapp.Net
// feature. Each scenario is self-contained: it builds a dedicated sculptor, performs a
// mapping, and prints a clearly delimited section header plus before/after output. Exits
// with code 0 on success; any scenario-level exception is surfaced to stderr and the process
// exits with code 1 — enabling the CI smoke-test step to assert exit code.
//
// Helpers (Section / PrintBeforeAfter / SampleData) live in sibling files per spec §S8-T09
// Technical Considerations bullet 2.

using Microsoft.Extensions.DependencyInjection;
using SmartMapp.Net;
using SmartMapp.Net.Extensions;
using SmartMapp.Net.Samples.Console.Blueprints;
using SmartMapp.Net.Samples.Console.Fixtures;
using SmartMapp.Net.Samples.Console.Models;
using static SmartMapp.Net.Samples.Console.Output.ConsoleOutput;

const int ExitSuccess = 0;
const int ExitFailure = 1;

try
{
    Section("1. Zero-Config Flat Mapping", () =>
    {
        // No blueprint, no attributes — just Bind<Customer, CustomerDto>() with convention
        // matching doing all the work. Customer.Id → CustomerDto.Id, same for Name and Email.
        var sculptor = new SculptorBuilder()
            .Configure(options => options.Bind<Customer, CustomerDto>(_ => { }))
            .Forge();

        var customer = SampleData.Customer();
        var dto = sculptor.Map<Customer, CustomerDto>(customer);

        PrintBeforeAfter(customer, dto);
    });

    Section("2. Flattening (Customer.Address.City -> CustomerAddressCity)", () =>
    {
        // The flattening convention auto-links Customer.Address.City to CustomerDto.AddressCity
        // and Customer.Address.PostalCode to CustomerDto.AddressPostalCode — no fluent rule.
        var sculptor = new SculptorBuilder()
            .Configure(options => options.Bind<Customer, CustomerDto>(_ => { }))
            .Forge();

        var customer = SampleData.Customer();
        var dto = sculptor.Map<Customer, CustomerDto>(customer);

        System.Console.WriteLine($"  Source Customer.Address = {customer.Address.Street}, " +
                                 $"{customer.Address.City}, {customer.Address.PostalCode}");
        System.Console.WriteLine($"  Target DTO AddressCity       = {dto.AddressCity}");
        System.Console.WriteLine($"  Target DTO AddressPostalCode = {dto.AddressPostalCode}");
    });

    Section("3. Collection Mapping (MapAll)", () =>
    {
        var sculptor = new SculptorBuilder()
            .Configure(options => options.Bind<OrderLine, OrderLineDto>(_ => { }))
            .Forge();

        var lines = SampleData.OrderLines();
        var dtos = sculptor.MapAll<OrderLine, OrderLineDto>(lines);

        System.Console.WriteLine($"  Source lines: {lines.Count}");
        foreach (var line in lines)
            System.Console.WriteLine($"    - {line.Sku} x{line.Quantity} @ {line.UnitPrice:0.00}");

        System.Console.WriteLine($"  Mapped dtos : {dtos.Count}");
        foreach (var dto in dtos)
            System.Console.WriteLine($"    - {dto.Sku} x{dto.Quantity} @ {dto.UnitPrice:0.00}");
    });

    Section("4. Polymorphic / Inheritance-Aware Mapping", () =>
    {
        // ExtendWith<Derived, Derived>() registers the polymorphic derived pairs against the
        // base Shape -> ShapeDto binding so Map<Shape, ShapeDto>(circle) returns a CircleDto,
        // not a sliced ShapeDto.
        var sculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Bind<Shape, ShapeDto>(rule => rule
                    .ExtendWith<Circle, CircleDto>()
                    .ExtendWith<Rectangle, RectangleDto>());
                options.Bind<Circle, CircleDto>(_ => { });
                options.Bind<Rectangle, RectangleDto>(_ => { });
            })
            .Forge();

        Shape circle = new Circle { Id = 1, Name = "unit-circle", Radius = 1.0 };
        Shape rect = new Rectangle { Id = 2, Name = "square", Width = 5, Height = 5 };

        var circleDto = sculptor.Map<Shape, ShapeDto>(circle);
        var rectDto = sculptor.Map<Shape, ShapeDto>(rect);

        System.Console.WriteLine($"  Shape(Circle)    -> runtime {circleDto.GetType().Name} " +
                                 $"(expected CircleDto)");
        System.Console.WriteLine($"  Shape(Rectangle) -> runtime {rectDto.GetType().Name} " +
                                 $"(expected RectangleDto)");

        if (circleDto is CircleDto c)
            System.Console.WriteLine($"  Circle.Radius       = {c.Radius}");
        if (rectDto is RectangleDto r)
            System.Console.WriteLine($"  Rectangle (W x H)   = {r.Width} x {r.Height}");
    });

    Section("5. Inline Bind (options.Bind<S, D>)", () =>
    {
        // Inline fluent configuration — no blueprint class required. Same expressive power.
        var sculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Bind<OrderLine, OrderLineDto>(rule => { });
                options.Bind<Order, OrderDto>(rule => rule
                    .Property(d => d.Total,
                              p => p.From(o => o.Lines.Sum(l => l.Quantity * l.UnitPrice))));
            })
            .Forge();

        var order = SampleData.Order();
        var dto = sculptor.Map<Order, OrderDto>(order);

        System.Console.WriteLine($"  Order.Id            = {dto.Id}");
        System.Console.WriteLine($"  CustomerName        = \"{dto.CustomerName}\"");
        System.Console.WriteLine($"  CustomerAddressCity = {dto.CustomerAddressCity}");
        System.Console.WriteLine($"  Total (computed)    = {dto.Total:0.00}");
        System.Console.WriteLine($"  Lines.Count         = {dto.Lines.Count}");
    });

    Section("6. Blueprint Class (reusable MappingBlueprint)", () =>
    {
        // Same mapping as scenario 5 but captured in a reusable OrderBlueprint class.
        var sculptor = new SculptorBuilder()
            .UseBlueprint<OrderBlueprint>()
            .Forge();

        var order = SampleData.Order();
        var dto = sculptor.Map<Order, OrderDto>(order);

        System.Console.WriteLine($"  Order.Id           = {dto.Id}");
        System.Console.WriteLine($"  CustomerName       = \"{dto.CustomerName}\" " +
                                 "(OnMapped trimmed whitespace)");
        System.Console.WriteLine($"  Total              = {dto.Total:0.00}");
        System.Console.WriteLine($"  First Line         = {dto.Lines[0].Sku} x{dto.Lines[0].Quantity}");
    });

    Section("7. Attribute-Based Configuration ([MappedBy<T>] + [Unmapped])", () =>
    {
        // Scanning the calling assembly auto-discovers [MappedBy<Product>] on ProductDto —
        // no fluent or blueprint configuration needed. [Unmapped] blocks InternalNotes.
        var sculptor = new SculptorBuilder()
            .ScanAssembliesContaining<Product>()
            .Forge();

        var product = new Product
        {
            Id = 101,
            Name = "Mechanical Keyboard",
            Price = 149.99m,
            InternalNotes = "do-not-ship",
        };

        var dto = sculptor.Map<Product, ProductDto>(product);

        System.Console.WriteLine($"  Id            = {dto.Id}");
        System.Console.WriteLine($"  Name          = {dto.Name}");
        System.Console.WriteLine($"  Price         = {dto.Price:0.00}");
        System.Console.WriteLine($"  InternalNotes = \"{dto.InternalNotes}\" " +
                                 "(blocked by [Unmapped])");
    });

    Section("8. MapTo<T>() Object Extension (ambient sculptor via DI)", () =>
    {
        // services.AddSculptor() installs the ambient ISculptor accessor — the opt-in
        // MapTo<T>() extension (SmartMapp.Net.Extensions namespace) resolves that ambient
        // instead of requiring explicit sculptor arguments at every call site.
        var services = new ServiceCollection();
        services.AddSculptor(options =>
        {
            options.Bind<Product, ProductDto>(_ => { });
        });

        using var provider = services.BuildServiceProvider();
        // Force the lazy forge + ambient install by resolving the sculptor once.
        _ = provider.GetRequiredService<ISculptor>();

        var product = new Product { Id = 7, Name = "Desk Lamp", Price = 39m };
        var dto = product.MapTo<ProductDto>();   // ambient — no explicit sculptor argument

        System.Console.WriteLine($"  product.MapTo<ProductDto>() -> Id={dto.Id}, " +
                                 $"Name={dto.Name}, Price={dto.Price:0.00}");
    });

    Section("9. Bidirectional Mapping (.Bidirectional)", () =>
    {
        // .Bidirectional() auto-generates the inverse blueprint (CustomerDto -> Customer)
        // so both directions are mappable from a single binding declaration.
        var sculptor = new SculptorBuilder()
            .Configure(options =>
            {
                options.Bind<Customer, CustomerDto>(rule => rule.Bidirectional());
            })
            .Forge();

        var customer = SampleData.Customer();
        var forward = sculptor.Map<Customer, CustomerDto>(customer);
        var roundTrip = sculptor.Map<CustomerDto, Customer>(forward);

        System.Console.WriteLine($"  Forward : Customer.Name=\"{customer.Name}\" -> DTO.Name=\"{forward.Name}\"");
        System.Console.WriteLine($"  Reverse : DTO.Name=\"{forward.Name}\" -> Customer.Name=\"{roundTrip.Name}\"");
        System.Console.WriteLine($"  Ids match: {customer.Id == roundTrip.Id}");
    });

    System.Console.WriteLine();
    System.Console.WriteLine("=== All scenarios completed successfully. ===");
    return ExitSuccess;
}
catch (Exception ex)
{
    System.Console.Error.WriteLine();
    System.Console.Error.WriteLine($"!!! Sample failed: {ex.GetType().Name}: {ex.Message}");
    System.Console.Error.WriteLine(ex.StackTrace);
    return ExitFailure;
}
