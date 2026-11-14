namespace FloreriaBautista.Services.Auth;

/// <summary>Decisión de la política de renovación de un refresh token.</summary>
public enum DecisionRenovacion
{
    /// <summary>Canjear el token: crear sucesor y cerrar el presentado como ROTADO.</summary>
    Rotar,
    /// <summary>Token ya rotado dentro de la ventana: devolver el mismo sucesor.</summary>
    Reintentar,
    /// <summary>401 sin efectos secundarios sobre la familia.</summary>
    Rechazar,
    /// <summary>Token rotado presentado fuera de la ventana: 401 y revocar la familia.</summary>
    ReusoDetectado
}

/// <summary>Datos mínimos de un token ya leído de la BD (la política no accede a la BD).</summary>
public sealed record TokenRenovacion(
    string    Tipo,
    Guid      UserId,
    DateTime  ExpiraEn,
    bool      Usado,
    string?   MotivoCierre,
    DateTime? CerradoEn);

/// <summary>
/// Política pura de la renovación idempotente (plan sesion-renovacion-idempotente, Paso 4).
/// Sin BD ni reloj propio: recibe <c>ahora</c>.
/// </summary>
public static class PoliticaRenovacion
{
    public const string Rotado          = "ROTADO";
    public const string Logout          = "LOGOUT";
    public const string LogoutTodas     = "LOGOUT_TODAS";
    public const string ResetPassword   = "RESET_PASSWORD";
    public const string CuentaInactiva  = "CUENTA_INACTIVA";
    public const string ReusoDetectado  = "REUSO_DETECTADO";

    public const string TipoRefresh = "REFRESH";

    public static DecisionRenovacion Decidir(
        TokenRenovacion? presentado,
        TokenRenovacion? sucesor,
        bool             usuarioActivo,
        DateTime         ahora,
        int              ventanaSegundos,
        bool             deteccionActiva)
    {
        // 1. Inexistente o de otro tipo
        if (presentado == null || presentado.Tipo != TipoRefresh)
            return DecisionRenovacion.Rechazar;

        // 2. Vencido (no se marca nada)
        if (presentado.ExpiraEn <= ahora)
            return DecisionRenovacion.Rechazar;

        // 3. Cuenta no activa
        if (!usuarioActivo)
            return DecisionRenovacion.Rechazar;

        // 4. Activo
        if (!presentado.Usado)
            return DecisionRenovacion.Rotar;

        // 5. Cerrado por otro motivo (incluye null: cerrado antes de esta tarea)
        if (presentado.MotivoCierre != Rotado)
            return DecisionRenovacion.Rechazar;

        // 6/7. Rotado dentro de la ventana
        var dentroDeVentana = ventanaSegundos > 0
            && presentado.CerradoEn.HasValue
            && (ahora - presentado.CerradoEn.Value).TotalSeconds <= ventanaSegundos;

        if (dentroDeVentana)
        {
            return SucesorActivo(presentado, sucesor, ahora)
                ? DecisionRenovacion.Reintentar
                : DecisionRenovacion.Rechazar;
        }

        // 8. Rotado fuera de la ventana (o ventana = 0, o sin CerradoEn)
        return deteccionActiva ? DecisionRenovacion.ReusoDetectado : DecisionRenovacion.Rechazar;
    }

    private static bool SucesorActivo(TokenRenovacion presentado, TokenRenovacion? sucesor, DateTime ahora) =>
        sucesor != null
        && sucesor.Tipo == TipoRefresh
        && !sucesor.Usado
        && sucesor.ExpiraEn > ahora
        && sucesor.UserId == presentado.UserId;
}
