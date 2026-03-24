using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Common;
using FloreriaBautista.Models.DTOs.Flowers;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Models.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Controllers;

[ApiController]
[Route("api/admin/flowers")]
[Authorize(Roles = "ADMIN,INVENTARIO")]
public class AdminFlowersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminFlowersController> _logger;

    public AdminFlowersController(AppDbContext context, ILogger<AdminFlowersController> logger)
    {
        _context = context;
        _logger  = logger;
    }

    // GET /api/admin/flowers?busqueda=rosa&color=rojo&bajoMinimo=true
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? busqueda,
        [FromQuery] string? color,
        [FromQuery] bool?   bajoMinimo,
        [FromQuery] string? estado,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var query = _context.Flowers.AsQueryable();

        if (!string.IsNullOrWhiteSpace(busqueda))
            query = query.Where(f => f.Nombre.Contains(busqueda));
        if (!string.IsNullOrWhiteSpace(color))
            query = query.Where(f => f.Color.ToLower() == color.ToLower());
        if (!string.IsNullOrWhiteSpace(estado))
            query = query.Where(f => f.Estado == estado.ToUpper());

        var flores = await query.OrderBy(f => f.Nombre).ToListAsync();

        if (bajoMinimo == true)
            flores = flores.Where(f => f.StockActual <= f.StockMinimo).ToList();

        var total    = flores.Count;
        var paginado = flores.Skip((page - 1) * size).Take(size).Select(MapToDto).ToList();

        return Ok(ApiResponseDto<PagedResultDto<FlowerDto>>.Ok(new PagedResultDto<FlowerDto>
        {
            Items        = paginado,
            Total        = total,
            Pagina       = page,
            TamanoPagina = size,
            TotalPaginas = (int)Math.Ceiling(total / (double)size)
        }));
    }

    // GET /api/admin/flowers/{flowerId}
    [HttpGet("{flowerId:guid}")]
    public async Task<IActionResult> Detalle(Guid flowerId)
    {
        var flor = await _context.Flowers.FindAsync(flowerId)
            ?? throw new NotFoundException("Flor", flowerId);
        return Ok(ApiResponseDto<FlowerDto>.Ok(MapToDto(flor)));
    }

    // POST /api/admin/flowers
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateFlowerRequestDto request)
    {
        var flor = new Flower
        {
            Id             = Guid.NewGuid(),
            Nombre         = request.Nombre.Trim(),
            Color          = request.Color.Trim(),
            PrecioCosto    = request.PrecioCosto,
            UnidadMedida   = request.UnidadMedida.ToUpper(),
            EsFlorPrimaria = request.EsFlorPrimaria,
            StockMinimo    = request.StockMinimo,
            Estado         = "ACTIVA",
            CreadoEn      = DateTime.UtcNow,
            ActualizadoEn = DateTime.UtcNow
        };

        _context.Flowers.Add(flor);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Flor creada: {Nombre} ({Id})", flor.Nombre, flor.Id);
        return Ok(ApiResponseDto<FlowerDto>.Ok(MapToDto(flor), "Flor creada correctamente."));
    }

    // POST /api/admin/flowers/{flowerId} — editar
    [HttpPost("{flowerId:guid}")]
    public async Task<IActionResult> Actualizar(Guid flowerId, [FromBody] UpdateFlowerRequestDto request)
    {
        var flor = await _context.Flowers.FindAsync(flowerId)
            ?? throw new NotFoundException("Flor", flowerId);

        if (!string.IsNullOrWhiteSpace(request.Nombre))      flor.Nombre       = request.Nombre.Trim();
        if (!string.IsNullOrWhiteSpace(request.Color))       flor.Color        = request.Color.Trim();
        if (request.PrecioCosto.HasValue)                    flor.PrecioCosto  = request.PrecioCosto.Value;
        if (!string.IsNullOrWhiteSpace(request.UnidadMedida)) flor.UnidadMedida = request.UnidadMedida.ToUpper();
        if (request.EsFlorPrimaria.HasValue)                     flor.EsFlorPrimaria = request.EsFlorPrimaria.Value;
        if (request.StockMinimo.HasValue)                    flor.StockMinimo  = request.StockMinimo.Value;
        if (!string.IsNullOrWhiteSpace(request.Estado))      flor.Estado       = request.Estado.ToUpper();
        flor.ActualizadoEn = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ApiResponseDto<FlowerDto>.Ok(MapToDto(flor), "Flor actualizada correctamente."));
    }

    // POST /api/admin/flowers/{flowerId}/movements — registrar movimiento de stock
    [HttpPost("{flowerId:guid}/movements")]
    public async Task<IActionResult> RegistrarMovimiento(
        Guid flowerId, [FromBody] RegisterFlowerMovementDto request)
    {
        var flor = await _context.Flowers.FindAsync(flowerId)
            ?? throw new NotFoundException("Flor", flowerId);

        var tipo = request.Tipo.ToUpper();
        if (tipo != "ENTRADA" && tipo != "SALIDA" && tipo != "AJUSTE")
            return BadRequest(ApiResponseDto<object>.Fail("Tipo inválido. Use: ENTRADA, SALIDA o AJUSTE."));

        var stockAntes = flor.StockActual;

        flor.StockActual = tipo switch
        {
            "ENTRADA" => flor.StockActual + request.Cantidad,
            "SALIDA"  => flor.StockActual - request.Cantidad,
            "AJUSTE"  => request.Cantidad,
            _         => flor.StockActual
        };

        if (flor.StockActual < 0)
            return BadRequest(ApiResponseDto<object>.Fail(
                $"Stock insuficiente. Stock actual: {stockAntes}, se intenta retirar: {request.Cantidad}"));

        flor.ActualizadoEn = DateTime.UtcNow;

        var usuarioId = ObtenerUsuarioId();
        var movimiento = new FlowerMovement
        {
            Id        = Guid.NewGuid(),
            FlowerId  = flowerId,
            Tipo      = tipo,
            Cantidad  = request.Cantidad,
            Motivo    = request.Motivo,
            UsuarioId = usuarioId ?? Guid.Empty,
            FechaHora = DateTime.UtcNow
        };

        _context.FlowerMovements.Add(movimiento);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Movimiento {Tipo} en flor {Id}: {Antes}→{Despues}",
            tipo, flowerId, stockAntes, flor.StockActual);

        return Ok(ApiResponseDto<FlowerMovementDto>.Ok(new FlowerMovementDto
        {
            Id           = movimiento.Id,
            FlowerId     = flowerId,
            Flor         = flor.Nombre,
            Tipo         = tipo,
            Cantidad     = request.Cantidad,
            StockAntes   = stockAntes,
            StockDespues = flor.StockActual,
            Motivo       = request.Motivo,
            FechaHora    = movimiento.FechaHora
        }, "Movimiento registrado."));
    }

    // GET /api/admin/flowers/{flowerId}/movements
    [HttpGet("{flowerId:guid}/movements")]
    public async Task<IActionResult> ListarMovimientos(
        Guid flowerId,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var total = await _context.FlowerMovements
            .Where(m => m.FlowerId == flowerId).CountAsync();

        var items = await _context.FlowerMovements
            .Include(m => m.Flower)
            .Where(m => m.FlowerId == flowerId)
            .OrderByDescending(m => m.FechaHora)
            .Skip((page - 1) * size).Take(size)
            .Select(m => new FlowerMovementDto
            {
                Id        = m.Id,
                FlowerId  = m.FlowerId,
                Flor      = m.Flower.Nombre,
                Tipo      = m.Tipo,
                Cantidad  = m.Cantidad,
                Motivo    = m.Motivo,
                FechaHora = m.FechaHora
            }).ToListAsync();

        return Ok(ApiResponseDto<PagedResultDto<FlowerMovementDto>>.Ok(
            new PagedResultDto<FlowerMovementDto>
            {
                Items        = items,
                Total        = total,
                Pagina       = page,
                TamanoPagina = size,
                TotalPaginas = (int)Math.Ceiling(total / (double)size)
            }));
    }

    // GET /api/admin/flowers/product/{productId} — flores que usa un producto
    [HttpGet("product/{productId:guid}")]
    public async Task<IActionResult> FloresPorProducto(Guid productId)
    {
        var flores = await _context.ProductFlowers
            .Include(pf => pf.Flower)
            .Where(pf => pf.ProductId == productId)
            .Select(pf => new ProductFlowerDto
            {
                FlowerId     = pf.FlowerId,
                NombreFlor   = pf.Flower.Nombre,
                Color        = pf.Flower.Color,
                Cantidad     = pf.Cantidad,
                UnidadMedida = pf.Flower.UnidadMedida
            }).ToListAsync();

        return Ok(ApiResponseDto<List<ProductFlowerDto>>.Ok(flores));
    }

    // POST /api/admin/flowers/product/{productId} — asignar flores a producto
    [HttpPost("product/{productId:guid}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> AsignarFloresAProducto(
        Guid productId, [FromBody] UpdateProductFlowersDto request)
    {
        var producto = await _context.Products.FindAsync(productId)
            ?? throw new NotFoundException("Producto", productId);

        // Eliminar relaciones anteriores
        var anteriores = await _context.ProductFlowers
            .Where(pf => pf.ProductId == productId).ToListAsync();
        _context.RemoveRange(anteriores);

        // Insertar nuevas
        foreach (var item in request.Flores)
        {
            var flor = await _context.Flowers.FindAsync(item.FlowerId)
                ?? throw new AppException($"Flor '{item.FlowerId}' no encontrada.");

            _context.ProductFlowers.Add(new ProductFlower
            {
                ProductId = productId,
                FlowerId  = item.FlowerId,
                Cantidad  = item.Cantidad
            });
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Flores asignadas al producto {Id}: {Count} flores",
            productId, request.Flores.Count);

        return Ok(ApiResponseDto<object>.Ok(null,
            $"{request.Flores.Count} flores asignadas al producto correctamente."));
    }

    // GET /api/admin/flowers/product/{productId}/costo — calcular costo en tiempo real
    [HttpGet("product/{productId:guid}/costo")]
    public async Task<IActionResult> CalcularCostoProducto(Guid productId)
    {
        var producto = await _context.Products.FindAsync(productId)
            ?? throw new NotFoundException("Producto", productId);

        var receta = await _context.ProductFlowers
            .Include(pf => pf.Flower)
            .Where(pf => pf.ProductId == productId)
            .ToListAsync();

        return Ok(ApiResponseDto<CostoProductoDto>.Ok(CalcularCosto(producto, receta)));
    }

    // POST /api/admin/flowers/product/{productId}/precio — guardar precio final
    [HttpPost("product/{productId:guid}/precio")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> ActualizarPrecio(
        Guid productId, [FromBody] ActualizarPrecioRequestDto request)
    {
        var producto = await _context.Products.FindAsync(productId)
            ?? throw new NotFoundException("Producto", productId);

        var receta = await _context.ProductFlowers
            .Include(pf => pf.Flower)
            .Where(pf => pf.ProductId == productId)
            .ToListAsync();

        // Recalcular costo base con flores primarias
        var costoBase = receta
            .Where(pf => pf.Flower.EsFlorPrimaria)
            .Sum(pf => pf.Cantidad * pf.Flower.PrecioCosto);

        var precioSugerido = Math.Round(costoBase * request.MargenFactor, 2);

        producto.CostoBase      = costoBase;
        producto.MargenFactor   = request.MargenFactor;
        producto.PrecioSugerido = precioSugerido;

        // Si se manda precio final manual, usarlo como PrecioBase del producto
        if (request.PrecioFinal.HasValue)
            producto.PrecioBase = request.PrecioFinal.Value;
        else
            producto.PrecioBase = precioSugerido;

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Precio actualizado producto {Id}: costo={Costo} margen={Margen} precio={Precio}",
            productId, costoBase, request.MargenFactor, producto.PrecioBase);

        return Ok(ApiResponseDto<CostoProductoDto>.Ok(
            CalcularCosto(producto, receta),
            $"Precio actualizado. Costo base: {costoBase:C} | Precio final: {producto.PrecioBase:C}"));
    }

    // POST /api/admin/flowers/product/{productId} — asignar flores Y recalcular costo
    private Guid? ObtenerUsuarioId()
    {
        var claim = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static FlowerDto MapToDto(Flower f) => new()
    {
        Id             = f.Id,
        Nombre         = f.Nombre,
        Color          = f.Color,
        PrecioCosto    = f.PrecioCosto,
        UnidadMedida   = f.UnidadMedida,
        EsFlorPrimaria = f.EsFlorPrimaria,
        StockActual    = f.StockActual,
        StockMinimo    = f.StockMinimo,
        Estado         = f.Estado,
        CreadoEn       = f.CreadoEn
    };

    // ── Calcular costo de producto basado en flores primarias ──────
    private static CostoProductoDto CalcularCosto(Product producto, List<ProductFlower> receta)
    {
        var insumos = receta.Select(pf => new InsumoCalculoDto
        {
            FlowerId       = pf.FlowerId,
            Nombre         = pf.Flower.Nombre,
            Color          = pf.Flower.Color,
            EsFlorPrimaria = pf.Flower.EsFlorPrimaria,
            Cantidad       = pf.Cantidad,
            CostoUnitario  = pf.Flower.PrecioCosto,
            Subtotal       = pf.Cantidad * pf.Flower.PrecioCosto
        }).ToList();

        var costoBase      = insumos.Where(i => i.EsFlorPrimaria).Sum(i => i.Subtotal);
        var precioSugerido = Math.Round(costoBase * producto.MargenFactor, 2);

        return new CostoProductoDto
        {
            ProductId      = producto.Id,
            NombreProducto = producto.Nombre,
            CostoBase      = costoBase,
            MargenFactor   = producto.MargenFactor,
            PrecioSugerido = precioSugerido,
            Insumos        = insumos
        };
    }
}
