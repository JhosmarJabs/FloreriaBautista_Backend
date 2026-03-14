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

    public User User { get; set; } = null!;
}
