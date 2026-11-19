using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Cms;

namespace FloreriaBautista.PlayReviewSeed;

/// <summary>
/// Todo el dataset sintético en memoria. Ningún dato sale de producción: catálogo,
/// clientes, pedidos y montos se inventan aquí con una semilla fija.
/// </summary>
public sealed class DatosRevision
{
    public List<Role>     Roles     { get; } = [];
    public List<User>     Usuarios  { get; } = [];
    public List<UserRole> UserRoles { get; } = [];

    public List<Customer> Clientes    { get; } = [];
    public List<Address>  Direcciones { get; } = [];

    public List<Category>                   Categorias            { get; } = [];
    public List<Catalogo>                   Catalogos             { get; } = [];
    public List<Product>                    Productos             { get; } = [];
    public List<ProductCategory>            ProductoCategorias    { get; } = [];
    public List<ProductCatalogo>            ProductoCatalogos     { get; } = [];
    public List<CustomizationOption>        Personalizaciones     { get; } = [];
    public List<ProductCustomizationOption> ProductoPersonalizaciones { get; } = [];
    public List<InventoryItem>              Insumos               { get; } = [];
    public List<ProductRecipe>              Recetas               { get; } = [];

    public List<Order>                  Pedidos                 { get; } = [];
    public List<OrderItem>              Lineas                  { get; } = [];
    public List<OrderItemCustomization> LineasPersonalizadas    { get; } = [];
    public List<Payment>                Pagos                   { get; } = [];
    public List<Delivery>               Entregas                { get; } = [];

    public List<InventoryMovement>      Movimientos { get; } = [];
    public List<InventoryDailySnapshot> Instantaneas { get; } = [];
    public List<SupplyOrder>            OrdenesCompra { get; } = [];
    public List<SupplyOrderItem>        LineasCompra  { get; } = [];
    public List<UncoveredDemand>        DemandaNoCubierta { get; } = [];

    public List<Expense>     Gastos   { get; } = [];
    public List<CashCut>     Cortes   { get; } = [];
    public List<ErrorReport> Reportes { get; } = [];

    public List<QuickSaleTemplate>         Plantillas      { get; } = [];
    public List<QuickSaleTemplateItem>     PlantillaItems  { get; } = [];
    public List<SolicitudVentaInstantanea> SolicitudesVenta { get; } = [];
    public List<SolicitudEvento>           SolicitudesEvento { get; } = [];

    public List<Oferta>    Ofertas    { get; } = [];
    public List<Descuento> Descuentos { get; } = [];
    public List<Promotion> Promociones { get; } = [];

    public List<Notification>    Notificaciones { get; } = [];
    public List<AuditLog>        Auditoria      { get; } = [];
    public List<CustomerSegment> Segmentos      { get; } = [];
    public List<ContenidoLegal>  ContenidoLegal { get; } = [];

    public SiteSettings      Sitio       { get; set; } = null!;
    public SchedulerSettings Programador { get; set; } = null!;
    public SupplySettings    Abasto      { get; set; } = null!;
    public WhatsAppSettings  WhatsApp    { get; set; } = null!;

    /// <summary>
    /// Insumos que NO suman al costo. La columna tiene DEFAULT true y EF omite el valor
    /// false (es el default de CLR), así que el cargador los corrige con un UPDATE.
    /// </summary>
    public List<Guid> InsumosSinCosto { get; } = [];

    /// <summary>Id del usuario de cada cuenta de revisión (por correo).</summary>
    public Dictionary<string, Guid> IdsCuentasRevision { get; } = new(StringComparer.OrdinalIgnoreCase);

    // TEMPORAL-TESTERS: eliminar cuando Google Play apruebe la app para producción.
    /// <summary>Id del usuario de cada tester de Google Play (por correo). Vacío si se siembra sin testers.</summary>
    public Dictionary<string, Guid> IdsTesters { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Genera el dataset de revisión. Es determinista: mismo (semilla, instante de
/// referencia, cuentas) ⇒ mismos registros, mismos ids y mismos montos. Todos los ids
/// empiezan con <c>5eed</c>, así se reconoce a simple vista un dato sintético.
/// Las fechas son relativas a "hoy" en la zona de la tienda, para que los tableros y el
/// día del empleado nunca se vean vacíos.
/// </summary>
public sealed class GeneradorDatos
{
    public const int SemillaPorDefecto = 20261107;

    // Ids canónicos de los roles (los mismos de BaseDatos/03_floreria_seeds_pruebas.sql).
    public static readonly Guid RolAdmin    = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    public static readonly Guid RolEmpleado = Guid.Parse("a0000001-0000-0000-0000-000000000002");
    public static readonly Guid RolCliente  = Guid.Parse("a0000001-0000-0000-0000-000000000005");

    private const string DominioFicticio = "@playreview.invalid";

    private readonly Random                       _rnd;
    private readonly DateTime                     _ahoraUtc;
    private readonly TimeZoneInfo                 _zona;
    private readonly DateOnly                     _hoy;
    private readonly DateTime                     _ahoraLocal;
    private readonly IReadOnlyList<CuentaRevision> _cuentas;
    private readonly IReadOnlyList<CuentaTester>   _testers;   // TEMPORAL-TESTERS
    private readonly DatosRevision                _d = new();
    private readonly Dictionary<int, int>         _contadores = new();

    // Actores
    private Guid _admin, _empleado, _empleado2, _empleado3;
    private Customer _mostrador = null!;
    private readonly List<Customer> _clientesFisicos = [];
    private readonly List<Customer> _clientesSesion  = [];

    // Catálogo
    private readonly Dictionary<string, InventoryItem> _insumo = new();
    private readonly List<(Product P, int Peso)>       _vendibles = [];
    private readonly Dictionary<Guid, List<ProductRecipe>> _recetaDe = new();
    private CustomizationOption _optMensaje = null!, _optColor = null!, _optChocolates = null!;

    public GeneradorDatos(IReadOnlyList<CuentaRevision> cuentas, DateTime ahoraUtc, TimeZoneInfo zona,
        int semilla = SemillaPorDefecto, IReadOnlyList<CuentaTester>? testers = null)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("ahoraUtc debe ser UTC.", nameof(ahoraUtc));
        _cuentas    = cuentas;
        _testers    = testers ?? [];
        _rnd        = new Random(semilla);
        _ahoraUtc   = ahoraUtc;
        _zona       = zona;
        _ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona);
        _hoy        = DateOnly.FromDateTime(_ahoraLocal);
    }

    public static TimeZoneInfo ZonaTienda()
    {
        var id = Environment.GetEnvironmentVariable("STORE_TIMEZONE");
        foreach (var candidato in new[] { id, "America/Mexico_City", "Central Standard Time (Mexico)" })
        {
            if (string.IsNullOrWhiteSpace(candidato)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(candidato); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    public DatosRevision Generar()
    {
        Roles();
        Usuarios();
        Configuracion();
        Catalogo();
        Clientes();
        Pedidos();
        Inventario();
        CajaYGastos();
        Abastecimiento();
        VentasInstantaneasYEventos();
        Promociones();
        NotificacionesYAuditoria();
        Segmentos();
        return _d;
    }

    // ── Utilidades ───────────────────────────────────────────────

    /// <summary>Id determinista: 5eed{tipo}-0000-4000-8000-{n}.</summary>
    public static Guid IdSintetico(int tipo, int n) => Guid.Parse($"5eed{tipo:x4}-0000-4000-8000-{n:x12}");

    private Guid NuevoId(int tipo)
    {
        _contadores.TryGetValue(tipo, out var n);
        _contadores[tipo] = ++n;
        return IdSintetico(tipo, n);
    }

    private T Elegir<T>(IReadOnlyList<T> lista) => lista[_rnd.Next(lista.Count)];
    private bool Prob(double p) => _rnd.NextDouble() < p;
    private decimal Monto(decimal min, decimal max) =>
        Math.Round(min + (decimal)_rnd.NextDouble() * (max - min), 0);

    private DateTime Utc(DateOnly dia, int hora, int minuto = 0)
    {
        var local = new DateTime(dia.Year, dia.Month, dia.Day, hora, minuto, 0, DateTimeKind.Unspecified);
        if (_zona.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, _zona);
    }

    private DateTime Utc(DateOnly dia, TimeOnly hora) => Utc(dia, hora.Hour, hora.Minute);

    /// <summary>Instante de hoy que ya pasó: reparte entre las 9:00 y "hace 5 minutos".</summary>
    private DateTime MomentoDeHoy(int indice, int total)
    {
        var inicio = Utc(_hoy, 9);
        var fin    = _ahoraUtc.AddMinutes(-5);
        if (fin <= inicio) { inicio = Utc(_hoy, 0); fin = _ahoraUtc.AddMinutes(-1); }
        if (fin <= inicio) fin = inicio.AddMinutes(1);
        var paso = (fin - inicio).Ticks / Math.Max(1, total + 1);
        return new DateTime(inicio.Ticks + paso * (indice + 1), DateTimeKind.Utc);
    }

    private DateTime MomentoDelDia(DateOnly dia) =>
        dia == _hoy ? MomentoDeHoy(_rnd.Next(20), 20) : Utc(dia, 9 + _rnd.Next(10), _rnd.Next(60));

    // ── Roles y usuarios ─────────────────────────────────────────

    private void Roles()
    {
        _d.Roles.Add(new Role { Id = RolAdmin,    Nombre = "ADMIN",    Descripcion = "Propietario. Control total." });
        _d.Roles.Add(new Role { Id = RolEmpleado, Nombre = "EMPLEADO", Descripcion = "Empleado de mostrador y operación." });
        _d.Roles.Add(new Role { Id = RolCliente,  Nombre = "CLIENTE",  Descripcion = "Cliente de la tienda web." });
    }

    private User NuevoUsuario(int n, string nombre, string apellido, string correo, Guid rol, bool responsable = false)
    {
        var u = new User
        {
            Id                 = IdSintetico(0x0100, n),
            Nombre             = nombre,
            Apellido           = apellido,
            Correo             = correo.ToLowerInvariant(),
            Telefono           = $"5550100{n:000}",
            Estado             = "ACTIVO",
            CorreoVerificado   = true,
            EsCliente          = rol == RolCliente,
            EsResponsableTurno = responsable,
            CreadoEn           = Utc(_hoy.AddDays(-200), 10),
            ActualizadoEn      = Utc(_hoy.AddDays(-200), 10),
        };
        _d.Usuarios.Add(u);
        _d.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = rol });
        return u;
    }

    private void Usuarios()
    {
        var admin    = _cuentas.Single(c => c.Rol == "ADMIN");
        var empleado = _cuentas.Single(c => c.Rol == "EMPLEADO");

        // La cuenta EMPLEADO de revisión es la responsable de turno: así ve y decide las
        // ventas instantáneas y ningún módulo del móvil queda restringido.
        _admin     = NuevoUsuario(1, admin.Nombre, admin.Apellido, admin.Correo, RolAdmin).Id;
        _empleado  = NuevoUsuario(2, empleado.Nombre, empleado.Apellido, empleado.Correo, RolEmpleado, responsable: true).Id;
        _d.IdsCuentasRevision[admin.Correo]    = _admin;
        _d.IdsCuentasRevision[empleado.Correo] = _empleado;

        // Personal ficticio (no puede iniciar sesión: el cargador le pone una contraseña aleatoria).
        _empleado2 = NuevoUsuario(3, "Lucía",  "Hernández Ficticia", "lucia.empleada" + DominioFicticio, RolEmpleado).Id;
        _empleado3 = NuevoUsuario(4, "Mateo",  "Cruz Ficticio",      "mateo.repartidor" + DominioFicticio, RolEmpleado).Id;

        Testers();
    }

    // TEMPORAL-TESTERS: eliminar cuando Google Play apruebe la app para producción.
    /// <summary>
    /// Testers de la prueba cerrada de Google Play: cuentas reales para iniciar sesión
    /// (el cargador les pone la contraseña común). Ids fijos por posición, así una
    /// resiembra conserva el mismo usuario. Ninguno es responsable de turno (solo puede
    /// haber uno y es la cuenta EMPLEADO de revisión).
    /// </summary>
    private void Testers()
    {
        for (var i = 0; i < _testers.Count; i++)
        {
            var t = _testers[i];
            if (_d.Usuarios.Any(u => string.Equals(u.Correo, t.Correo, StringComparison.OrdinalIgnoreCase)))
                throw new GuardaException($"El tester {t.Correo} choca con otra cuenta del dataset.");

            var u = new User
            {
                Id                 = CuentasTesters.Id(i),
                Nombre             = t.Nombre,
                Apellido           = t.Apellido,
                Correo             = t.Correo.ToLowerInvariant(),
                Telefono           = $"5550200{i + 1:000}",
                Estado             = "ACTIVO",
                CorreoVerificado   = true,
                EsCliente          = false,
                EsResponsableTurno = false,
                CreadoEn           = Utc(_hoy.AddDays(-1), 10),
                ActualizadoEn      = Utc(_hoy.AddDays(-1), 10),
            };
            _d.Usuarios.Add(u);
            _d.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = t.Rol == "ADMIN" ? RolAdmin : RolEmpleado });
            _d.IdsTesters[u.Correo] = u.Id;
        }
    }

