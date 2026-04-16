using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.DTOs.Inventory;
using FloreriaBautista.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace FloreriaBautista.Tests;

public class InventoryServiceTests
{
    private readonly AppDbContext _context;
    private readonly Mock<ILogger<InventoryService>> _loggerMock;
    private readonly InventoryService _service;

    public InventoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new AppDbContext(options);
        _loggerMock = new Mock<ILogger<InventoryService>>();
        _service = new InventoryService(_context, _loggerMock.Object);
    }

    [Fact]
    public async Task ListarAsync_ShouldReturnPagedResults()
    {
        // Arrange
        _context.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), Nombre = "Rosas", Sucursal = "Norte", Activo = true });
        _context.InventoryItems.Add(new InventoryItem { Id = Guid.NewGuid(), Nombre = "Tulipanes", Sucursal = "Norte", Activo = true });
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.ListarAsync("Norte", null, null, 1, 10);

        // Assert
        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_Entrada_ShouldIncreaseStock()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new InventoryItem { Id = itemId, Nombre = "Rosas", StockActual = 10, Sucursal = "Norte", Activo = true };
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        var request = new RegisterMovementRequestDto { InventoryItemId = itemId, Tipo = "ENTRADA", Cantidad = 5, Motivo = "Compra" };
        var usuarioId = Guid.NewGuid();

        // Act
        var result = await _service.RegistrarMovimientoAsync(request, usuarioId);

        // Assert
        Assert.Equal(15, result.StockDespues);
        var updatedItem = await _context.InventoryItems.FindAsync(itemId);
        Assert.Equal(15, updatedItem.StockActual);
    }

    [Fact]
    public async Task RegistrarMovimientoAsync_SalidaInsuficiente_ShouldThrowAppException()
    {
        // Arrange
        var itemId = Guid.NewGuid();
        var item = new InventoryItem { Id = itemId, Nombre = "Rosas", StockActual = 2, Sucursal = "Norte", Activo = true };
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        var request = new RegisterMovementRequestDto { InventoryItemId = itemId, Tipo = "SALIDA", Cantidad = 10, Motivo = "Venta" };
        var usuarioId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<FloreriaBautista.Models.Exceptions.AppException>(() => 
            _service.RegistrarMovimientoAsync(request, usuarioId));
    }
}
