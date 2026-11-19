using BCryptNet = BCrypt.Net.BCrypt;
using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FloreriaBautista.PlayReviewSeed;

/// <summary>
/// Todo lo que toca la base de revisión: leer su estado (para las guardas), crear o
/// verificar el esquema, poner el marcador, limpiar y cargar el dataset.
/// </summary>
public static class BaseRevision
{
    /// <summary>
    /// Tablas que NO se vacían al reiniciar: las cuentas de revisión conservan su id y
    /// sus sesiones (Google puede estar a media revisión cuando se resiembra).
    /// </summary>
    public static readonly IReadOnlySet<string> TablasConservadas =
        new HashSet<string>(StringComparer.Ordinal) { "users", "roles", "user_roles", "auth_tokens", "push_token_devices" };

    // ── Estado (solo lectura) ────────────────────────────────────

    public static async Task<EstadoDestino> LeerEstadoAsync(NpgsqlConnection cn)
    {
        var tablas = await EscalarAsync<long>(cn,
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");

        async Task<(bool Existe, long Filas)> Contar(string tabla)
        {
            var existe = await EscalarAsync<bool>(cn, $"SELECT to_regclass('public.{tabla}') IS NOT NULL");
            return existe ? (true, await EscalarAsync<long>(cn, $"SELECT count(*) FROM public.{tabla}")) : (false, 0);
        }

        var usuarios = await Contar("users");
        var clientes = await Contar("customers");
        var pedidos  = await Contar("orders");

        var marcadorExiste = await EscalarAsync<bool>(cn, "SELECT to_regclass('public._entorno') IS NOT NULL");
        var esRevision = false;
        if (marcadorExiste)
        {
            var buenos = await EscalarAsync<long>(cn, "SELECT count(*) FROM public._entorno WHERE valor = 'play_review'");
            var otros  = await EscalarAsync<long>(cn, "SELECT count(*) FROM public._entorno WHERE valor IS DISTINCT FROM 'play_review'");
            esRevision = buenos > 0 && otros == 0;
        }

        return new EstadoDestino((int)tablas, usuarios.Existe, usuarios.Filas, clientes.Filas, pedidos.Filas,
            marcadorExiste, esRevision);
    }

    // ── Esquema ──────────────────────────────────────────────────

    /// <summary>
    /// Base vacía: se crea con el script que EF genera del modelo (el mismo AppDbContext
    /// que usa la app, incluidos índices, CHECK y filas semilla). Base con tablas (rama
    /// "schema-only" de Neon): se usa tal cual, pero antes se verifica que tenga todas las
    /// tablas y columnas del modelo; si falta algo se aborta sin escribir.
    /// </summary>
    public static async Task PrepararEsquemaAsync(AppDbContext ctx, EstadoDestino estado, TextWriter log)
    {
        if (estado.TablasEnPublic == 0)
        {
            log.WriteLine("  Base vacía: creando el esquema desde el modelo EF (GenerateCreateScript)...");
            await ctx.Database.ExecuteSqlRawAsync(ctx.Database.GenerateCreateScript());
            return;
        }

        log.WriteLine($"  La base ya tiene {estado.TablasEnPublic} tablas: verificando que cubran el modelo...");
        var faltantes = await FaltantesDelModeloAsync(ctx);
        if (faltantes.Count > 0)
            throw new GuardaException(
                "El esquema de la base no coincide con el modelo de este código. Faltan:\n   - " +
                string.Join("\n   - ", faltantes.Take(30)) +
                (faltantes.Count > 30 ? $"\n   ... y {faltantes.Count - 30} más" : "") +
                "\nAplica los SQL pendientes en la rama de revisión o usa una base vacía (la herramienta crea el esquema).");
    }

    public static async Task<List<string>> FaltantesDelModeloAsync(AppDbContext ctx)
    {
        var existentes = new HashSet<string>(StringComparer.Ordinal);
        var cn = (NpgsqlConnection)ctx.Database.GetDbConnection();
        await using (var cmd = new NpgsqlCommand(
            "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public'", cn))
        {
            cmd.Transaction = (NpgsqlTransaction?)ctx.Database.CurrentTransaction?.GetDbTransaction();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) existentes.Add($"{r.GetString(0)}.{r.GetString(1)}");
        }

        var faltantes = new List<string>();
        foreach (var (tabla, columna) in ColumnasDelModelo(ctx.Model))
            if (!existentes.Contains($"{tabla}.{columna}"))
                faltantes.Add($"{tabla}.{columna}");
        return faltantes;
    }

    public static IEnumerable<(string Tabla, string Columna)> ColumnasDelModelo(IModel modelo)
    {
        foreach (var et in modelo.GetEntityTypes())
        {
            var tabla = et.GetTableName();
            if (tabla is null) continue;
            var objeto = StoreObjectIdentifier.Table(tabla, et.GetSchema());
            foreach (var p in et.GetProperties())
                if (p.GetColumnName(objeto) is { } col)
                    yield return (tabla, col);
        }
    }