    // ── Configuración ────────────────────────────────────────────

    private void Configuracion()
    {
        _d.Sitio = new SiteSettings
        {
            Id               = 1,
            NombreTienda     = "Florería Bautista",
            Telefono         = "5550100000",
            Correo           = "contacto" + DominioFicticio,
            Direccion        = "Av. de las Flores 100, Col. Centro (dirección de demostración)",
            DireccionUrl     = string.Empty,
            RazonSocial      = "Florería de Demostración S.A. de C.V.",
            Rfc              = "XAXX010101000",
            CorreoPrivacidad = "privacidad" + DominioFicticio,
            FacebookUrl      = string.Empty,
            InstagramUrl     = string.Empty,
            WhatsappUrl      = string.Empty,
            BannerTitulo     = "Flores para cada momento",
            BannerSubtitulo  = "Entorno de demostración con datos ficticios",
            BannerCta        = "Ver catálogo",
            HorariosJson     = "[{\"dia\":\"Lunes a sábado\",\"horario\":\"9:00 a 20:00\"},{\"dia\":\"Domingo\",\"horario\":\"10:00 a 15:00\"}]",
            DestacadosJson   = "[]",
            AnuncioTexto     = string.Empty,
            AnuncioActivo    = false,
            ActualizadoEn    = Utc(_hoy.AddDays(-30), 9),
        };

        // Respaldos y mantenimiento apagados: en revisión no hay respaldos programados.
        _d.Programador = new SchedulerSettings
        {
            Id = 1, BackupAutomaticoActivo = false, MantenimientoActivo = false,
            Frecuencia = "SEMANAL", DiaSemana = 0, Hora = 2,
            FrecuenciaMantenimiento = "SEMANAL", DiaSemanaMantenimiento = 0, HoraMantenimiento = 3,
            ActualizadoEn = Utc(_hoy.AddDays(-30), 9),
        };

        _d.Abasto = new SupplySettings { Id = 1, ActualizadoEn = Utc(_hoy.AddDays(-30), 9) };

        // WhatsApp deshabilitado y desconectado (además el notificador de revisión es no-op).
        _d.WhatsApp = new WhatsAppSettings
        {
            Id = 1, Habilitado = false, EstadoConexion = "DESCONECTADO",
            ActualizadoEn = Utc(_hoy.AddDays(-30), 9),
        };

        foreach (var slug in ContenidoLegalPorDefecto.Slugs)
            _d.ContenidoLegal.Add(ContenidoLegalPorDefecto.Crear(slug));
    }

    // ── Catálogo ─────────────────────────────────────────────────

