namespace FloreriaBautista.Models.DTOs.Analytics;

public class CustomerSegmentGroupDto
{
    public string Grupo               { get; set; } = string.Empty; // VIP / FRECUENTE / OCASIONAL / INACTIVO
    public int    TotalClientes       { get; set; }
    public decimal MontoTotalGrupo    { get; set; }
    public decimal MontoPromedio      { get; set; }
    public double RecenciaPromedioDias { get; set; }
    public double FrecuenciaPromedio  { get; set; }

    // Resumen demográfico del grupo (columnas de importancia 2 del dataset de la
    // Propuesta 3) — útil para dirigir campañas de marketing por segmento.
    public double? EdadPromedio             { get; set; }
    public double  TasaCancelacionPromedio  { get; set; }
    public string? ColoniaMasComun          { get; set; }
    public string? CategoriaFavoritaMasComun { get; set; }
    public string? CanalPreferidoMasComun   { get; set; }

    public List<CustomerSegmentDetailDto> Clientes { get; set; } = [];
}

public class CustomerSegmentDetailDto
{
    public Guid   CustomerId         { get; set; }
    public string Nombre             { get; set; } = string.Empty;
    public string? Telefono          { get; set; }
    public string? Correo            { get; set; }
    public int    RecenciaDias       { get; set; }
    public int    FrecuenciaPedidos  { get; set; }
    public decimal MontoTotal        { get; set; }

    // Demográficos por cliente (importancia 2, Propuesta 3).
    public int?    Edad                     { get; set; }
    public string? Sexo                     { get; set; }
    public string? Colonia                  { get; set; }
    public string? CategoriaFavorita        { get; set; }
    public string? CanalPreferido           { get; set; }
    public double  TasaCancelacion          { get; set; }
    public int     AntiguedadDias           { get; set; }
    public double? DiasPromedioEntrePedidos { get; set; }
}

public class RecalcularSegmentosResultDto
{
    public int ClientesProcesados { get; set; }
    public int GruposGenerados    { get; set; }
    public DateTime CalculadoEn   { get; set; }
}
