using FloreriaBautista.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FloreriaBautista.PlayReviewSeed;

/// <summary>
/// Siembra la base del entorno play_review con datos 100 % sintéticos.
///
///   dotnet run --project tools/PlayReviewSeed -- --credenciales &lt;ruta json&gt; [--confirmar &lt;nombre_bd&gt;] [--reiniciar] [--sin-testers]
///
/// La conexión sale SOLO de REVIEW_DB_URL. Guardas (si una falla, no se escribe nada):
///  1. REVIEW_DB_URL definida.
///  2. Si existe PROD_DB_HOST y es el host de destino → aborta sin conectarse.
///  3. Base con datos y sin el marcador _entorno='play_review' → aborta.
///  4. Hay que escribir el nombre de la base (o pasarlo en --confirmar).
/// </summary>
public static class Programa
{
    public static async Task<int> Main(string[] args)
    {
        Argumentos a;
        try { a = Argumentos.Parsear(args); }
        catch (GuardaException ex) { Console.Error.WriteLine(ex.Message); Console.Error.WriteLine(Argumentos.TextoAyuda); return 2; }

        if (a.Ayuda) { Console.WriteLine(Argumentos.TextoAyuda); return 0; }

        try
        {
            await EjecutarAsync(a, Console.Out);
            return 0;
        }
        catch (GuardaException ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"ABORTADO: {ex.Message}");
            return 3;
        }
    }

    private static async Task EjecutarAsync(Argumentos a, TextWriter log)
    {
        var dominios = FloreriaBautista.PlayReview.PlayReviewEntorno.ParsearDominiosLogin(
                           Environment.GetEnvironmentVariable("REVIEW_LOGIN_EMAIL_DOMAIN"))
                       ?? throw new GuardaException("REVIEW_LOGIN_EMAIL_DOMAIN no parece un dominio válido (uno o varios separados por coma).");

        var cuentas = Credenciales.Leer(a.RutaCredenciales!, dominios);
        log.WriteLine($"Cuentas de revisión leídas: {string.Join(", ", cuentas.Select(c => $"{c.Correo} ({c.Rol})"))}");

        // TEMPORAL-TESTERS: eliminar cuando Google Play apruebe la app para producción.
        IReadOnlyList<CuentaTester> testers = a.SinTesters ? [] : CuentasTesters.Todas;
        if (a.SinTesters)
            log.WriteLine("Sin testers (--sin-testers): las cuentas de testers que existan se BORRAN de la base de revisión.");
        else
            log.WriteLine($"Testers de Google Play: {testers.Count} cuentas {CuentasTesters.Dominio} (contraseña común, ver docs/play-review.md). " +
                          $"PROD debe tener {CuentasTesters.Dominio} en REVIEW_LOGIN_EMAIL_DOMAIN o su login no llegará a REVISIÓN.");

        // Guardas 1 y 2: antes de abrir ninguna conexión.
        var conexion = Guardas.ConexionDesdeVariable(Environment.GetEnvironmentVariable("REVIEW_DB_URL"));
        Guardas.HostDistintoDeProduccion(conexion.Host!, Environment.GetEnvironmentVariable("PROD_DB_HOST"));
        log.WriteLine($"Destino: host {conexion.Host}, base '{conexion.Database}', usuario '{conexion.Username}'");

        await using var cn = new NpgsqlConnection(conexion.ConnectionString);
        await cn.OpenAsync();

        // Guarda 3.
        var estado = await BaseRevision.LeerEstadoAsync(cn);
        log.WriteLine($"Estado: {estado.TablasEnPublic} tablas, usuarios={estado.FilasUsuarios}, clientes={estado.FilasClientes}, " +
                      $"pedidos={estado.FilasPedidos}, marcador={(estado.MarcadorEsRevision ? "play_review" : estado.MarcadorExiste ? "OTRO" : "no")}");
        Guardas.EstadoPermiteSembrar(estado, a.Reiniciar);

        // Guarda 4.
        var confirmacion = a.Confirmar;
        if (confirmacion is null)
        {
            if (Console.IsInputRedirected)
                throw new GuardaException("Sin terminal interactiva: pasa --confirmar <nombre_bd>.");
            log.Write($"Escribe el nombre de la base para confirmar ('{conexion.Database}'): ");
            confirmacion = Console.ReadLine();
        }
        Guardas.ConfirmacionCoincide(conexion.Database!, confirmacion);

        // Dataset (en memoria, determinista para el mismo minuto de referencia).
        var ahora = DateTime.UtcNow;
        ahora = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, DateTimeKind.Utc);
        var zona  = GeneradorDatos.ZonaTienda();
        var datos = new GeneradorDatos(cuentas, ahora, zona, testers: testers).Generar();
        log.WriteLine($"Dataset: {datos.Clientes.Count} clientes, {datos.Pedidos.Count} pedidos, {datos.Pagos.Count} pagos, " +
                      $"{datos.Movimientos.Count} movimientos de inventario (zona {zona.Id}).");

        var opciones = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cn)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var ctx = new AppDbContext(opciones);
        await using var tx  = await ctx.Database.BeginTransactionAsync();

        log.WriteLine("Escribiendo (una sola transacción)...");
        await BaseRevision.PrepararEsquemaAsync(ctx, estado, log);
        await BaseRevision.MarcarAsync(ctx);
        await BaseRevision.LimpiarAsync(ctx);
        await BaseRevision.CargarAsync(ctx, datos, cuentas, log);
        await tx.CommitAsync();

        log.WriteLine();
        log.WriteLine("Listo. Base de revisión sembrada con datos sintéticos y marcada con _entorno='play_review'.");
        log.WriteLine("Las contraseñas no se imprimen: están en el JSON de credenciales (fuera del repositorio).");
    }
}

public sealed class Argumentos
{
    public string? RutaCredenciales { get; private set; }
    public string? Confirmar        { get; private set; }
    public bool    Reiniciar        { get; private set; }
    public bool    SinTesters       { get; private set; }   // TEMPORAL-TESTERS
    public bool    Ayuda            { get; private set; }

    public const string TextoAyuda =
        "Uso: dotnet run --project tools/PlayReviewSeed -- --credenciales <ruta json> [--confirmar <nombre_bd>] [--reiniciar] [--sin-testers]\n" +
        "  --sin-testers  no siembra las cuentas de testers de Google Play (@test.com) y borra las que existan.\n" +
        "  Variables: REVIEW_DB_URL (obligatoria), PROD_DB_HOST (opcional, guarda contra producción),\n" +
        "             REVIEW_LOGIN_EMAIL_DOMAIN (opcional, uno o varios separados por coma; por defecto @playreview.invalid),\n" +
        "             STORE_TIMEZONE (opcional).";

    public static Argumentos Parsear(string[] args)
    {
        var a = new Argumentos();
        for (var i = 0; i < args.Length; i++)
        {
            string Valor() => i + 1 < args.Length ? args[++i] : throw new GuardaException($"Falta el valor de {args[i]}.");
            switch (args[i])
            {
                case "--credenciales": a.RutaCredenciales = Valor(); break;
                case "--confirmar":    a.Confirmar        = Valor(); break;
                case "--reiniciar":    a.Reiniciar        = true;    break;
                case "--sin-testers":  a.SinTesters       = true;    break;
                case "--ayuda" or "-h" or "--help": a.Ayuda = true;  break;
                default: throw new GuardaException($"Opción desconocida: {args[i]}");
            }
        }
        if (!a.Ayuda && string.IsNullOrWhiteSpace(a.RutaCredenciales))
            throw new GuardaException("Falta --credenciales <ruta json>.");
        return a;
    }
}