    private void Catalogo()
    {
        var categorias = new Dictionary<string, Category>();
        foreach (var (nombre, desc) in new[]
        {
            ("Ramos", "Ramos envueltos para regalar"),
            ("Arreglos", "Arreglos en base, caja o florero"),
            ("Cumpleaños", "Detalles para cumpleaños"),
            ("Aniversario y amor", "Rosas y arreglos románticos"),
            ("Condolencias", "Coronas y arreglos de condolencia"),
            ("Plantas", "Plantas en maceta"),
            ("Complementos", "Chocolates, globos y peluches"),
        })
        {
            var c = new Category
            {
                Id = NuevoId(0x0200), Nombre = nombre, Descripcion = desc, Estado = "ACTIVA", Activo = true,
                CreadoEn = Utc(_hoy.AddDays(-300), 9), ActualizadoEn = Utc(_hoy.AddDays(-300), 9),
            };
            categorias[nombre] = c;
            _d.Categorias.Add(c);
        }

        var catalogos = new Dictionary<string, Catalogo>();
        foreach (var (nombre, ini, fin) in new (string, string?, string?)[]
        {
            ("Todo el año", null, null),
            ("San Valentín", "02-07", "02-14"),
            ("Día de las Madres", "05-01", "05-10"),
            ("Día de Muertos", "10-25", "11-02"),
            ("Navidad", "12-01", "12-24"),
        })
        {
            var c = new Catalogo
            {
                Id = NuevoId(0x0201), Nombre = nombre, Descripcion = $"Colección {nombre}", Estado = "ACTIVA",
                Activo = true, MesDiaInicio = ini, MesDiaFin = fin,
                CreadoEn = Utc(_hoy.AddDays(-300), 9), ActualizadoEn = Utc(_hoy.AddDays(-300), 9),
            };
            catalogos[nombre] = c;
            _d.Catalogos.Add(c);
        }

        // Insumos: nombre, unidad, costo, mínimo, ¿flor primaria?, ¿suma al costo?, vida útil, unidad de compra, rendimiento
        foreach (var (nombre, unidad, costo, minimo, primaria, sumaCosto, vida, compra, rend) in
            new (string, string, decimal, int, bool, bool, int?, string?, decimal)[]
        {
            ("Rosa roja",            "tallo",  16m,  80, true,  true,  7,    "paquete de 25", 24m),
            ("Rosa blanca",          "tallo",  16m,  50, true,  true,  7,    "paquete de 25", 24m),
            ("Rosa rosa",            "tallo",  16m,  40, true,  true,  7,    "paquete de 25", 24m),
            ("Girasol",              "tallo",  20m,  30, true,  true,  6,    "paquete de 10", 10m),
            ("Gerbera",              "tallo",  11m,  30, true,  true,  6,    "paquete de 10", 10m),
            ("Lilium",               "tallo",  32m,  20, true,  true,  8,    "paquete de 5",  5m),
            ("Clavel",               "tallo",   7m,  40, true,  true,  10,   "paquete de 25", 25m),
            ("Tulipán",              "tallo",  38m,  15, true,  true,  6,    "paquete de 10", 10m),
            ("Alstroemeria",         "tallo",   9m,  30, true,  true,  10,   "paquete de 10", 10m),
            ("Crisantemo blanco",    "tallo",   8m,  40, true,  true,  12,   "paquete de 20", 20m),
            ("Nube (gypsophila)",    "manojo", 42m,  10, false, true,  10,   null,            1m),
            ("Follaje de eucalipto", "vara",    5m,  60, false, false, 12,   "rollo",         30m),
            ("Helecho cuero",        "vara",    4m,  60, false, false, 12,   "rollo",         40m),
            ("Espuma floral",        "bloque", 24m,  15, false, false, null, null,            1m),
            ("Papel coreano",        "hoja",    8m,  50, false, false, null, "paquete de 20", 20m),
            ("Listón satinado",      "metro",   3m, 100, false, false, null, "rollo de 50 m", 50m),
            ("Florero de vidrio",    "pieza",  60m,   8, false, true,  null, null,            1m),
            ("Caja kraft",           "pieza",  42m,   8, false, true,  null, null,            1m),
            ("Canasta de mimbre",    "pieza",  75m,   5, false, true,  null, null,            1m),
            ("Base para corona",     "pieza",  85m,   4, false, true,  null, null,            1m),
            ("Globo metálico",       "pieza",  32m,  10, false, true,  null, null,            1m),
            ("Chocolates (caja de 3)", "caja", 45m,  10, false, true,  null, null,            1m),
            ("Peluche pequeño",      "pieza", 110m,   4, false, true,  null, null,            1m),
            ("Tarjeta de dedicatoria", "pieza", 4m,  50, false, false, null, "paquete de 50", 50m),
            ("Orquídea en maceta",   "pieza", 170m,   3, false, true,  null, null,            1m),
        })
        {
            var i = new InventoryItem
            {
                Id = NuevoId(0x0300), Nombre = nombre, UnidadMedida = unidad, PrecioCosto = costo,
                StockMinimo = minimo, StockActual = 0, Sucursal = "Sucursal Principal",
                EsFlorPrimaria = primaria, SumaAlCosto = true, Activo = true, VidaUtilDias = vida,
                UnidadCompra = compra, RendimientoEsperado = rend,
                PrecioUnidadCompra = compra is null ? null : costo * rend,
                FactorMermaUso = primaria ? 0.05m : 0m,
                ActualizadoEn = _ahoraUtc,
            };
            if (!sumaCosto) _d.InsumosSinCosto.Add(i.Id);
            _insumo[nombre] = i;
            _d.Insumos.Add(i);
        }

        _optColor = Personalizacion("color_flores", "Color de flores", "LISTA",
            "[\"Rojo\",\"Rosa\",\"Blanco\",\"Amarillo\",\"Mixto\"]", 0m);
        _optMensaje = Personalizacion("mensaje_tarjeta", "Mensaje en tarjeta", "TEXTO", null, 0m);
        _optChocolates = Personalizacion("chocolates", "Agregar chocolates", "BOOLEANO", "[\"Sí\",\"No\"]", 85m);
        Personalizacion("globo", "Globo metálico", "LISTA", "[\"1 globo\",\"3 globos\"]", 45m);
        Personalizacion("tamano", "Tamaño del arreglo", "LISTA", "[\"Mediano\",\"Grande\"]", 0m);

        // Productos: nombre, precio, tipo, categorías, catálogos, receta, ¿personalizable?, ¿venta instantánea?, peso de venta
        void P(string nombre, decimal precio, string tipo, string[] cats, string[] cols,
               (string Insumo, int Cant)[] receta, bool personalizable, bool instantanea, int peso,
               string visibilidad = "AMBOS")
        {
            var p = new Product
            {
                Id = NuevoId(0x0400), Nombre = nombre,
                Descripcion = $"{nombre}. Producto de demostración con datos ficticios.",
                PrecioBase = precio, Tipo = tipo, EsPersonalizable = personalizable, Estado = "ACTIVO",
                Visibilidad = visibilidad, Activo = true, EsReal = true,
                PermiteVentaInstantanea = instantanea, LimiteVentaInstantanea = instantanea ? 5 : null,
                CreadoEn = Utc(_hoy.AddDays(-250), 9), ActualizadoEn = Utc(_hoy.AddDays(-20), 9),
            };
            _d.Productos.Add(p);
            foreach (var c in cats) _d.ProductoCategorias.Add(new ProductCategory { ProductId = p.Id, CategoryId = categorias[c].Id });
            foreach (var c in cols) _d.ProductoCatalogos.Add(new ProductCatalogo { ProductId = p.Id, CatalogoId = catalogos[c].Id });
            var lista = new List<ProductRecipe>();
            foreach (var (ins, cant) in receta)
            {
                var r = new ProductRecipe { Id = NuevoId(0x0401), ProductId = p.Id, InventoryItemId = _insumo[ins].Id, CantidadRequerida = cant };
                _d.Recetas.Add(r);
                lista.Add(r);
            }
            _recetaDe[p.Id] = lista;
            if (personalizable)
                foreach (var o in new[] { _optColor, _optMensaje, _optChocolates })
                    _d.ProductoPersonalizaciones.Add(new ProductCustomizationOption { ProductId = p.Id, CustomizationOptionId = o.Id });
            if (peso > 0) _vendibles.Add((p, peso));
        }

        const string A = "ARREGLO_FLORAL", F = "FLORERO";
        var todoElAnio = new[] { "Todo el año" };
        P("Ramo de 12 rosas rojas", 520m, A, ["Ramos", "Aniversario y amor"], ["Todo el año", "San Valentín"],
            [("Rosa roja", 12), ("Follaje de eucalipto", 4), ("Papel coreano", 3), ("Listón satinado", 2), ("Tarjeta de dedicatoria", 1)], true, true, 14);
        P("Ramo de 24 rosas rojas", 950m, A, ["Ramos", "Aniversario y amor"], ["Todo el año", "San Valentín"],
            [("Rosa roja", 24), ("Follaje de eucalipto", 6), ("Papel coreano", 4), ("Listón satinado", 3), ("Tarjeta de dedicatoria", 1)], true, false, 6);
        P("Ramo de rosas blancas", 480m, A, ["Ramos"], todoElAnio,
            [("Rosa blanca", 12), ("Nube (gypsophila)", 1), ("Papel coreano", 3), ("Listón satinado", 2)], true, true, 7);
        P("Ramo de girasoles", 390m, A, ["Ramos", "Cumpleaños"], todoElAnio,
            [("Girasol", 6), ("Follaje de eucalipto", 4), ("Papel coreano", 2), ("Listón satinado", 2)], true, true, 10);
        P("Ramo primaveral de gerberas", 340m, A, ["Ramos", "Cumpleaños"], ["Todo el año", "Día de las Madres"],
            [("Gerbera", 8), ("Alstroemeria", 4), ("Helecho cuero", 4), ("Papel coreano", 2)], true, true, 9);
        P("Ramo mixto de temporada", 420m, A, ["Ramos"], todoElAnio,
            [("Rosa rosa", 6), ("Clavel", 6), ("Alstroemeria", 4), ("Follaje de eucalipto", 3), ("Papel coreano", 3)], true, false, 8);
        P("Tulipanes en caja", 690m, A, ["Arreglos", "Aniversario y amor"], ["Todo el año", "Día de las Madres"],
            [("Tulipán", 10), ("Caja kraft", 1), ("Listón satinado", 2)], true, false, 4);
        P("Arreglo en florero primaveral", 650m, F, ["Arreglos", "Cumpleaños"], todoElAnio,
            [("Rosa blanca", 6), ("Girasol", 3), ("Lilium", 2), ("Follaje de eucalipto", 5), ("Florero de vidrio", 1), ("Espuma floral", 1)], true, false, 6);
        P("Caja de rosas y chocolates", 780m, A, ["Arreglos", "Aniversario y amor"], ["Todo el año", "San Valentín"],
            [("Rosa roja", 9), ("Caja kraft", 1), ("Chocolates (caja de 3)", 1), ("Espuma floral", 1)], true, false, 5);
        P("Canasta de lilis", 720m, A, ["Arreglos"], ["Todo el año", "Día de las Madres"],
            [("Lilium", 5), ("Clavel", 6), ("Canasta de mimbre", 1), ("Espuma floral", 1), ("Helecho cuero", 5)], true, false, 3);
        P("Arreglo de cumpleaños con globo", 560m, A, ["Cumpleaños", "Arreglos"], todoElAnio,
            [("Gerbera", 6), ("Clavel", 6), ("Globo metálico", 1), ("Caja kraft", 1), ("Espuma floral", 1)], true, true, 7);
        P("Corona de condolencias", 1450m, A, ["Condolencias"], ["Todo el año", "Día de Muertos"],
            [("Crisantemo blanco", 30), ("Clavel", 20), ("Helecho cuero", 15), ("Base para corona", 1), ("Espuma floral", 2)], false, false, 3, "SOLO_SUCURSAL");
        P("Arreglo de condolencias en base", 980m, A, ["Condolencias"], ["Todo el año", "Día de Muertos"],
            [("Lilium", 4), ("Crisantemo blanco", 12), ("Helecho cuero", 6), ("Espuma floral", 2)], false, false, 3);
        P("Arreglo navideño", 590m, A, ["Arreglos"], ["Navidad"],
            [("Rosa roja", 6), ("Follaje de eucalipto", 6), ("Espuma floral", 1), ("Listón satinado", 2)], true, false, 0);
        P("Arreglo de Día de Muertos", 260m, A, ["Ramos"], ["Día de Muertos"],
            [("Clavel", 10), ("Helecho cuero", 4), ("Papel coreano", 2)], false, false, 0);
        P("Orquídea en maceta", 420m, F, ["Plantas"], todoElAnio,
            [("Orquídea en maceta", 1), ("Listón satinado", 1)], false, true, 4);
        P("Rosa individual", 45m, A, ["Ramos", "Aniversario y amor"], todoElAnio,
            [("Rosa roja", 1), ("Papel coreano", 1), ("Listón satinado", 1)], false, true, 10);
        P("Girasol individual", 50m, A, ["Ramos"], todoElAnio,
            [("Girasol", 1), ("Papel coreano", 1)], false, true, 6);
        P("Caja de chocolates", 95m, F, ["Complementos"], todoElAnio,
            [("Chocolates (caja de 3)", 1)], false, true, 6);
        P("Globo metálico", 70m, F, ["Complementos", "Cumpleaños"], todoElAnio,
            [("Globo metálico", 1)], false, true, 5);
        P("Peluche pequeño", 220m, F, ["Complementos"], todoElAnio,
            [("Peluche pequeño", 1)], false, false, 3);
        P("Florero de vidrio", 180m, F, ["Complementos"], todoElAnio,
            [("Florero de vidrio", 1)], false, false, 2);
    }

