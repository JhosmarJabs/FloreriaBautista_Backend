namespace FloreriaBautista.Models.DTOs.Analytics;

public class CustomerSegmentGroupDto
{
    public string Grupo               { get; set; } = string.Empty; // VIP / FRECUENTE / OCASIONAL / INACTIVO
    public int    TotalClientes       { get; set; }
    public decimal MontoTotalGrupo    { get; set; }
    public decimal MontoPromedio      { get; set; }
    public double RecenciaPromedioDias { get; set; }
    public double FrecuenciaPromedio  { get; set; }
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
}

public class RecalcularSegmentosResultDto
{
    public int ClientesProcesados { get; set; }
    public int GruposGenerados    { get; set; }
    public DateTime CalculadoEn   { get; set; }
}
