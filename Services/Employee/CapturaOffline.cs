using FloreriaBautista.Models.Exceptions;

namespace FloreriaBautista.Services.Employee;

/// <summary>
/// Validación del instante de captura que manda el dispositivo cuando una venta o
/// un gasto se registró sin conexión y llega después por la cola de sincronización.
///
/// El dato lo pone el cliente, así que el servidor no lo toma como autoridad: lo
/// acota a una ventana razonable. Una captura de hace más de 48 h ya no cuadra con
/// ningún corte que el empleado pueda revisar, y una captura en el futuro solo
/// puede salir de un reloj mal puesto (se toleran unos minutos de desfase).
/// </summary>
public static class CapturaOffline
{
    public static readonly TimeSpan Ventana         = TimeSpan.FromHours(48);
    public static readonly TimeSpan ToleranciaReloj = TimeSpan.FromMinutes(5);

    /// <summary>Devuelve la captura en UTC o lanza <see cref="AppException"/> (400)
    /// si cae fuera de la ventana.</summary>
    public static DateTime Validar(DateTime capturadoEn, DateTime ahoraUtc)
    {
        var captura = capturadoEn.Kind switch
        {
            DateTimeKind.Local       => capturadoEn.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(capturadoEn, DateTimeKind.Utc),
            _                        => capturadoEn
        };

        if (captura > ahoraUtc + ToleranciaReloj)
            throw new AppException(
                "La fecha de captura está en el futuro. Revisa la fecha y hora del dispositivo.");

        if (captura < ahoraUtc - Ventana)
            throw new AppException(
                "Este registro se capturó hace más de 48 horas y ya no puede sincronizarse. " +
                "Repórtalo al administrador.");

        return captura;
    }
}
