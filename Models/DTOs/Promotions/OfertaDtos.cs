namespace FloreriaBautista.Models.DTOs.Promotions;

public class OfertaDto
{
    public Guid      Id            { get; set; }
    public Guid      ProductoId    { get; set; }
    public string    ProductoNombre { get; set; } = string.Empty;
    public string?   ProductoImagen { get; set; }
    public decimal   PrecioBase     { get; set; }
    public decimal   PrecioOferta  { get; set; }
    public DateOnly? FechaInicio   { get; set; }
    public DateOnly? FechaFin      { get; set; }
    public bool      Activo        { get; set; }
    public DateTime  CreadoEn      { get; set; }
}

public class SaveOfertaRequestDto
{
    public Guid      ProductoId   { get; set; }
    public decimal   PrecioOferta { get; set; }
    public DateOnly? FechaInicio  { get; set; }
    public DateOnly? FechaFin     { get; set; }
    public bool      Activo       { get; set; } = true;
}

public class OfertaPublicaDto
{
    public Guid     Id             { get; set; }
    public Guid     ProductoId     { get; set; }
    public string   ProductoNombre { get; set; } = string.Empty;
    public string?  ProductoImagen { get; set; }
    public string   ProductoTipo   { get; set; } = string.Empty;
    public decimal  PrecioBase     { get; set; }
    public decimal  PrecioOferta   { get; set; }
    public int      PorcentajeDesc { get; set; }
    public DateOnly? FechaFin      { get; set; }
}