    private CustomizationOption Personalizacion(string clave, string nombre, string tipo, string? opciones, decimal precio)
    {
        var o = new CustomizationOption
        {
            Id = NuevoId(0x0402), Clave = clave, Nombre = nombre, Tipo = tipo, Opciones = opciones,
            PrecioAdicional = precio, Activo = true,
        };
        _d.Personalizaciones.Add(o);
        return o;
    }

    // ── Clientes ─────────────────────────────────────────────────

    private static readonly string[] NombresF =
        ["María", "Guadalupe", "Rosa", "Ana", "Laura", "Patricia", "Elena", "Sofía", "Fernanda", "Daniela", "Gabriela", "Andrea", "Carmen", "Verónica", "Isabel", "Mariana"];
    private static readonly string[] NombresM =
        ["José", "Juan", "Luis", "Carlos", "Miguel", "Jorge", "Francisco", "Alejandro", "Ricardo", "Fernando", "Eduardo", "Roberto", "Diego", "Arturo", "Raúl", "Sergio"];
    private static readonly string[] Apellidos =
        ["García", "Martínez", "López", "Hernández", "González", "Pérez", "Rodríguez", "Sánchez", "Ramírez", "Cruz", "Flores", "Gómez", "Morales", "Vázquez", "Reyes", "Jiménez", "Torres", "Díaz", "Ruiz", "Mendoza"];
    private static readonly string[] Colonias =
        ["Centro", "Jardines", "Las Flores", "Del Valle", "San José", "La Loma", "Los Pinos", "Benito Juárez", "El Mirador", "Santa Cruz"];
    private static readonly string[] Calles =
        ["Av. Principal", "Calle Hidalgo", "Calle Morelos", "Av. Juárez", "Calle Reforma", "Calle Allende", "Calle Guerrero", "Calle Zaragoza", "Calle Independencia", "Calle Aldama"];

    private void Clientes()
    {
        _mostrador = new Customer
        {
            Id = NuevoId(0x0500), TipoCliente = "MOSTRADOR", Nombre = "Venta de mostrador", Telefono = "5550000000",
            CreadoEn = Utc(_hoy.AddDays(-300), 9),
        };
        _d.Clientes.Add(_mostrador);

        for (var i = 1; i <= 150; i++)
        {
            var mujer    = Prob(0.62);
            var nombre   = Elegir(mujer ? NombresF : NombresM);
            var apellido = $"{Elegir(Apellidos)} {Elegir(Apellidos)}";
            var sesion   = i <= 25;
            var correo   = $"cliente{i:000}{DominioFicticio}";

            var c = new Customer
            {
                Id               = NuevoId(0x0500),
                TipoCliente      = sesion ? "SESION" : "FISICO",
                Nombre           = nombre,
                Apellido         = apellido,
                Telefono         = $"5550200{i:000}",
                Correo           = sesion || Prob(0.4) ? correo : null,
                Sexo             = mujer ? "Femenino" : "Masculino",
                FechaNacimiento  = Prob(0.6) ? new DateOnly(1960 + _rnd.Next(45), 1 + _rnd.Next(12), 1 + _rnd.Next(28)) : null,
                Rfc              = Prob(0.1) ? "XAXX010101000" : null,
                CreadoEn         = Utc(_hoy.AddDays(-_rnd.Next(130, 400)), 10),
            };

            if (sesion)
            {
                var u = NuevoUsuario(100 + i, nombre, apellido, correo, RolCliente);
                c.UserId = u.Id;
                _clientesSesion.Add(c);
            }
            else
            {
                _clientesFisicos.Add(c);
            }
            _d.Clientes.Add(c);

            var direcciones = Prob(0.3) ? 2 : 1;
            for (var k = 0; k < direcciones; k++)
            {
                _d.Direcciones.Add(new Address
                {
                    Id          = NuevoId(0x0501),
                    CustomerId  = c.Id,
                    Etiqueta    = k == 0 ? "Casa" : "Trabajo",
                    Calle       = $"{Elegir(Calles)} {10 + _rnd.Next(890)}",
                    Colonia     = Elegir(Colonias),
                    Municipio   = "Huejutla de Reyes",
                    Estado      = "Hidalgo",
                    Cp          = $"430{_rnd.Next(10):0}{_rnd.Next(10):0}",
                    Referencias = Prob(0.5) ? "Portón blanco (dato ficticio)" : null,
                    EsPrincipal = k == 0,
                    CreadoEn    = c.CreadoEn,
                });
            }
        }
    }

    // ── Pedidos, pagos y entregas ────────────────────────────────

    private static readonly string[] Mensajes =
        ["¡Feliz cumpleaños!", "Te quiero mucho", "Con cariño", "Felicidades en tu día", "Para la mejor mamá", "Gracias por todo", "Feliz aniversario"];
    private static readonly string[] MotivosCancelacion =
        ["El cliente canceló por cambio de fecha", "No se confirmó el anticipo", "Pedido duplicado"];

    private Guid EmpleadoAleatorio()
    {
        var x = _rnd.NextDouble();
        return x < 0.5 ? _empleado : x < 0.8 ? _empleado2 : x < 0.92 ? _empleado3 : _admin;
    }

    private string MetodoPago() =>
        _rnd.NextDouble() switch { < 0.62 => "EFECTIVO", < 0.87 => "TARJETA", _ => "TRANSFERENCIA" };

    private int PedidosDelDia(DateOnly dia)
    {
        var n = 8 + _rnd.Next(7);                       // 8–14
        if (dia.DayOfWeek is DayOfWeek.Saturday) n += 3;
        if (dia.DayOfWeek is DayOfWeek.Sunday)   n -= 3;
        var md = (dia.Month, dia.Day);
        if (md is (2, 13) or (2, 14) or (5, 9) or (5, 10) or (11, 1) or (11, 2) or (12, 23) or (12, 24)) n *= 3;
        return Math.Max(3, n);
    }

    private void Pedidos()
    {
        // Historia: los últimos 120 días.
        for (var d = -120; d <= -1; d++)
        {
            var dia = _hoy.AddDays(d);
            var n   = PedidosDelDia(dia);
            for (var k = 0; k < n; k++) NuevoPedido(dia, null);
        }

        // Hoy: las primeras 7 ventas son de mostrador hechas por la cuenta EMPLEADO de
        // revisión (su "Mi día", su caja y su corte nunca se ven vacíos).
        const int ventasHoyEmpleado = 7, otrosHoy = 8;
        for (var k = 0; k < ventasHoyEmpleado; k++)
            NuevoPedido(_hoy, (k, ventasHoyEmpleado + otrosHoy), forzarMostradorDe: _empleado);
        for (var k = 0; k < otrosHoy; k++)
            NuevoPedido(_hoy, (ventasHoyEmpleado + k, ventasHoyEmpleado + otrosHoy));

        // Futuro: pedidos anticipados pendientes para los próximos 14 días.
        for (var k = 0; k < 45; k++)
            NuevoPedidoFuturo(_hoy.AddDays(1 + _rnd.Next(14)));
    }

    private Product ProductoAleatorio(bool soloInstantaneo = false)
    {
        var candidatos = soloInstantaneo ? _vendibles.Where(v => v.P.PermiteVentaInstantanea).ToList() : _vendibles;
        var total = candidatos.Sum(v => v.Peso);
        var x = _rnd.Next(total);
        foreach (var (p, peso) in candidatos)
        {
            if (x < peso) return p;
            x -= peso;
        }
        return candidatos[^1].P;
    }

