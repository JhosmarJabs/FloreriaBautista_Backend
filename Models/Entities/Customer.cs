namespace FloreriaBautista.Models.Entities;

public class Customer
{
    public Guid     Id               { get; set; }
    public Guid?    UserId           { get; set; }
    public string   TipoCliente      { get; set; } = string.Empty; // FISICO / SESION / MOSTRADOR
    public string   Nombre           { get; set; } = string.Empty;
    public string?  Apellido         { get; set; }
    public string   Telefono         { get; set; } = string.Empty;
    public string?  Correo           { get; set; }
    public string?  Sexo             { get; set; }
    public DateOnly? FechaNacimiento { get; set; }
    public string?  Rfc              { get; set; }
    public string?  RazonSocial      { get; set; }
    public string?  CpFiscal         { get; set; }
    public string?  RegimenFiscal    { get; set; }
    public DateTime CreadoEn        { get; set; } = DateTime.UtcNow;

    public User?               User      { get; set; }
    public ICollection<Address> Addresses { get; set; } = [];
    public ICollection<Order>   Orders    { get; set; } = [];
}
