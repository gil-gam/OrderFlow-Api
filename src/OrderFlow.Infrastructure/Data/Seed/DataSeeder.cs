using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.ValueObjects;

namespace OrderFlow.Infrastructure.Data.Seed;

public static class DataSeeder
{
    public static async Task SeedAsync(
        OrderFlowDbContext context,
        IConfiguration configuration,
        IHostEnvironment environment,
        CancellationToken cancellationToken = default)
    {
        // Security guard: the bootstrap user is only seeded in Development/Test.
        // In Production, users must be provisioned via the register endpoint or
        // an external identity provider — never through a hardcoded seed.
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test"))
            return;

        // ── Bootstrap admin user (idempotent by email) ──────────
        var adminEmail = configuration["Seed:AdminEmail"] ?? "admin@orderflow.dev";
        var adminPassword = configuration["Seed:AdminPassword"]
            ?? throw new InvalidOperationException(
                "Seed:AdminPassword is required when seeding the bootstrap user.");

        if (!await context.Users.AnyAsync(u => u.Email == adminEmail, cancellationToken))
        {
            context.Users.Add(new User(
                name: "Administrator",
                email: adminEmail,
                passwordHash: BCrypt.HashPassword(adminPassword)));
        }

        // ── Demo domain data (idempotent by entity) ─────────────
        if (!await context.Orders.AnyAsync(cancellationToken))
        {
            var address = new Address("123 Main St", "New York", "NY", "10001", "USA");
            var order = Order.Create(Guid.NewGuid(), address);
            order.AddItem(Guid.NewGuid(), "Product A", 2, new Money(49.99m, "USD"));
            order.AddItem(Guid.NewGuid(), "Product B", 1, new Money(129.99m, "USD"));
            context.Orders.Add(order);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