    private Order CrearPedidoBase(Customer cliente, string tipo, string canal, DateTime creado, DateOnly entrega,
        TimeOnly? hora, Guid? atendio, bool conEnvio, bool soloInstantaneo = false)
    {
        var o = new Order
        {
            Id = NuevoId(0x0600), CustomerId = cliente.Id, TipoPedido = tipo, Canal = canal,
            FechaCreacion = creado, ActualizadoEn = creado, FechaEntrega = entrega, HoraEntrega = hora,
            AtendidoPorUsuarioId = atendio,
        };

        if (conEnvio)
        {
            var dir = _d.Direcciones.FirstOrDefault(a => a.CustomerId == cliente.Id);
            o.DireccionEntregaCalle       = dir?.Calle ?? $"{Elegir(Calles)} {10 + _rnd.Next(890)}";
            o.DireccionEntregaColonia     = dir?.Colonia ?? Elegir(Colonias);
            o.DireccionEntregaMunicipio   = dir?.Municipio ?? "Huejutla de Reyes";
            o.DireccionEntregaEstado      = dir?.Estado ?? "Hidalgo";
            o.DireccionEntregaCp          = dir?.Cp;
            o.DireccionEntregaReferencias = dir?.Referencias;
            o.CostoEnvio                  = Prob(0.5) ? 60m : 80m;
        }

        var total = o.CostoEnvio ?? 0m;
        var lineas = _rnd.NextDouble() switch { < 0.7 => 1, < 0.95 => 2, _ => 3 };
        var usados = new HashSet<Guid>();
        for (var l = 0; l < lineas; l++)
        {
            var p = ProductoAleatorio(soloInstantaneo);
            if (!usados.Add(p.Id)) continue;
            var cant = Prob(0.85) ? 1 : 2;
            if (p.PrecioBase < 100m) cant = 1 + _rnd.Next(6);  // flor suelta y complementos
            var item = new OrderItem
            {
                Id = NuevoId(0x0601), OrderId = o.Id, ProductId = p.Id, Cantidad = cant,
                PrecioUnitario = p.PrecioBase, Subtotal = p.PrecioBase * cant,
            };
            _d.Lineas.Add(item);
            total += item.Subtotal;

            if (p.EsPersonalizable && Prob(0.45))
                _d.LineasPersonalizadas.Add(new OrderItemCustomization
                {
                    Id = NuevoId(0x0602), OrderItemId = item.Id, CustomizationOptionId = _optMensaje.Id, Valor = Elegir(Mensajes),
                });
            if (p.EsPersonalizable && Prob(0.2))
                _d.LineasPersonalizadas.Add(new OrderItemCustomization
                {
                    Id = NuevoId(0x0602), OrderItemId = item.Id, CustomizationOptionId = _optColor.Id, Valor = Elegir(new[] { "Rojo", "Rosa", "Blanco", "Mixto" }),
                });
        }

        o.Total = total;
        _d.Pedidos.Add(o);
        return o;
    }

    private void Pagar(Order o, decimal monto, string tipoPago, string metodo, DateTime fecha, Guid? registro)
    {
        if (monto <= 0) return;
        _d.Pagos.Add(new Payment
        {
            Id = NuevoId(0x0603), OrderId = o.Id, Monto = monto, TipoPago = tipoPago, Metodo = metodo,
            FechaPago = fecha, Estado = "REGISTRADO", RegistradoPorUsuarioId = registro,
        });
    }

    private void Entrega(Order o, string estado, DateTime? real, Guid? repartidor)
    {
        _d.Entregas.Add(new Delivery
        {
            Id = NuevoId(0x0604), OrderId = o.Id, RepartidorId = repartidor,
            FechaProgramada = o.FechaEntrega, HoraProgramada = o.HoraEntrega,
            EstadoEntrega = estado, FechaReal = real,
            Notas = estado == "ENTREGADA" ? "Entregado a quien recibe (dato ficticio)" : null,
        });
    }

    /// <param name="hoy">Para pedidos de hoy: (índice, total) para repartir la hora entre las 9:00 y ahora.</param>
    private void NuevoPedido(DateOnly dia, (int Indice, int Total)? hoy, Guid? forzarMostradorDe = null)
    {
        var creado = hoy is { } h ? MomentoDeHoy(h.Indice, h.Total) : Utc(dia, 9 + _rnd.Next(11), _rnd.Next(60));
        var canal  = forzarMostradorDe.HasValue ? "FISICO"
                   : _rnd.NextDouble() switch { < 0.55 => "FISICO", < 0.85 => "WEB", _ => "TELEFONO" };

        // ── Venta de mostrador: se entrega y se cobra en el acto ──
        if (canal == "FISICO" && (forzarMostradorDe.HasValue || Prob(0.75)))
        {
            var cliente = Prob(0.35) || forzarMostradorDe.HasValue && Prob(0.5) ? _mostrador : Elegir(_clientesFisicos);
            var atendio = forzarMostradorDe ?? EmpleadoAleatorio();
            var o = CrearPedidoBase(cliente, "INSTANTANEO", canal, creado, dia, null, atendio, conEnvio: false);
            o.EstadoPedido = "ENTREGADO";
            o.SaldoPendiente = 0;
            o.Archivado = dia < _hoy;
            o.ArchivadoEn = dia < _hoy ? Utc(dia.AddDays(1), 0, 5) : null;
            // En hoy, las ventas del empleado de revisión son en efectivo: alimentan su caja.
            var metodo = forzarMostradorDe.HasValue ? (Prob(0.8) ? "EFECTIVO" : "TARJETA") : MetodoPago();
            Pagar(o, o.Total, "TOTAL", metodo, creado, atendio);
            return;
        }

        // ── Pedido anticipado (web, teléfono o mostrador con entrega posterior) ──
        var anticipacion = canal == "WEB" ? _rnd.Next(0, 4) : _rnd.Next(0, 8);
        var entrega = dia.AddDays(anticipacion);
        var clienteA = canal == "WEB" ? Elegir(_clientesSesion) : Elegir(_clientesFisicos);
        var atendioA = canal == "WEB" ? (Guid?)null : EmpleadoAleatorio();
        var envio    = canal switch { "WEB" => Prob(0.7), "TELEFONO" => Prob(0.8), _ => Prob(0.4) };
        var hora     = new TimeOnly(10 + _rnd.Next(8), _rnd.Next(2) * 30);
        var pedido   = CrearPedidoBase(clienteA, "ANTICIPADO", canal, creado, entrega, hora, atendioA, envio);
        var metodoA  = canal == "WEB" ? "TARJETA" : MetodoPago();
        var registra = canal == "WEB" ? (Guid?)null : atendioA;

        if (entrega < _hoy)
        {
            pedido.Archivado   = true;
            pedido.ArchivadoEn = Utc(entrega.AddDays(1), 0, 5);
            var x = _rnd.NextDouble();
            if (x < 0.07)
            {
                pedido.EstadoPedido      = "CANCELADO";
                pedido.MotivoCancelacion = Elegir(MotivosCancelacion);
                pedido.SaldoPendiente    = 0;
                return;
            }
            if (x < 0.10)
            {
                // Nadie lo atendió a tiempo: el archivador lo cerró como NO_COMPLETADO.
                var anticipo = Math.Round(pedido.Total / 2, 0);
                Pagar(pedido, anticipo, "ANTICIPO", metodoA, creado, registra);
                pedido.EstadoPedido   = "NO_COMPLETADO";
                pedido.SaldoPendiente = pedido.Total - anticipo;
                return;
            }

            pedido.EstadoPedido   = "ENTREGADO";
            pedido.SaldoPendiente = 0;
            if (canal == "WEB" || Prob(0.5))
            {
                Pagar(pedido, pedido.Total, "TOTAL", metodoA, creado, registra);
            }
            else
            {
                var anticipo = Math.Round(pedido.Total / 2, 0);
                Pagar(pedido, anticipo, "ANTICIPO", metodoA, creado, registra);
                Pagar(pedido, pedido.Total - anticipo, "LIQUIDACION", MetodoPago(), Utc(entrega, hora), atendioA ?? _empleado2);
            }
            if (envio) Entrega(pedido, "ENTREGADA", Utc(entrega, hora).AddMinutes(_rnd.Next(10, 50)), Prob(0.7) ? _empleado3 : _empleado2);
            return;
        }

        // Entrega hoy o después, creado en el pasado o hoy: en curso.
        var estado = entrega == _hoy
            ? _rnd.NextDouble() switch { < 0.2 => "PENDIENTE_VALIDACION", < 0.6 => "EN_PREPARACION", < 0.85 => envio ? "EN_RUTA" : "EN_PREPARACION", _ => "ENTREGADO" }
            : _rnd.NextDouble() < 0.6 ? "PENDIENTE_VALIDACION" : "EN_PREPARACION";
        PedidoEnCurso(pedido, estado, envio, metodoA, registra, creado);
    }

    private void NuevoPedidoFuturo(DateOnly entrega)
    {
        var canal   = _rnd.NextDouble() switch { < 0.45 => "WEB", < 0.75 => "TELEFONO", _ => "FISICO" };
        var cliente = canal == "WEB" ? Elegir(_clientesSesion) : Elegir(_clientesFisicos);
        var atendio = canal == "WEB" ? (Guid?)null : EmpleadoAleatorio();
        var envio   = Prob(0.7);
        var hora    = new TimeOnly(10 + _rnd.Next(8), _rnd.Next(2) * 30);
        var diasAntes = _rnd.Next(0, 6);
        var creado  = diasAntes == 0 ? MomentoDeHoy(_rnd.Next(10), 10) : Utc(_hoy.AddDays(-diasAntes), 9 + _rnd.Next(10), _rnd.Next(60));
        var pedido  = CrearPedidoBase(cliente, "ANTICIPADO", canal, creado, entrega, hora, atendio, envio);
        var estado  = _rnd.NextDouble() < 0.6 ? "PENDIENTE_VALIDACION" : "EN_PREPARACION";
        PedidoEnCurso(pedido, estado, envio, canal == "WEB" ? "TARJETA" : MetodoPago(), canal == "WEB" ? null : atendio, creado);
    }

    private void PedidoEnCurso(Order pedido, string estado, bool envio, string metodo, Guid? registra, DateTime creado)
    {
        pedido.EstadoPedido = estado;
        var x = _rnd.NextDouble();
        decimal pagado = 0;
        if (estado == "ENTREGADO" || x < 0.25) { pagado = pedido.Total; Pagar(pedido, pagado, "TOTAL", metodo, creado, registra); }
        else if (x < 0.85) { pagado = Math.Round(pedido.Total / 2, 0); Pagar(pedido, pagado, "ANTICIPO", metodo, creado, registra); }
        pedido.SaldoPendiente = pedido.Total - pagado;

        if (!envio) return;
        var repartidor = Prob(0.7) ? _empleado3 : _empleado2;
        switch (estado)
        {
            case "ENTREGADO": Entrega(pedido, "ENTREGADA", MomentoDeHoy(_rnd.Next(10), 10), repartidor); break;
            case "EN_RUTA":   Entrega(pedido, "EN_RUTA", null, repartidor); break;
            default:          Entrega(pedido, "PROGRAMADA", null, Prob(0.5) ? repartidor : null); break;
        }
    }

