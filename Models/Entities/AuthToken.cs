namespace FloreriaBautista.Models.Entities;

public class AuthToken
{
    public Guid     Id        { get; set; }
    public Guid     UserId    { get; set; }
    public string   Token     { get; set; } = string.Empty;
    public string   Tipo      { get; set; } = string.Empty; // REFRESH / PASSWORD_RESET / EMAIL_VERIFY
    public DateTime ExpiraEn  { get; set; }
    public bool     Usado     { get; set; } = false;
    public DateTime CreadoEn  { get; set; } = DateTime.UtcNow;

    // Renovación idempotente (sesion-renovacion-idempotente). Todas nulas: los tokens
    // anteriores a la migración quedan sin familia ni motivo de cierre.
    public Guid?     FamiliaId   { get; set; }  // cadena de rotación (sesión de un navegador/dispositivo)
    public Guid?     SucesorId   { get; set; }  // token emitido al rotar este (sin FK)
    public DateTime? CerradoEn   { get; set; }  // momento de cierre (inicio de la ventana si es ROTADO)
    public string?   MotivoCierre { get; set; } // ROTADO / LOGOUT / LOGOUT_TODAS / RESET_PASSWORD / CUENTA_INACTIVA / REUSO_DETECTADO

    public User User { get; set; } = null!;
}
