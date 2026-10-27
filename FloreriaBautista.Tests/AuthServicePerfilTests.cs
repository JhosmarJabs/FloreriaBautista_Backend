using FloreriaBautista.Data;
using FloreriaBautista.Models.Entities;
using FloreriaBautista.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloreriaBautista.Tests;

/// <summary>
/// El perfil propio (GET /api/users/me) debe reflejar User.EsResponsableTurno.
/// Crea su propio contexto InMemory.
/// </summary>
public class AuthServicePerfilTests
{
    private static (AuthService svc, AppDbContext ctx) Crear()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("perfil-" + Guid.NewGuid())
            .Options;
        var ctx = new AppDbContext(options);
        return (new AuthService(ctx, NullLogger<AuthService>.Instance), ctx);
    }

    private static User NuevoEmpleado(AppDbContext ctx, bool responsable)
    {
        var rol = new Role { Id = Guid.NewGuid(), Nombre = "EMPLEADO" };
        var user = new User
        {
            Id = Guid.NewGuid(),
            Nombre = "Emp",
            Apellido = "Test",
            Correo = $"emp-{Guid.NewGuid():N}@example.test",
            EsResponsableTurno = responsable,
        };
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = rol.Id, User = user, Role = rol });
        ctx.Roles.Add(rol);
        ctx.Users.Add(user);
        ctx.SaveChanges();
        return user;
    }

    [Fact]
    public async Task ObtenerPerfil_ResponsableDeTurno_DevuelveTrue()
    {
        var (svc, ctx) = Crear();
        var user = NuevoEmpleado(ctx, responsable: true);

        var perfil = await svc.ObtenerPerfilAsync(user.Id);

        Assert.True(perfil.EsResponsableTurno);
    }

    [Fact]
    public async Task ObtenerPerfil_NoResponsable_DevuelveFalse()
    {
        var (svc, ctx) = Crear();
        var user = NuevoEmpleado(ctx, responsable: false);

        var perfil = await svc.ObtenerPerfilAsync(user.Id);

        Assert.False(perfil.EsResponsableTurno);
    }
}
