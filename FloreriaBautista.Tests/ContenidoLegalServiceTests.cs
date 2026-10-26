using FloreriaBautista.Data;
using FloreriaBautista.Models.DTOs.Cms;
using FloreriaBautista.Models.Exceptions;
using FloreriaBautista.Services;
using FloreriaBautista.Services.Audit;
using FloreriaBautista.Services.Cms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Páginas legales del CMS: creación perezosa sin duplicados, slugs fijos, validación del PUT,
/// que el público no vea las notas de revisión y la validación de los datos legales del negocio.
/// </summary>
public class ContenidoLegalServiceTests
{
    private static AppDbContext NuevaBd() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"legal-{Guid.NewGuid()}")
            .Options);

    private static ContenidoLegalService Crear(AppDbContext db) =>
        new(db, new AuditService(db, NullLogger<AuditService>.Instance));

    private static ActualizarDocumentoLegalDto Valido(params (string t, string c)[] secciones) => new()
    {
        Titulo = "Términos y condiciones",
        Secciones = secciones.Select(s => new SeccionLegalDto { Titulo = s.t, Cuerpo = s.c }).ToList(),
        NotasRevision = "Revisar con asesor",
        EsProvisional = false
    };

    [Fact]
    public async Task Obtener_Con_Tabla_Vacia_Crea_Los_5_Documentos_En_Orden()
    {
        using var db = NuevaBd();
        var res = await Crear(db).ObtenerPublicoAsync();

        Assert.Equal(ContenidoLegalPorDefecto.Slugs, res.Documentos.Select(d => d.Slug).ToList());
        Assert.All(res.Documentos, d => Assert.NotEmpty(d.Secciones));
        Assert.Equal(5, await db.ContenidoLegal.CountAsync());
        Assert.All(await db.ContenidoLegal.ToListAsync(), f => Assert.True(f.EsProvisional));
    }

    [Fact]
    public async Task Segunda_Lectura_No_Duplica_Ni_Pisa_Lo_Editado()
    {
        using var db = NuevaBd();
        var svc = Crear(db);
        await svc.ObtenerPublicoAsync();
        await svc.ActualizarAsync("terminos-y-condiciones", Valido(("Uno", "Texto uno"), ("Dos", "Texto dos")), null);

        var res = await svc.ObtenerPublicoAsync();

        Assert.Equal(5, await db.ContenidoLegal.CountAsync());
        var terminos = res.Documentos.Single(d => d.Slug == "terminos-y-condiciones");
        Assert.Equal(2, terminos.Secciones.Count);
        Assert.Equal("Texto uno", terminos.Secciones[0].Cuerpo);
    }

    [Fact]
    public async Task Con_Filas_Parciales_Solo_Crea_Las_Que_Faltan()
    {
        using var db = NuevaBd();
        var svc = Crear(db);
        await svc.ObtenerPublicoAsync();
        db.ContenidoLegal.Remove(await db.ContenidoLegal.SingleAsync(f => f.Slug == "envios-y-entregas"));
        await db.SaveChangesAsync();

        var res = await svc.ObtenerPublicoAsync();

        Assert.Equal(5, res.Documentos.Count);
        Assert.Equal(5, await db.ContenidoLegal.CountAsync());
    }

    [Fact]
    public void El_Dto_Publico_No_Expone_Notas_Ni_Bandera_Ni_Editor()
    {
        var props = typeof(DocumentoLegalPublicoDto).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("NotasRevision", props);
        Assert.DoesNotContain("EsProvisional", props);
        Assert.DoesNotContain("ActualizadoPorUsuarioId", props);
        Assert.DoesNotContain("ActualizadoPorNombre", props);
        Assert.Equal(typeof(ContenidoLegalPublicoDto).GetProperty("Documentos")!.PropertyType,
            typeof(List<DocumentoLegalPublicoDto>));
    }

    [Fact]
    public async Task Admin_Ve_Notas_De_Revision_Y_Provisional()
    {
        using var db = NuevaBd();
        var docs = await Crear(db).ObtenerAdminAsync();

        Assert.Equal(5, docs.Count);
        Assert.All(docs, d => Assert.True(d.EsProvisional));
        Assert.All(docs, d => Assert.False(string.IsNullOrWhiteSpace(d.NotasRevision)));
    }

    [Fact]
    public async Task Actualizar_Guarda_Audita_Y_Marca_El_Usuario()
    {
        using var db = NuevaBd();
        var usuario = Guid.NewGuid();
        db.Users.Add(new FloreriaBautista.Models.Entities.User { Id = usuario, Nombre = "Ana", Apellido = "Admin" });
        await db.SaveChangesAsync();
        var svc = Crear(db);
        var antes = (await svc.ObtenerAdminAsync()).Single(d => d.Slug == "terminos-y-condiciones").ActualizadoEn;
        await Task.Delay(20);

        var res = await svc.ActualizarAsync("terminos-y-condiciones",
            Valido(("Uno", "Hola\r\nmundo"), ("Dos", "- a\n- b"), ("", "")), usuario);

        Assert.Equal(2, res.Secciones.Count);              // la sección vacía se descarta
        Assert.Equal("Hola\nmundo", res.Secciones[0].Cuerpo); // \r\n normalizado
        Assert.False(res.EsProvisional);
        Assert.True(res.ActualizadoEn > antes);
        Assert.Equal("Ana Admin", res.ActualizadoPorNombre);
        var log = await db.AuditLogs.SingleAsync(l => l.Accion == "CMS_LEGAL_ACTUALIZAR");
        Assert.Equal("ContenidoLegal", log.Entidad);
        Assert.Equal("terminos-y-condiciones", log.EntidadId);
        Assert.Equal(usuario, log.UsuarioId);
        Assert.DoesNotContain("Hola", log.Detalles ?? "");
    }

    [Theory]
    [InlineData("politica-de-cookies")]
    [InlineData("Terminos-Y-Condiciones")]
    [InlineData(" terminos-y-condiciones")]
    [InlineData("")]
    public async Task Slug_No_Permitido_Da_404_Y_No_Crea_Filas(string slug)
    {
        using var db = NuevaBd();
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Crear(db).ActualizarAsync(slug, Valido(("Uno", "Texto")), null));
        Assert.Equal(0, await db.ContenidoLegal.CountAsync());
    }

    [Fact]
    public async Task Validaciones_Rechazan_Y_No_Cambian_El_Documento()
    {
        using var db = NuevaBd();
        var svc = Crear(db);
        var original = (await svc.ObtenerAdminAsync()).Single(d => d.Slug == "terminos-y-condiciones");

        var casos = new List<ActualizarDocumentoLegalDto>
        {
            new() { Titulo = "  ", Secciones = [new() { Titulo = "A", Cuerpo = "B" }] },
            new() { Titulo = "ab", Secciones = [new() { Titulo = "A", Cuerpo = "B" }] },
            new() { Titulo = new string('x', 151), Secciones = [] },
            new() { Titulo = "Válido", Secciones = Enumerable.Range(0, 41).Select(i => new SeccionLegalDto { Titulo = $"T{i}", Cuerpo = "c" }).ToList() },
            new() { Titulo = "Válido", Secciones = [new() { Titulo = "A", Cuerpo = new string('x', 8_001) }] },
            new() { Titulo = "Válido", Secciones = [new() { Titulo = new string('x', 201), Cuerpo = "c" }] },
            new() { Titulo = "Válido", Secciones = [new() { Titulo = "A", Cuerpo = "Escríbenos al {{whatsapp}}" }] },
            new() { Titulo = "Válido", Secciones = [new() { Titulo = "A", Cuerpo = "" }] },
            new() { Titulo = "Válido", Secciones = [new() { Titulo = "", Cuerpo = "algo" }] },
            new() { Titulo = "Válido", Secciones = [], NotasRevision = new string('n', 4_001) },
            new() { Titulo = "Válido", Secciones = Enumerable.Range(0, 8).Select(i => new SeccionLegalDto { Titulo = $"T{i}", Cuerpo = new string('x', 7_900) }).ToList() },
        };

        foreach (var caso in casos)
            await Assert.ThrowsAsync<AppException>(() => svc.ActualizarAsync("terminos-y-condiciones", caso, null));

        var despues = (await svc.ObtenerAdminAsync()).Single(d => d.Slug == "terminos-y-condiciones");
        Assert.Equal(original.Titulo, despues.Titulo);
        Assert.Equal(original.Secciones.Count, despues.Secciones.Count);
        Assert.Equal(original.ActualizadoEn, despues.ActualizadoEn);
        Assert.Empty(db.AuditLogs);
    }

    [Fact]
    public async Task Variables_Conocidas_Se_Aceptan_Con_Espacios_Internos()
    {
        using var db = NuevaBd();
        var res = await Crear(db).ActualizarAsync("preguntas-frecuentes",
            Valido(("¿Dónde están?", "En {{ direccion }}. Llama al {{telefono}} o escribe a {{correoPrivacidad}}.")), null);
        Assert.Single(res.Secciones);
    }

    // ── Datos legales en el CMS ───────────────────────────────────

    private static SiteSettingsDto Ajustes(string? rfc = null, string? correo = null, string? razon = null) => new()
    {
        NombreTienda = "Florería Bautista",
        Rfc = rfc,
        CorreoPrivacidad = correo,
        RazonSocial = razon
    };

    [Theory]
    [InlineData("abc")]
    [InlineData("BAUJ8001")]
    [InlineData("BAUJ800101AB11")]
    public async Task Cms_Rfc_Invalido_Se_Rechaza(string rfc)
    {
        using var db = NuevaBd();
        await Assert.ThrowsAsync<AppException>(() => new CmsService(db).ActualizarAsync(Ajustes(rfc: rfc)));
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@b")]
    public async Task Cms_Correo_Privacidad_Invalido_Se_Rechaza(string correo)
    {
        using var db = NuevaBd();
        await Assert.ThrowsAsync<AppException>(() => new CmsService(db).ActualizarAsync(Ajustes(correo: correo)));
    }

    [Fact]
    public async Task Cms_Rfc_Valido_Se_Guarda_En_Mayusculas_Y_Vacio_Lo_Borra_Y_Null_Lo_Conserva()
    {
        using var db = NuevaBd();
        var svc = new CmsService(db);

        var r1 = await svc.ActualizarAsync(Ajustes(rfc: " bauj800101ab1 ", correo: "privacidad@floreria.mx", razon: "Florería Bautista S.A. de C.V."));
        Assert.Equal("BAUJ800101AB1", r1.Rfc);
        Assert.Equal("privacidad@floreria.mx", r1.CorreoPrivacidad);

        var r2 = await svc.ActualizarAsync(Ajustes());          // los 3 campos no vienen: se conservan
        Assert.Equal("BAUJ800101AB1", r2.Rfc);
        Assert.Equal("Florería Bautista S.A. de C.V.", r2.RazonSocial);

        var r3 = await svc.ActualizarAsync(Ajustes(rfc: "", correo: "", razon: ""));  // "" = vaciar
        Assert.Null(r3.Rfc);
        Assert.Null(r3.CorreoPrivacidad);
        Assert.Null(r3.RazonSocial);
        Assert.Null((await svc.ObtenerAsync()).Rfc);
    }
}
