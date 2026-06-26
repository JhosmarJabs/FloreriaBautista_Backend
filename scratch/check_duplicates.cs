using Microsoft.EntityFrameworkCore;
using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

// Setup
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("DB_URL") ?? "Host=localhost;Database=floreria_bautista;Username=postgres;Password=password");

using var context = new AppDbContext(optionsBuilder.Options);

var duplicates = await context.InventoryItems
    .GroupBy(i => new { i.Nombre, i.Sucursal })
    .Where(g => g.Count() > 1)
    .Select(g => new { g.Key.Nombre, g.Key.Sucursal, Count = g.Count() })
    .ToListAsync();

Console.WriteLine("--- Duplicados por Nombre y Sucursal ---");
foreach (var d in duplicates)
{
    Console.WriteLine($"{d.Nombre} | {d.Sucursal} | {d.Count}");
}

var multiSucursal = await context.InventoryItems
    .GroupBy(i => i.Nombre)
    .Where(g => g.Select(i => i.Sucursal).Distinct().Count() > 1)
    .Select(g => new { Nombre = g.Key, Sucursales = string.Join(", ", g.Select(i => i.Sucursal).Distinct()) })
    .ToListAsync();

Console.WriteLine("\n--- Items en Múltiples Sucursales ---");
foreach (var m in multiSucursal)
{
    Console.WriteLine($"{m.Nombre} | {m.Sucursales}");
}
