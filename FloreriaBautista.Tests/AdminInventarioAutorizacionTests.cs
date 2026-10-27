using System.Reflection;
using FloreriaBautista.Controllers.Admin;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// Registrar un movimiento de inventario cambia el stock, así que es solo de ADMIN
/// (decisión del usuario, 2026-10-25). El EMPLEADO conserva las lecturas: por eso la
/// clase sigue en "ADMIN,EMPLEADO" y la restricción vive en la acción.
/// </summary>
public class AdminInventarioAutorizacionTests
{
    [Fact]
    public void RegistrarMovimiento_exige_solo_ADMIN()
    {
        var accion = typeof(AdminInventoryController).GetMethod(nameof(AdminInventoryController.RegistrarMovimiento));

        Assert.NotNull(accion);
        var atributo = accion!.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(atributo);
        Assert.Equal("ADMIN", atributo!.Roles);
    }

    [Fact]
    public void Clase_conserva_ADMIN_y_EMPLEADO_para_las_lecturas()
    {
        var atributo = typeof(AdminInventoryController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal("ADMIN,EMPLEADO", atributo!.Roles);
    }
}
