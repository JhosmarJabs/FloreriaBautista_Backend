using System.Text.Json;
using FloreriaBautista.Data;
using FloreriaBautista.PlayReviewSeed;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Herramienta tools/PlayReviewSeed: guardas (sin base real) y generador de datos
/// sintéticos (determinismo e integridad referencial en memoria).
/// </summary>
public class PlayReviewSiembraTests
{
    private const string Dominio = "@playreview.invalid";

    // Valores de prueba: no son las credenciales reales de revisión.
    private static readonly IReadOnlyList<CuentaRevision> Cuentas =
    [
        new("ADMIN", "Revisor", "Administrador", "revisor.admin@playreview.invalid", "contrasena-de-prueba-1"),
        new("EMPLEADO", "Revisor", "Empleado", "revisor.empleado@playreview.invalid", "contrasena-de-prueba-2"),
    ];

    // ── Guarda 1: REVIEW_DB_URL ──────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_Review_Db_Url_Aborta(string? valor)
    {
        Assert.Throws<GuardaException>(() => Guardas.ConexionDesdeVariable(valor));
    }

    [Fact]
    public void Url_De_Neon_Se_Convierte_En_Cadena_Npgsql()
    {
        var b = Guardas.ConexionDesdeVariable(
            "postgresql://floreria_review:p%40ss@ep-ejemplo-123-pooler.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require");

        Assert.Equal("ep-ejemplo-123-pooler.us-east-2.aws.neon.tech", b.Host);
        Assert.Equal("neondb", b.Database);
        Assert.Equal("floreria_review", b.Username);
        Assert.Equal("p@ss", b.Password);
        Assert.Equal(5432, b.Port);
        Assert.Equal(SslMode.Require, b.SslMode);
    }

    [Fact]
    public void Cadena_Npgsql_Tambien_Se_Acepta_Y_Exige_Host_Y_Base()
    {
        var b = Guardas.ConexionDesdeVariable("Host=localhost;Database=floreria_review;Username=u;Password=p");
        Assert.Equal("floreria_review", b.Database);
        Assert.Throws<GuardaException>(() => Guardas.ConexionDesdeVariable("Username=u;Password=p"));
    }

    // ── Guarda 2: host de producción ─────────────────────────────

    [Theory]
    [InlineData("ep-prod-1.us-east-2.aws.neon.tech", "ep-prod-1.us-east-2.aws.neon.tech")]
    [InlineData("ep-prod-1-pooler.us-east-2.aws.neon.tech", "ep-prod-1.us-east-2.aws.neon.tech")]
    [InlineData("EP-PROD-1.us-east-2.aws.neon.tech", "ep-prod-1-pooler.us-east-2.aws.neon.tech")]
    public void Destino_Igual_Al_Host_De_Produccion_Aborta(string destino, string prod)
    {
        Assert.Throws<GuardaException>(() => Guardas.HostDistintoDeProduccion(destino, prod));
    }

    [Fact]
    public void Destino_Distinto_O_Sin_Prod_Db_Host_Continua()
    {
        Guardas.HostDistintoDeProduccion("ep-review-9.us-east-2.aws.neon.tech", "ep-prod-1.us-east-2.aws.neon.tech");
        Guardas.HostDistintoDeProduccion("ep-review-9.us-east-2.aws.neon.tech", null);
    }

    // ── Guarda 3: estado de la base ──────────────────────────────

    [Fact]
    public void Base_Con_Usuarios_Y_Sin_Marcador_Aborta_Aunque_Pidan_Reiniciar()
    {
        var conDatos = new EstadoDestino(60, true, 12, 300, 5000, false, false);
        Assert.Throws<GuardaException>(() => Guardas.EstadoPermiteSembrar(conDatos, reiniciar: false));
        Assert.Throws<GuardaException>(() => Guardas.EstadoPermiteSembrar(conDatos, reiniciar: true));
    }

    [Fact]
    public void Marcador_Con_Otro_Valor_Aborta()
    {
        var otro = new EstadoDestino(60, true, 0, 0, 0, MarcadorExiste: true, MarcadorEsRevision: false);
        Assert.Throws<GuardaException>(() => Guardas.EstadoPermiteSembrar(otro, reiniciar: true));
    }

