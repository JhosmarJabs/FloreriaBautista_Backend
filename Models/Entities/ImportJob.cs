namespace FloreriaBautista.Models.Entities;

public class ImportJob
{
    public Guid      Id               { get; set; }
    public string    TipoImportacion  { get; set; } = string.Empty; // PRODUCTS / INVENTORY
    public string    Estado           { get; set; } = "PENDIENTE";
    public Guid?     UsuarioId        { get; set; }
    public DateTime  CreadoEn        { get; set; } = DateTime.UtcNow;
    public DateTime? CompletadoEn    { get; set; }
    public string?   Resumen          { get; set; } // JSON con totales

    public User? Usuario { get; set; }
}