    // ── Inventario ───────────────────────────────────────────────

    private void Inventario()
    {
        // Consumo por día e insumo de todo lo que salió de la tienda (entregado).
        var consumo = new Dictionary<(DateOnly, Guid), int>();
        var pedidosPorId = _d.Pedidos.ToDictionary(p => p.Id);
        foreach (var linea in _d.Lineas)
        {
            var p = pedidosPorId[linea.OrderId];
            if (p.EstadoPedido != "ENTREGADO") continue;
            foreach (var r in _recetaDe[linea.ProductId])
            {
                var k = (p.FechaEntrega, r.InventoryItemId);
                consumo[k] = consumo.GetValueOrDefault(k) + r.CantidadRequerida * linea.Cantidad;
            }
        }

        var empleados = new[] { _empleado, _empleado2 };
        var bajoMinimo = new HashSet<string> { "Tulipán", "Lilium", "Peluche pequeño", "Espuma floral" };

        foreach (var insumo in _d.Insumos)
        {
            var usoPromedio = Math.Max(1, Enumerable.Range(1, 120)
                .Sum(d => consumo.GetValueOrDefault((_hoy.AddDays(-d), insumo.Id))) / 120);
            var objetivo = Math.Max(insumo.StockMinimo * 2, usoPromedio * 9);
            var stock = objetivo;

            for (var d = -120; d <= 0; d++)
            {
                var dia = _hoy.AddDays(d);
                int entrada = 0, salida = 0, merma = 0;

                // Recepción los lunes (o de emergencia si no alcanza para el día).
                var usoDia = consumo.GetValueOrDefault((dia, insumo.Id));
                if (dia.DayOfWeek == DayOfWeek.Monday || stock < usoDia)
                {
                    entrada = Math.Max(0, objetivo - stock) + usoDia;
                    if (entrada > 0)
                        Mov(insumo, "ENTRADA", entrada, "RECEPCION", "Recepción de proveedor (ficticio)",
                            _admin, dia == _hoy ? MomentoDeHoy(0, 4) : Utc(dia, 8, 30));
                    stock += entrada;
                }

                if (usoDia > 0)
                {
                    salida = usoDia;
                    Mov(insumo, "SALIDA", salida, "VENTA", "Consumo por ventas del día",
                        Elegir(empleados), dia == _hoy ? MomentoDeHoy(2, 4) : Utc(dia, 20, 0));
                    stock -= salida;
                }

                // Merma: manipulación los miércoles y caducidad algunos viernes (flores).
                if (insumo.EsFlorPrimaria && dia.DayOfWeek == DayOfWeek.Wednesday && stock > 5)
                {
                    merma = Math.Max(1, stock / 25);
                    Mov(insumo, "SALIDA", merma, "MANIPULACION", "Tallos dañados al acomodar",
                        Elegir(empleados), Utc(dia, 12));
                    stock -= merma;
                }
                if (insumo.EsFlorPrimaria && dia.DayOfWeek == DayOfWeek.Friday && Prob(0.4) && stock > 5)
                {
                    var cad = Math.Max(1, stock / 15);
                    Mov(insumo, "SALIDA", cad, "CADUCIDAD", "Flor marchita retirada",
                        Elegir(empleados), Utc(dia, 18));
                    merma += cad;
                    stock -= cad;
                }

                if (dia < _hoy)
                    _d.Instantaneas.Add(new InventoryDailySnapshot
                    {
                        Id = NuevoId(0x0701), InventoryItemId = insumo.Id, Fecha = dia, StockFinal = stock,
                        CantidadVendida = salida + merma, CantidadRecibida = entrada, DemandaRechazada = 0,
                    });
            }

            // Algunos insumos quedan por debajo del mínimo para que se vean las alertas.
            if (bajoMinimo.Contains(insumo.Nombre) && stock >= insumo.StockMinimo)
            {
                var retiro = stock - Math.Max(0, insumo.StockMinimo / 2);
                if (retiro > 0)
                {
                    Mov(insumo, "SALIDA", retiro, "CADUCIDAD", "Lote retirado por calidad", _empleado, MomentoDeHoy(3, 4));
                    stock -= retiro;
                }
            }

            // TEMPORAL-TESTERS: con testers, existencias de sobra para 14 días de ventas
            // diarias de mostrador (una venta con receta se rechaza si no alcanza). Se deja
            // sin reabasto un par de insumos para que la alerta de stock bajo siga visible.
            if (_testers.Count > 0 && !InsumosSinReabastoTesters.Contains(insumo.Nombre))
            {
                var extra = Math.Max(500, insumo.StockMinimo * 50);
                Mov(insumo, "ENTRADA", extra, "RECEPCION", "Reabasto para pruebas de testers (ficticio)",
                    _admin, MomentoDeHoy(1, 4));
                stock += extra;
            }

            insumo.StockActual   = stock;
            insumo.ActualizadoEn = _ahoraUtc;
        }
    }

    /// <summary>Insumos que no reciben el reabasto de testers (alerta de stock bajo visible; ningún producto de venta instantánea los usa).</summary>
    public static readonly IReadOnlySet<string> InsumosSinReabastoTesters =
        new HashSet<string>(StringComparer.Ordinal) { "Tulipán", "Peluche pequeño" };

    private void Mov(InventoryItem i, string tipo, int cantidad, string categoria, string motivo, Guid usuario, DateTime fecha)
    {
        _d.Movimientos.Add(new InventoryMovement
        {
            Id = NuevoId(0x0700), InventoryItemId = i.Id, TipoMovimiento = tipo, Cantidad = cantidad,
            MotivoCategoria = categoria, Motivo = motivo, UsuarioId = usuario, FechaHora = fecha,
        });
    }

    // ── Caja, gastos y reportes de error ─────────────────────────

    private static readonly string[] ConceptosGasto =
        ["Pasaje a la central de abasto", "Agua para floreros", "Bolsas para entrega", "Cinta adhesiva", "Comida del turno", "Gasolina de la moto de reparto", "Copias de notas"];

    private void CajaYGastos()
    {
        var empleados = new[] { _empleado, _empleado2, _empleado3 };

        // Gastos de los últimos 45 días.
        for (var d = -45; d <= -1; d++)
        {
            var dia = _hoy.AddDays(d);
            foreach (var e in empleados)
                if (Prob(0.35))
                    NuevoGasto(e, Utc(dia, 11 + _rnd.Next(7), _rnd.Next(60)));
        }

        // Hoy: dos gastos abiertos del empleado de revisión (aún sin corte).
        NuevoGasto(_empleado, MomentoDeHoy(1, 3));
        NuevoGasto(_empleado, MomentoDeHoy(2, 3));

        // Cortes de caja cerrados de los últimos 30 días (nunca el de hoy: queda abierto).
        var pagosPorPedido = _d.Pagos.ToLookup(p => p.OrderId);
        for (var d = -30; d <= -1; d++)
        {
            var dia = _hoy.AddDays(d);
            var ini = Utc(dia, 0);
            var fin = Utc(dia.AddDays(1), 0);
            foreach (var e in empleados)
            {
                var pedidos = _d.Pedidos.Where(o => o.AtendidoPorUsuarioId == e && o.FechaCreacion >= ini
                                                    && o.FechaCreacion < fin && o.EstadoPedido != "CANCELADO").ToList();
                var gastos  = _d.Gastos.Where(g => g.UsuarioId == e && g.FechaHora >= ini && g.FechaHora < fin).ToList();
                if (pedidos.Count == 0 && gastos.Count == 0) continue;

                var efectivo = _d.Pagos.Where(p => p.RegistradoPorUsuarioId == e && p.Metodo == "EFECTIVO"
                                                   && p.FechaPago >= ini && p.FechaPago < fin).Sum(p => p.Monto);
                var totalGastos = gastos.Sum(g => g.Importe);
                var esperado    = efectivo - totalGastos;
                var diferencia  = Prob(0.8) ? 0m : Elegir(new[] { -20m, -10m, 10m, 5m });
                var corte = new CashCut
                {
                    Id = NuevoId(0x0800), UsuarioId = e, FechaLocal = dia, FechaHoraCierre = Utc(dia, 20, 15),
                    PeriodoInicioUtc = ini, PeriodoFinUtc = fin,
                    TotalVentas = pedidos.Sum(o => o.Total), TotalEfectivo = efectivo, TotalGastos = totalGastos,
                    EfectivoEsperado = esperado, EfectivoDeclarado = esperado + diferencia, Diferencia = diferencia,
                    PedidosContados = pedidos.Count, GastosContados = gastos.Count, Estado = "CERRADO",
                    Notas = diferencia == 0 ? null : "Diferencia por cambio (dato ficticio)",
                };
                _d.Cortes.Add(corte);
                foreach (var g in gastos) g.CashCutId = corte.Id;
            }
        }

        // Reportes de error: uno pendiente, uno resuelto y uno rechazado.
        var gastoViejo = _d.Gastos.FirstOrDefault(g => g.UsuarioId == _empleado && g.CashCutId != null)
                         ?? _d.Gastos.First(g => g.UsuarioId == _empleado);
        var corteViejo = _d.Cortes.First(c => c.UsuarioId == _empleado);
        var gastoOtro  = _d.Gastos.FirstOrDefault(g => g.UsuarioId == _empleado2) ?? gastoViejo;
        _d.Reportes.Add(new ErrorReport
        {
            Id = NuevoId(0x0801), UsuarioId = _empleado, TipoRegistro = "GASTO", RegistroId = gastoViejo.Id,
            Motivo = "Capturé un importe equivocado", Estado = "PENDIENTE", FechaHora = Utc(_hoy.AddDays(-1), 19),
        });
        _d.Reportes.Add(new ErrorReport
        {
            Id = NuevoId(0x0801), UsuarioId = _empleado, TipoRegistro = "CORTE", RegistroId = corteViejo.Id,
            Motivo = "Conté mal el efectivo", Estado = "RESUELTO", FechaHora = corteViejo.FechaHoraCierre.AddMinutes(30),
            ResueltoPorUsuarioId = _admin, FechaResolucion = corteViejo.FechaHoraCierre.AddHours(14),
            ResolucionAccion = "SIN_CAMBIO", ResolucionNota = "Se revisó con el empleado; el corte está bien.",
        });
        _d.Reportes.Add(new ErrorReport
        {
            Id = NuevoId(0x0801), UsuarioId = _empleado2, TipoRegistro = "GASTO", RegistroId = gastoOtro.Id,
            Motivo = "El gasto se registró dos veces", Estado = "RECHAZADO", FechaHora = gastoOtro.FechaHora.AddHours(2),
            ResueltoPorUsuarioId = _admin, FechaResolucion = gastoOtro.FechaHora.AddDays(1),
            ResolucionAccion = "SIN_CAMBIO", ResolucionNota = "Solo hay un registro.",
        });
    }