    public static IEnumerable<string> TablasDelModelo(IModel modelo) =>
        modelo.GetEntityTypes().Select(e => e.GetTableName()).OfType<string>().Distinct(StringComparer.Ordinal);

    // ── Marcador y limpieza ──────────────────────────────────────

    public static async Task MarcarAsync(AppDbContext ctx)
    {
        await ctx.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS public._entorno (valor text NOT NULL)");
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM public._entorno");
        await ctx.Database.ExecuteSqlRawAsync("INSERT INTO public._entorno (valor) VALUES ('play_review')");
    }

    /// <summary>Vacía todas las tablas del modelo salvo las conservadas. Sin RESTART IDENTITY (no hace falta).</summary>
    public static async Task LimpiarAsync(AppDbContext ctx)
    {
        var tablas = TablasDelModelo(ctx.Model).Where(t => !TablasConservadas.Contains(t)).OrderBy(t => t).ToList();
        var lista  = string.Join(", ", tablas.Select(t => $"public.\"{t}\""));
        // Los nombres salen del modelo EF compilado, no de una entrada externa.
#pragma warning disable EF1002
        await ctx.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE {lista} CASCADE");
#pragma warning restore EF1002
    }

    // ── Carga ────────────────────────────────────────────────────

    public static async Task CargarAsync(AppDbContext ctx, DatosRevision d, IReadOnlyList<CuentaRevision> cuentas, TextWriter log)
    {
        ctx.ChangeTracker.AutoDetectChangesEnabled = false;

        // Roles: se reutilizan los existentes por nombre.
        var rolesExistentes = await ctx.Roles.AsNoTracking().ToListAsync();
        var mapaRoles = new Dictionary<Guid, Guid>();
        foreach (var rol in d.Roles)
        {
            var existente = rolesExistentes.FirstOrDefault(r => r.Nombre == rol.Nombre);
            if (existente is null) ctx.Roles.Add(rol);
            mapaRoles[rol.Id] = existente?.Id ?? rol.Id;
        }
        await GuardarAsync(ctx, "roles", log);

        await CargarUsuariosAsync(ctx, d, cuentas, mapaRoles, log);

        await Grupo(ctx, log, "configuración", c =>
        {
            c.SiteSettings.Add(d.Sitio);
            c.SchedulerSettings.Add(d.Programador);
            c.SupplySettings.Add(d.Abasto);
            c.WhatsAppSettings.Add(d.WhatsApp);
            c.ContenidoLegal.AddRange(d.ContenidoLegal);
        });
        await Grupo(ctx, log, "catálogo", c =>
        {
            c.Categories.AddRange(d.Categorias);
            c.Catalogos.AddRange(d.Catalogos);
            c.InventoryItems.AddRange(d.Insumos);
            c.CustomizationOptions.AddRange(d.Personalizaciones);
            c.Products.AddRange(d.Productos);
            c.ProductCategories.AddRange(d.ProductoCategorias);
            c.ProductCatalogos.AddRange(d.ProductoCatalogos);
            c.ProductCustomizationOptions.AddRange(d.ProductoPersonalizaciones);
            c.ProductRecipes.AddRange(d.Recetas);
        });
        await Grupo(ctx, log, "clientes", c =>
        {
            c.Customers.AddRange(d.Clientes);
            c.Addresses.AddRange(d.Direcciones);
        });
        await Grupo(ctx, log, "pedidos", c =>
        {
            c.Orders.AddRange(d.Pedidos);
            c.OrderItems.AddRange(d.Lineas);
            c.OrderItemCustomizations.AddRange(d.LineasPersonalizadas);
            c.Payments.AddRange(d.Pagos);
            c.Deliveries.AddRange(d.Entregas);
        });
        await Grupo(ctx, log, "inventario", c =>
        {
            c.InventoryMovements.AddRange(d.Movimientos);
            c.InventoryDailySnapshots.AddRange(d.Instantaneas);
            c.SupplyOrders.AddRange(d.OrdenesCompra);
            c.SupplyOrderItems.AddRange(d.LineasCompra);
            c.UncoveredDemands.AddRange(d.DemandaNoCubierta);
        });
        await Grupo(ctx, log, "caja y gastos", c =>
        {
            c.CashCuts.AddRange(d.Cortes);
            c.Expenses.AddRange(d.Gastos);
            c.ErrorReports.AddRange(d.Reportes);
        });
        await Grupo(ctx, log, "venta rápida, solicitudes y promociones", c =>
        {
            c.QuickSaleTemplates.AddRange(d.Plantillas);
            c.QuickSaleTemplateItems.AddRange(d.PlantillaItems);
            c.SolicitudesVentaInstantanea.AddRange(d.SolicitudesVenta);
            c.SolicitudesEvento.AddRange(d.SolicitudesEvento);
            c.Ofertas.AddRange(d.Ofertas);
            c.Descuentos.AddRange(d.Descuentos);
            c.Promotions.AddRange(d.Promociones);
        });
        await Grupo(ctx, log, "notificaciones, bitácora y segmentos", c =>
        {
            c.Notifications.AddRange(d.Notificaciones);
            c.AuditLogs.AddRange(d.Auditoria);
            c.CustomerSegments.AddRange(d.Segmentos);
        });

        // EF omite un bool=false cuando la columna tiene DEFAULT true; se corrige aquí.
        if (d.InsumosSinCosto.Count > 0)
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE public.inventory_items SET suma_al_costo = false WHERE id = ANY({0})", d.InsumosSinCosto.ToArray());
    }

