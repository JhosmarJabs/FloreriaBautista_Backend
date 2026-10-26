namespace FloreriaBautista.Models.Entities;

public class PushTokenDevice
{
    public Guid     Id          { get; set; }
    public Guid     UserId      { get; set; }
    public string   Token       { get; set; } = string.Empty;
    public string   Platform    { get; set; } = "android"; // android / ios
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