    private void NuevoGasto(Guid usuario, DateTime fecha) =>
        _d.Gastos.Add(new Expense
        {
            Id = NuevoId(0x0802), UsuarioId = usuario, FechaHora = fecha, Concepto = Elegir(ConceptosGasto),
            Categoria = "GASTO_PERSONAL", Importe = Monto(20m, 180m), Estado = "REGISTRADO",
        });

    // ── Abastecimiento ───────────────────────────────────────────

    private void Abastecimiento()
    {
        var flores = _d.Insumos.Where(i => i.EsFlorPrimaria).ToList();
        var estados = new[] { "RECIBIDA", "RECIBIDA", "RECIBIDA", "RECIBIDA_PARCIAL", "ENVIADA", "BORRADOR" };
        for (var k = 0; k < estados.Length; k++)
        {
            var estado = estados[k];
            var semanas = estados.Length - 1 - k;
            var fecha = Utc(_hoy.AddDays(-7 * semanas - 3), 10);
            var oc = new SupplyOrder
            {
                Id = NuevoId(0x0900), Folio = $"REAB-{_hoy.Year}-{k + 1:0000}", Estado = estado,
                Proveedor = Elegir(new[] { "Proveedor de flores (ficticio)", "Mayorista central (ficticio)" }),
                FechaSolicitud = fecha, FechaEnvio = estado == "BORRADOR" ? null : fecha.AddHours(2),
                FechaRecepcion = estado.StartsWith("RECIBIDA") ? fecha.AddDays(3) : null,
                UsuarioId = _admin, Notas = "Orden de demostración",
            };
            decimal total = 0;
            foreach (var ins in flores.OrderBy(_ => _rnd.Next()).Take(3 + _rnd.Next(4)).ToList())
            {
                var cant = 25 * (1 + _rnd.Next(6));
                var linea = new SupplyOrderItem
                {
                    Id = NuevoId(0x0901), SupplyOrderId = oc.Id, InventoryItemId = ins.Id, NombreSnapshot = ins.Nombre,
                    UnidadMedida = ins.UnidadMedida, CantidadSolicitada = cant, PrecioUnitario = ins.PrecioCosto,
                    Origen = "Manual",
                };
                if (estado == "RECIBIDA") { linea.CantidadRecibida = cant; linea.EstadoLinea = "COMPLETO"; linea.RecibidoEn = oc.FechaRecepcion; }
                else if (estado == "RECIBIDA_PARCIAL")
                {
                    linea.CantidadRecibida = Prob(0.5) ? cant : cant - 10;
                    linea.EstadoLinea      = linea.CantidadRecibida == cant ? "COMPLETO" : "PARCIAL";
                    linea.Observacion      = linea.EstadoLinea == "PARCIAL" ? "Llegaron menos tallos (dato ficticio)" : null;
                    linea.RecibidoEn       = oc.FechaRecepcion;
                }
                total += cant * ins.PrecioCosto;
                _d.LineasCompra.Add(linea);
            }
            oc.TotalEstimado = total;
            _d.OrdenesCompra.Add(oc);
        }

        for (var k = 0; k < 15; k++)
        {
            var ins = Elegir(flores);
            var solicitada = 5 + _rnd.Next(20);
            var disponible = _rnd.Next(solicitada);
            _d.DemandaNoCubierta.Add(new UncoveredDemand
            {
                Id = NuevoId(0x0902), InventoryItemId = ins.Id, CantidadSolicitada = solicitada,
                CantidadDisponible = disponible, Deficit = solicitada - disponible,
                Origen = Elegir(new[] { "VENTA_INSTANTANEA", "VENTA_ANTICIPADA", "VALIDACION_WEB" }),
                FechaRegistro = Utc(_hoy.AddDays(-_rnd.Next(1, 60)), 12),
            });
        }
    }

    // ── Venta rápida, ventas instantáneas y eventos ──────────────

    private void VentasInstantaneasYEventos()
    {
        var instantaneos = _d.Productos.Where(p => p.PermiteVentaInstantanea).ToList();
        var plantillas = new (string Nombre, string Icono, string[] Productos)[]
        {
            ("Ramos del día", "Flower", ["Ramo de 12 rosas rojas", "Ramo de girasoles", "Ramo primaveral de gerberas", "Ramo de rosas blancas"]),
            ("Flor suelta", "Sparkles", ["Rosa individual", "Girasol individual"]),
            ("Complementos", "Gift", ["Caja de chocolates", "Globo metálico", "Orquídea en maceta"]),
        };
        var colores = new[] { "rose", "amber", "blue", "green", "purple" };
        for (var k = 0; k < plantillas.Length; k++)
        {
            var (nombre, icono, productos) = plantillas[k];
            var t = new QuickSaleTemplate
            {
                Id = NuevoId(0x0a00), Nombre = nombre, Descripcion = $"Botones de venta rápida: {nombre.ToLowerInvariant()}",
                Icono = icono, Orden = k + 1, Activa = true,
                CreadoEn = Utc(_hoy.AddDays(-60), 9), ActualizadoEn = Utc(_hoy.AddDays(-10), 9),
            };
            _d.Plantillas.Add(t);
            for (var j = 0; j < productos.Length; j++)
            {
                var p = _d.Productos.Single(x => x.Nombre == productos[j]);
                _d.PlantillaItems.Add(new QuickSaleTemplateItem
                {
                    Id = NuevoId(0x0a01), QuickSaleTemplateId = t.Id, ProductId = p.Id, Icono = icono,
                    Color = colores[j % colores.Length], Orden = j + 1, CantidadPreset = p.PrecioBase < 100m ? 5 : 1,
                });
            }
        }

        // Historial de solicitudes de venta instantánea (ninguna pendiente: el expirador
        // de la instancia las cerraría a los pocos minutos de todos modos).
        for (var k = 0; k < 25; k++)
        {
            var creada = Utc(_hoy.AddDays(-_rnd.Next(1, 30)), 10 + _rnd.Next(8), _rnd.Next(60));
            var x = _rnd.NextDouble();
            var estado = x < 0.5 ? "ACEPTADA" : x < 0.75 ? "RECHAZADA" : "EXPIRADA";
            _d.SolicitudesVenta.Add(new SolicitudVentaInstantanea
            {
                Id = NuevoId(0x0a02), CustomerId = Elegir(_clientesSesion).Id, ProductId = Elegir(instantaneos).Id,
                Cantidad = 1 + _rnd.Next(2), Estado = estado, CreadaEn = creada,
                EscaladaAEmpleadoEn = estado == "EXPIRADA" ? creada.AddMinutes(5) : null,
                DecididaEn = estado == "EXPIRADA" ? null : creada.AddMinutes(1 + _rnd.Next(4)),
                DecididaPorUsuarioId = estado == "EXPIRADA" ? null : (Prob(0.5) ? _admin : _empleado),
                MotivoRechazo = estado == "RECHAZADA" ? "Sin existencias suficientes en este momento" : null,
                MotivoExpiracion = estado == "EXPIRADA" ? "SIN_RESPUESTA" : null,
            });
        }

        var tipos = new[] { "BODA", "BAUTIZO", "CORPORATIVO", "CONMEMORATIVO", "OTRO", "BODA" };
        var estados = new[] { "PENDIENTE", "PENDIENTE", "CONTACTADA", "CONTACTADA", "CERRADA", "PENDIENTE" };
        for (var k = 0; k < tipos.Length; k++)
        {
            var c = Elegir(_clientesSesion);
            var creado = Utc(_hoy.AddDays(-_rnd.Next(1, 40)), 12);
            _d.SolicitudesEvento.Add(new SolicitudEvento
            {
                Id = NuevoId(0x0a03), CustomerId = c.Id, TipoEvento = tipos[k],
                FechaEvento = _hoy.AddDays(15 + _rnd.Next(90)), InvitadosAprox = 50 + _rnd.Next(200),
                NombreContacto = $"{c.Nombre} {c.Apellido}", TelefonoContacto = c.Telefono, CorreoContacto = c.Correo,
                Mensaje = "Solicitud de cotización de demostración.", Estado = estados[k],
                CreadoEn = creado, ActualizadoEn = creado,
            });
        }
    }

