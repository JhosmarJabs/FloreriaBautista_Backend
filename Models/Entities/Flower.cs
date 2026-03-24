namespace FloreriaBautista.Models.Entities;

public class Flower
{
    public Guid     Id             { get; set; }
    public string   Nombre         { get; set; } = string.Empty;
    public string   Color          { get; set; } = string.Empty;
    public decimal  PrecioCosto    { get; set; }
    public string   UnidadMedida   { get; set; } = "TALLO";
    public bool     EsFlorPrimaria { get; set; } = false;
    public int      StockActual    { get; set; }
    public int      StockMinimo    { get; set; }
    public string   Estado         { get; set; } = "ACTIVA";
    public DateTime CreadoEn      { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public ICollection<FlowerMovement>  FlowerMovements  { get; set; } = [];
    public ICollection<ProductFlower>   ProductFlowers   { get; set; } = [];
}