    private static async Task CargarUsuariosAsync(AppDbContext ctx, DatosRevision d, IReadOnlyList<CuentaRevision> cuentas,
        Dictionary<Guid, Guid> mapaRoles, TextWriter log)
    {
        var ids = d.Usuarios.Select(u => u.Id).ToArray();

        // Fuera todo usuario que no sea del dataset (p. ej. los que crea el robot de Google
        // al registrarse). Sus tokens se borran en cascada.
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM public.users WHERE NOT (id = ANY({0}))", ids);
        await ctx.Database.ExecuteSqlRawAsync("UPDATE public.users SET es_responsable_turno = false");
        await ctx.Database.ExecuteSqlRawAsync("DELETE FROM public.user_roles WHERE user_id = ANY({0})", ids);

        var existentes = await ctx.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var passwordPorCorreo = cuentas.ToDictionary(c => c.Correo, c => c.Password, StringComparer.OrdinalIgnoreCase);
        // TEMPORAL-TESTERS: los testers de Google Play entran con la contraseña común.
        foreach (var correo in d.IdsTesters.Keys)
            passwordPorCorreo[correo] = CuentasTesters.Password;
        var revocar = new List<Guid>();

        foreach (var u in d.Usuarios)
        {
            var esRevision = d.IdsCuentasRevision.ContainsKey(u.Correo!) || d.IdsTesters.ContainsKey(u.Correo!);
            existentes.TryGetValue(u.Id, out var actual);

            string hash;
            if (esRevision)
            {
                var password = passwordPorCorreo[u.Correo!];
                if (actual?.PasswordHash is { } h && VerificaSinFallar(password, h)) hash = h;
                else
                {
                    hash = BCryptNet.HashPassword(password);
                    if (actual is not null) revocar.Add(u.Id);   // contraseña rotada: cierra sus sesiones
                }
            }
            else
            {
                // Personal y clientes ficticios: contraseña aleatoria que nadie conoce.
                hash = actual?.PasswordHash ?? BCryptNet.HashPassword(Convert.ToBase64String(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
            }

            if (actual is null)
            {
                u.PasswordHash = hash;
                ctx.Users.Add(u);
            }
            else
            {
                actual.Nombre             = u.Nombre;
                actual.Apellido           = u.Apellido;
                actual.Correo             = u.Correo;
                actual.Telefono           = u.Telefono;
                actual.PasswordHash       = hash;
                actual.EsCliente          = u.EsCliente;
                actual.Estado             = "ACTIVO";
                actual.CorreoVerificado   = true;
                actual.EsResponsableTurno = u.EsResponsableTurno;
                actual.ActualizadoEn      = u.ActualizadoEn;
                ctx.Entry(actual).State   = EntityState.Modified;
            }
        }

        foreach (var ur in d.UserRoles)
            ctx.UserRoles.Add(new UserRole { UserId = ur.UserId, RoleId = mapaRoles[ur.RoleId] });

        ctx.ChangeTracker.DetectChanges();
        await GuardarAsync(ctx, $"usuarios ({d.Usuarios.Count})", log);

        if (d.IdsTesters.Count > 0)
            log.WriteLine($"  Testers de Google Play: {d.IdsTesters.Count} cuentas (TEMPORAL-TESTERS).");

        if (revocar.Count > 0)
        {
            await ctx.Database.ExecuteSqlRawAsync(
                "UPDATE public.auth_tokens SET usado = true WHERE user_id = ANY({0})", revocar.ToArray());
            log.WriteLine($"  Contraseña cambiada en {revocar.Count} cuenta(s) de revisión o de testers: sesiones anteriores revocadas.");
        }
    }

    private static bool VerificaSinFallar(string password, string hash)
    {
        try { return BCryptNet.Verify(password, hash); }
        catch (Exception) { return false; }
    }

    private static async Task Grupo(AppDbContext ctx, TextWriter log, string nombre, Action<AppDbContext> agregar)
    {
        agregar(ctx);
        ctx.ChangeTracker.DetectChanges();
        await GuardarAsync(ctx, nombre, log);
    }

    private static async Task GuardarAsync(AppDbContext ctx, string nombre, TextWriter log)
    {
        var filas = await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        log.WriteLine($"  {nombre}: {filas} filas");
    }

    private static async Task<T> EscalarAsync<T>(NpgsqlConnection cn, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, cn);
        var v = await cmd.ExecuteScalarAsync();
        return (T)Convert.ChangeType(v!, typeof(T));
    }
}