    // ── Ofertas, descuentos y promociones ────────────────────────

    private void Promociones()
    {
        foreach (var nombre in new[] { "Ramo de girasoles", "Ramo primaveral de gerberas", "Caja de rosas y chocolates", "Orquídea en maceta" })
        {
            var p = _d.Productos.Single(x => x.Nombre == nombre);
            _d.Ofertas.Add(new Oferta
            {
                Id = NuevoId(0x0b00), ProductoId = p.Id, PrecioOferta = Math.Round(p.PrecioBase * 0.85m, 0),
                FechaInicio = _hoy.AddDays(-5), FechaFin = _hoy.AddDays(10), Activo = true,
                CreadoEn = Utc(_hoy.AddDays(-6), 9), ActualizadoEn = Utc(_hoy.AddDays(-6), 9),
            });
        }

        var cumple = _d.Categorias.Single(c => c.Nombre == "Cumpleaños");
        var madres = _d.Catalogos.Single(c => c.Nombre == "Día de las Madres");
        _d.Descuentos.Add(new Descuento
        {
            Id = NuevoId(0x0b01), Nombre = "10% en compras desde $1,000", TipoRegla = "MONTO_MINIMO",
            MontoMinimoCompra = 1000m, TipoValor = "PORCENTAJE", Valor = 10m, Activo = true,
            FechaInicio = _hoy.AddDays(-15), FechaFin = _hoy.AddDays(30),
            CreadoEn = Utc(_hoy.AddDays(-15), 9), ActualizadoEn = Utc(_hoy.AddDays(-15), 9),
        });
        _d.Descuentos.Add(new Descuento
        {
            Id = NuevoId(0x0b01), Nombre = "$50 menos en cumpleaños", TipoRegla = "CATEGORIA", CategoriaId = cumple.Id,
            TipoValor = "MONTO_FIJO", Valor = 50m, Activo = true, FechaInicio = _hoy.AddDays(-3), FechaFin = _hoy.AddDays(20),
            CreadoEn = Utc(_hoy.AddDays(-3), 9), ActualizadoEn = Utc(_hoy.AddDays(-3), 9),
        });
        _d.Descuentos.Add(new Descuento
        {
            Id = NuevoId(0x0b01), Nombre = "15% colección Día de las Madres", TipoRegla = "CATALOGO", CatalogoId = madres.Id,
            TipoValor = "PORCENTAJE", Valor = 15m, Activo = true, FechaInicio = _hoy.AddDays(-1), FechaFin = _hoy.AddDays(45),
            CreadoEn = Utc(_hoy.AddDays(-1), 9), ActualizadoEn = Utc(_hoy.AddDays(-1), 9),
        });

        // Códigos inventados que solo existen en la base de revisión.
        _d.Promociones.Add(new Promotion
        {
            Id = NuevoId(0x0b02), Nombre = "Demo 10%", Codigo = "DEMO10REV", Tipo = "PORCENTAJE", Valor = 10m,
            MinimoCompra = 300m, Estado = "ACTIVO", FechaInicio = _hoy.AddDays(-10), FechaFin = _hoy.AddDays(30),
            MaxUsos = 100, UsosActuales = 7, AplicarATodaLaTienda = true,
            CreadoEn = Utc(_hoy.AddDays(-10), 9), ActualizadoEn = Utc(_hoy.AddDays(-10), 9),
        });
        _d.Promociones.Add(new Promotion
        {
            Id = NuevoId(0x0b02), Nombre = "Demo envío", Codigo = "DEMOENVIOREV", Tipo = "MONTO_FIJO", Valor = 80m,
            MinimoCompra = 600m, Estado = "ACTIVO", FechaInicio = _hoy.AddDays(-2), FechaFin = _hoy.AddDays(14),
            MaxUsos = 50, UsosActuales = 2, AplicarATodaLaTienda = true,
            CreadoEn = Utc(_hoy.AddDays(-2), 9), ActualizadoEn = Utc(_hoy.AddDays(-2), 9),
        });
        _d.Promociones.Add(new Promotion
        {
            Id = NuevoId(0x0b02), Nombre = "Demo temporada", Codigo = "DEMOTEMPREV", Tipo = "PORCENTAJE", Valor = 15m,
            MinimoCompra = 0m, Estado = "PROGRAMADO", FechaInicio = _hoy.AddDays(20), FechaFin = _hoy.AddDays(40),
            AplicarATodaLaTienda = true,
            CreadoEn = Utc(_hoy.AddDays(-1), 9), ActualizadoEn = Utc(_hoy.AddDays(-1), 9),
        });
    }

    // ── Notificaciones y bitácora ────────────────────────────────

    private void NotificacionesYAuditoria()
    {
        var recientes = _d.Pedidos.Where(p => p.FechaCreacion >= Utc(_hoy.AddDays(-3), 0))
                                  .OrderByDescending(p => p.FechaCreacion).Take(10).ToList();
        var k = 0;
        foreach (var p in recientes)
        {
            foreach (var destino in new[] { _admin, _empleado })
                _d.Notificaciones.Add(new Notification
                {
                    Id = NuevoId(0x0c00), DestinatarioUsuarioId = destino, Tipo = "PEDIDO_NUEVO",
                    Titulo = "Nuevo pedido", Mensaje = $"Pedido {p.Canal.ToLowerInvariant()} por ${p.Total:0.00} para el {p.FechaEntrega:dd/MM}.",
                    EntidadTipo = "Order", EntidadId = p.Id, Leida = k > 3, LeidaEn = k > 3 ? p.FechaCreacion.AddMinutes(20) : null,
                    CreadaEn = p.FechaCreacion,
                });
            k++;
        }
        foreach (var ins in _d.Insumos.Where(i => i.StockActual < i.StockMinimo))
            _d.Notificaciones.Add(new Notification
            {
                Id = NuevoId(0x0c00), DestinatarioUsuarioId = _admin, Tipo = "STOCK_BAJO",
                Titulo = "Stock bajo", Mensaje = $"{ins.Nombre}: quedan {ins.StockActual} (mínimo {ins.StockMinimo}).",
                EntidadTipo = "InventoryItem", EntidadId = ins.Id, Leida = false, CreadaEn = MomentoDeHoy(3, 4),
            });
        _d.Notificaciones.Add(new Notification
        {
            Id = NuevoId(0x0c00), DestinatarioUsuarioId = _admin, Tipo = "REPORTE_ERROR",
            Titulo = "Reporte de error de un empleado", Mensaje = "Un empleado reportó un gasto con importe equivocado.",
            EntidadTipo = "ErrorReport", EntidadId = _d.Reportes[0].Id, Leida = false, CreadaEn = _d.Reportes[0].FechaHora,
        });

        foreach (var p in _d.Pedidos.OrderByDescending(p => p.FechaCreacion).Take(20))
            _d.Auditoria.Add(new AuditLog
            {
                Id = NuevoId(0x0c01), UsuarioId = p.AtendidoPorUsuarioId ?? _admin, Accion = "CREAR", Entidad = "Order",
                EntidadId = p.Id.ToString(), Detalles = $"{{\"estado\":\"{p.EstadoPedido}\",\"total\":{p.Total}}}",
                FechaHora = p.FechaCreacion,
            });
        foreach (var prod in _d.Productos.Take(5))
            _d.Auditoria.Add(new AuditLog
            {
                Id = NuevoId(0x0c01), UsuarioId = _admin, Accion = "ACTUALIZAR", Entidad = "Product",
                EntidadId = prod.Id.ToString(), Detalles = $"{{\"precioBase\":{prod.PrecioBase}}}", FechaHora = prod.ActualizadoEn,
            });
    }

    // ── Segmentos RFM (la instancia los recalcula cada 24 h; aquí se dejan listos) ──

    private void Segmentos()
    {
        var porCliente = _d.Pedidos.Where(p => p.EstadoPedido == "ENTREGADO" && p.CustomerId != _mostrador.Id)
                                   .GroupBy(p => p.CustomerId);
        foreach (var g in porCliente)
        {
            var c = _d.Clientes.Single(x => x.Id == g.Key);
            var ultima = g.Max(p => p.FechaEntrega);
            var recencia = _hoy.DayNumber - ultima.DayNumber;
            var frecuencia = g.Count();
            var monto = g.Sum(p => p.Total);
            var grupo = recencia > 60 ? "INACTIVO" : monto >= 3000m && frecuencia >= 5 ? "VIP" : frecuencia >= 3 ? "FRECUENTE" : "OCASIONAL";
            var canales = g.GroupBy(p => p.Canal).OrderByDescending(x => x.Count()).First().Key;
            _d.Segmentos.Add(new CustomerSegment
            {
                Id = NuevoId(0x0c02), CustomerId = c.Id, Grupo = grupo, RecenciaDias = recencia,
                FrecuenciaPedidos = frecuencia, MontoTotal = monto,
                Edad = c.FechaNacimiento is { } f ? _hoy.Year - f.Year : null, Sexo = c.Sexo,
                Colonia = _d.Direcciones.FirstOrDefault(a => a.CustomerId == c.Id)?.Colonia,
                CanalPreferido = canales, TasaCancelacion = 0,
                AntiguedadDias = Math.Max(0, (int)(_ahoraUtc - c.CreadoEn).TotalDays),
                DiasPromedioEntrePedidos = frecuencia > 1 ? (double)(ultima.DayNumber - g.Min(p => p.FechaEntrega).DayNumber) / (frecuencia - 1) : null,
                FechaCalculo = _ahoraUtc,
            });
        }
    }
}