    [Fact]
    public void Base_Vacia_O_Rama_Schema_Only_Se_Puede_Sembrar()
    {
        Guardas.EstadoPermiteSembrar(EstadoDestino.Vacia, reiniciar: false);
        Guardas.EstadoPermiteSembrar(new EstadoDestino(60, true, 0, 0, 0, false, false), reiniciar: false);
    }

    [Fact]
    public void Base_De_Revision_Con_Datos_Exige_Reiniciar()
    {
        var revision = new EstadoDestino(61, true, 27, 151, 1600, true, true);
        Assert.Throws<GuardaException>(() => Guardas.EstadoPermiteSembrar(revision, reiniciar: false));
        Guardas.EstadoPermiteSembrar(revision, reiniciar: true);
    }

    // ── Guarda 4: confirmación ───────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("NEONDB")]
    [InlineData("neondb_prod")]
    public void Confirmacion_Distinta_Aborta(string? confirmacion)
    {
        Assert.Throws<GuardaException>(() => Guardas.ConfirmacionCoincide("neondb", confirmacion));
    }

    [Fact]
    public void Confirmacion_Exacta_Continua()
    {
        Guardas.ConfirmacionCoincide("neondb", " neondb ");
    }

    // ── Credenciales ─────────────────────────────────────────────

    private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement;

    [Fact]
    public void Credenciales_Validas_Se_Leen()
    {
        var cuentas = Credenciales.Desde(Json("""
            {"floreria":{"cuentas":[
              {"rol":"ADMIN","nombre":"A","apellido":"B","correo":"a@playreview.invalid","password":"123456789012"},
              {"rol":"EMPLEADO","nombre":"C","apellido":"D","correo":"C@PlayReview.invalid","password":"123456789012"}]}}
            """), Dominio);
        Assert.Equal(2, cuentas.Count);
        Assert.Equal("c@playreview.invalid", cuentas[1].Correo);
    }

    [Theory]
    [InlineData("""{"floreria":{}}""")]
    [InlineData("""{"floreria":{"cuentas":[{"rol":"ADMIN","nombre":"A","apellido":"B","correo":"a@gmail.com","password":"123456789012"},{"rol":"EMPLEADO","nombre":"C","apellido":"D","correo":"c@playreview.invalid","password":"123456789012"}]}}""")]
    [InlineData("""{"floreria":{"cuentas":[{"rol":"ADMIN","nombre":"A","apellido":"B","correo":"a@playreview.invalid","password":"corta"},{"rol":"EMPLEADO","nombre":"C","apellido":"D","correo":"c@playreview.invalid","password":"123456789012"}]}}""")]
    [InlineData("""{"floreria":{"cuentas":[{"rol":"CLIENTE","nombre":"A","apellido":"B","correo":"a@playreview.invalid","password":"123456789012"}]}}""")]
    public void Credenciales_Invalidas_Abortan(string json)
    {
        Assert.Throws<GuardaException>(() => Credenciales.Desde(Json(json), Dominio));
    }

    // ── Generador ────────────────────────────────────────────────

    private static readonly TimeZoneInfo Zona = GeneradorDatos.ZonaTienda();
    private static readonly DateTime Referencia = new(2026, 11, 7, 23, 30, 0, DateTimeKind.Utc); // 17:30 en CDMX

    private static DatosRevision Generar() => new GeneradorDatos(Cuentas, Referencia, Zona).Generar();

    [Fact]
    public void El_Generador_Es_Determinista()
    {
        var a = Generar();
        var b = Generar();

        Assert.Equal(a.Pedidos.Select(p => (p.Id, p.Total, p.EstadoPedido, p.FechaCreacion)),
                     b.Pedidos.Select(p => (p.Id, p.Total, p.EstadoPedido, p.FechaCreacion)));
        Assert.Equal(a.Pagos.Select(p => (p.Id, p.Monto, p.Metodo)), b.Pagos.Select(p => (p.Id, p.Monto, p.Metodo)));
        Assert.Equal(a.Clientes.Select(c => (c.Id, c.Nombre, c.Apellido)), b.Clientes.Select(c => (c.Id, c.Nombre, c.Apellido)));
        Assert.Equal(a.Insumos.Select(i => (i.Id, i.StockActual)), b.Insumos.Select(i => (i.Id, i.StockActual)));
    }

