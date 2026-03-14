namespace FloreriaBautista.Models.DTOs.Database;

/// <summary>Estado de conexión y salud de la base de datos.</summary>
public class HealthResponseDto
{
    public bool     Conectado        { get; set; }
    public string   Estado           { get; set; } = string.Empty; // OK | ERROR
    public string   BaseDatos        { get; set; } = string.Empty;
    public string   Servidor         { get; set; } = string.Empty;
    public string   VersionPostgres  { get; set; } = string.Empty;
    public int      ConexionesActivas { get; set; }
    public int      ConexionesMaximas { get; set; }
    public string   TiempoRespuesta  { get; set; } = string.Empty; // ms
    public string   TiempoActividad  { get; set; } = string.Empty; // uptime del servidor
    public string?  MensajeError     { get; set; }
    public DateTime ConsultadoEn    { get; set; } = DateTime.UtcNow;
}