    [Fact]
    public void Volumenes_Y_Rangos_De_Fechas_Son_Los_Esperados()
    {
        var d = Generar();
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Referencia, Zona));

        Assert.InRange(d.Clientes.Count, 150, 152);
        Assert.InRange(d.Pedidos.Count, 1300, 1900);
        Assert.Contains(d.Pedidos, p => p.FechaEntrega > hoy && p.EstadoPedido is "PENDIENTE_VALIDACION" or "EN_PREPARACION");
        Assert.All(d.Pedidos, p => Assert.True(p.FechaEntrega >= hoy.AddDays(-120)));
        Assert.All(d.Pedidos, p => Assert.True(p.FechaCreacion <= Referencia));   // nada creado en el futuro
        Assert.All(d.Pedidos, p => Assert.Equal(d.Lineas.Where(l => l.OrderId == p.Id).Sum(l => l.Subtotal) + (p.CostoEnvio ?? 0), p.Total));
    }

    [Fact]
    public void Todos_Los_Ids_Son_Sinteticos_Salvo_Los_Roles_Canonicos()
    {
        var d = Generar();
        var ids = d.Usuarios.Select(x => x.Id).Concat(d.Clientes.Select(x => x.Id)).Concat(d.Pedidos.Select(x => x.Id))
            .Concat(d.Productos.Select(x => x.Id)).Concat(d.Insumos.Select(x => x.Id)).Concat(d.Pagos.Select(x => x.Id));
        Assert.All(ids, id => Assert.StartsWith("5eed", id.ToString()));
    }

    [Fact]
    public void Integridad_Referencial_Del_Dataset()
    {
        var d = Generar();
        var usuarios  = d.Usuarios.Select(u => u.Id).ToHashSet();
        var clientes  = d.Clientes.Select(c => c.Id).ToHashSet();
        var productos = d.Productos.Select(p => p.Id).ToHashSet();
        var insumos   = d.Insumos.Select(i => i.Id).ToHashSet();
        var pedidos   = d.Pedidos.Select(p => p.Id).ToHashSet();
        var lineas    = d.Lineas.Select(l => l.Id).ToHashSet();
        var cortes    = d.Cortes.Select(c => c.Id).ToHashSet();
        var opciones  = d.Personalizaciones.Select(o => o.Id).ToHashSet();

        Assert.All(d.UserRoles, ur => Assert.Contains(ur.UserId, usuarios));
        Assert.All(d.Clientes.Where(c => c.UserId.HasValue), c => Assert.Contains(c.UserId!.Value, usuarios));
        Assert.All(d.Direcciones, a => Assert.Contains(a.CustomerId, clientes));
        Assert.All(d.Pedidos, p => Assert.Contains(p.CustomerId, clientes));
        Assert.All(d.Pedidos.Where(p => p.AtendidoPorUsuarioId.HasValue), p => Assert.Contains(p.AtendidoPorUsuarioId!.Value, usuarios));
        Assert.All(d.Lineas, l => { Assert.Contains(l.OrderId, pedidos); Assert.Contains(l.ProductId, productos); });
        Assert.All(d.LineasPersonalizadas, l => { Assert.Contains(l.OrderItemId, lineas); Assert.Contains(l.CustomizationOptionId, opciones); });
        Assert.All(d.Pagos, p => Assert.Contains(p.OrderId, pedidos));
        Assert.All(d.Entregas, e => Assert.Contains(e.OrderId, pedidos));
        Assert.Equal(d.Entregas.Count, d.Entregas.Select(e => e.OrderId).Distinct().Count());   // 1:1
        Assert.All(d.Recetas, r => { Assert.Contains(r.ProductId, productos); Assert.Contains(r.InventoryItemId, insumos); });
        Assert.All(d.Movimientos, m => { Assert.Contains(m.InventoryItemId, insumos); Assert.Contains(m.UsuarioId, usuarios); });
        Assert.All(d.Gastos, g => { Assert.Contains(g.UsuarioId, usuarios); if (g.CashCutId is { } c) Assert.Contains(c, cortes); });
        Assert.All(d.Notificaciones, n => Assert.True(n.DestinatarioUsuarioId.HasValue ^ n.DestinatarioCustomerId.HasValue));
        Assert.All(d.Insumos, i => Assert.True(i.StockActual >= 0));

        // Un solo corte por empleado y día (índice único de cash_cuts).
        Assert.Equal(d.Cortes.Count, d.Cortes.Select(c => (c.UsuarioId, c.FechaLocal)).Distinct().Count());
        // Un solo responsable de turno (índice único parcial de users).
        Assert.Single(d.Usuarios, u => u.EsResponsableTurno);
        // Correos de usuario únicos.
        Assert.Equal(d.Usuarios.Count, d.Usuarios.Select(u => u.Correo).Distinct().Count());
    }

    [Fact]
    public void El_Empleado_De_Revision_Tiene_Su_Dia_Con_Datos_Y_El_Corte_De_Hoy_Abierto()
    {
        var d = Generar();
        var empleado = d.IdsCuentasRevision["revisor.empleado@playreview.invalid"];
        var hoyLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Referencia, Zona));
        var inicio = TimeZoneInfo.ConvertTimeToUtc(hoyLocal.ToDateTime(TimeOnly.MinValue), Zona);

        Assert.True(d.Pedidos.Count(p => p.AtendidoPorUsuarioId == empleado && p.FechaCreacion >= inicio) >= 7);
        Assert.Contains(d.Pagos, p => p.RegistradoPorUsuarioId == empleado && p.Metodo == "EFECTIVO" && p.FechaPago >= inicio);
        Assert.Contains(d.Gastos, g => g.UsuarioId == empleado && g.FechaHora >= inicio && g.CashCutId == null);
        Assert.DoesNotContain(d.Cortes, c => c.UsuarioId == empleado && c.FechaLocal == hoyLocal);
        Assert.Contains(d.Cortes, c => c.UsuarioId == empleado);
        Assert.True(d.Usuarios.Single(u => u.Id == empleado).EsResponsableTurno);
        Assert.Contains(d.Plantillas, t => t.Activa);
        Assert.Contains(d.Insumos, i => i.StockActual < i.StockMinimo);   // alertas de stock bajo visibles
    }

    [Fact]
    public void Las_Cuentas_Del_Dataset_Vienen_Del_Json_Y_El_Resto_Es_Ficticio()
    {
        var d = Generar();
        Assert.Equal(2, d.IdsCuentasRevision.Count);
        Assert.All(d.Usuarios, u => Assert.EndsWith(Dominio, u.Correo));
        Assert.All(d.Usuarios, u => Assert.Null(u.PasswordHash));   // el cargador pone el hash; el generador nunca ve contraseñas en claro
    }

    // ── Testers de Google Play (TEMPORAL-TESTERS) ────────────────

    private static DatosRevision GenerarConTesters() =>
        new GeneradorDatos(Cuentas, Referencia, Zona, testers: CuentasTesters.Todas).Generar();

    [Fact]
    public void Lista_De_Testers_Es_La_Pedida()
    {
        var t = CuentasTesters.Todas;
        Assert.Equal(15, t.Count);
        Assert.Single(t, x => x.Rol == "ADMIN");
        Assert.Equal("gadiel.admin@test.com", t.Single(x => x.Rol == "ADMIN").Correo);
        Assert.All(t, x => Assert.EndsWith("@test.com", x.Correo));
        Assert.Equal(t.Count, t.Select(x => x.Correo).Distinct().Count());
        Assert.Contains(t, x => x is { Nombre: "Liz", Apellido: "Prueba", Correo: "liz.prueba@test.com", Rol: "EMPLEADO" });
        Assert.Contains(t, x => x is { Nombre: "Gadiel", Apellido: "Admin" });
        Assert.Contains(t, x => x is { Nombre: "Gadiel", Apellido: "Empleado", Rol: "EMPLEADO" });
        foreach (var u in new[] { "liz", "angelito", "abi", "alan", "sabino", "domi", "diana", "leo", "jordan",
                                  "magdiel", "luz", "javier", "paco" })
            Assert.Contains(t, x => x.Correo == $"{u}.prueba@test.com" && x.Rol == "EMPLEADO");
        // El borde reconoce a todos con la lista de PROD.
        var dominios = FloreriaBautista.PlayReview.PlayReviewEntorno.ParsearDominiosLogin("@playreview.invalid,@test.com")!;
        Assert.All(t, x => Assert.True(FloreriaBautista.PlayReview.PlayReviewEntorno.CorreoEsDeRevision(x.Correo, dominios)));
    }

    [Fact]
    public void Con_Testers_El_Dataset_Los_Incluye_Activos_Con_Su_Rol_E_Ids_Fijos()
    {
        var d = GenerarConTesters();

        Assert.Equal(15, d.IdsTesters.Count);
        for (var i = 0; i < CuentasTesters.Todas.Count; i++)
        {
            var t = CuentasTesters.Todas[i];
            var u = d.Usuarios.Single(x => x.Correo == t.Correo);
            Assert.Equal(CuentasTesters.Id(i), u.Id);
            Assert.StartsWith("5eed0101-", u.Id.ToString());
            Assert.Equal("ACTIVO", u.Estado);
            Assert.True(u.CorreoVerificado);
            Assert.False(u.EsCliente);
            Assert.False(u.EsResponsableTurno);
            Assert.Null(u.PasswordHash);   // el hash lo pone el cargador
            var rol = Assert.Single(d.UserRoles, ur => ur.UserId == u.Id).RoleId;
            Assert.Equal(t.Rol == "ADMIN" ? GeneradorDatos.RolAdmin : GeneradorDatos.RolEmpleado, rol);
        }

        // Siguen las reglas del dataset: un solo responsable de turno y correos únicos.
        Assert.Single(d.Usuarios, u => u.EsResponsableTurno);
        Assert.Equal(d.Usuarios.Count, d.Usuarios.Select(u => u.Correo).Distinct().Count());
        Assert.Equal(d.Usuarios.Count, d.Usuarios.Select(u => u.Id).Distinct().Count());
        Assert.Equal(2, d.IdsCuentasRevision.Count);
    }

    [Fact]
    public void Con_Testers_Hay_Existencias_De_Sobra_Para_Vender_Y_El_Resto_No_Cambia()
    {
        var sin = Generar();
        var con = GenerarConTesters();

        // Mismo catálogo, clientes y pedidos: los testers solo agregan usuarios y reabasto.
        Assert.Equal(sin.Pedidos.Select(p => (p.Id, p.Total)), con.Pedidos.Select(p => (p.Id, p.Total)));
        Assert.Equal(sin.Productos.Select(p => p.Id), con.Productos.Select(p => p.Id));

        foreach (var ins in con.Insumos)
        {
            var antes = sin.Insumos.Single(i => i.Id == ins.Id).StockActual;
            if (GeneradorDatos.InsumosSinReabastoTesters.Contains(ins.Nombre))
                Assert.Equal(antes, ins.StockActual);
            else
                Assert.True(ins.StockActual >= antes + 500, $"{ins.Nombre}: {ins.StockActual}");
        }
        Assert.Contains(con.Insumos, i => i.StockActual < i.StockMinimo);   // la alerta de stock bajo sigue visible

        // Todo producto de venta instantánea alcanza para muchas ventas (15 testers x 14 días).
        var stock = con.Insumos.ToDictionary(i => i.Id, i => i.StockActual);
        foreach (var p in con.Productos.Where(p => p.PermiteVentaInstantanea))
        {
            var receta = con.Recetas.Where(r => r.ProductId == p.Id).ToList();
            Assert.NotEmpty(receta);
            Assert.All(receta, r => Assert.True(stock[r.InventoryItemId] / r.CantidadRequerida >= 40, p.Nombre));
        }

        // Productos, categorías, plantillas y configuración para vender existen.
        Assert.Contains(con.Productos, p => p.Activo && p.Estado == "ACTIVO" && p.PermiteVentaInstantanea);
        Assert.NotEmpty(con.Categorias);
        Assert.NotNull(con.Sitio);
        Assert.Contains(con.Plantillas, t => t.Activa);
    }

    [Fact]
    public void Sin_Testers_El_Dataset_No_Tiene_Cuentas_De_Testers()
    {
        var d = Generar();
        Assert.Empty(d.IdsTesters);
        Assert.DoesNotContain(d.Usuarios, u => u.Correo!.EndsWith("@test.com"));
    }

    [Fact]
    public void Credenciales_Aceptan_Cualquiera_De_Varios_Dominios()
    {
        var cuentas = Credenciales.Desde(Json("""
            {"floreria":{"cuentas":[
              {"rol":"ADMIN","nombre":"A","apellido":"B","correo":"a@playreview.invalid","password":"123456789012"},
              {"rol":"EMPLEADO","nombre":"C","apellido":"D","correo":"c@test.com","password":"123456789012"}]}}
            """), ["@playreview.invalid", "@test.com"]);
        Assert.Equal(2, cuentas.Count);
    }

    [Fact]
    public void Argumento_Sin_Testers_Se_Lee()
    {
        Assert.True(Argumentos.Parsear(["--credenciales", "x.json", "--sin-testers"]).SinTesters);
        Assert.False(Argumentos.Parsear(["--credenciales", "x.json"]).SinTesters);
    }

    /// <summary>
    /// docs/testers-cuentas.sql trae los mismos correos e ids que la siembra, y sus hashes
    /// BCrypt validan "prueba1234" (no son inventados).
    /// </summary>
    [Fact]
    public void El_Sql_De_Testers_Coincide_Con_La_Siembra()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "testers-cuentas.sql"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var sql = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "testers-cuentas.sql"));

        var filas = System.Text.RegularExpressions.Regex.Matches(sql,
            @"\('(?<id>[0-9a-f-]{36})', '(?<nombre>[^']+)', '(?<apellido>[^']+)', '(?<correo>[^']+)', '[0-9]+', '(?<hash>\$2[aby]\$\d\d\$[./A-Za-z0-9]{53})', '(?<rol>ADMIN|EMPLEADO)'\)");
        Assert.Equal(CuentasTesters.Todas.Count, filas.Count);

        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i].Groups;
            var t = CuentasTesters.Todas[i];
            Assert.Equal(CuentasTesters.Id(i).ToString(), f["id"].Value);
            Assert.Equal(t.Correo, f["correo"].Value);
            Assert.Equal(t.Nombre, f["nombre"].Value);
            Assert.Equal(t.Apellido, f["apellido"].Value);
            Assert.Equal(t.Rol, f["rol"].Value);
            Assert.True(BCrypt.Net.BCrypt.Verify(CuentasTesters.Password, f["hash"].Value), t.Correo);
        }
    }

    // ── Esquema ──────────────────────────────────────────────────

    [Fact]
    public void El_Modelo_Genera_El_Script_De_Creacion_Sin_Conectarse()
    {
        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=no_existe")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var ctx = new AppDbContext(opciones);

        var script = ctx.Database.GenerateCreateScript();
        Assert.Contains("CREATE TABLE public.users", script);
        Assert.Contains("CREATE TABLE public.orders", script);

        var tablas = BaseRevision.TablasDelModelo(ctx.Model).ToList();
        Assert.Contains("users", tablas);
        Assert.Contains("auth_tokens", tablas);
        // Lo que se conserva al reiniciar existe en el modelo; todo lo demás se vacía.
        Assert.All(BaseRevision.TablasConservadas, t => Assert.Contains(t, tablas));
        Assert.Contains(BaseRevision.ColumnasDelModelo(ctx.Model), c => c == ("users", "es_responsable_turno"));
    }
}
