using FloreriaBautista.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace FloreriaBautista.Services;

public class PasswordService : IPasswordService
{
    public string HashPassword(string password)
    {
        // FIX: Se reemplazó el uso de MD5 por BCrypt para mayor seguridad (Resuelve Security Rating E)
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        // FIX: Verificación segura con BCrypt. Manejo de posibles excepciones si el hash es inválido.
        if (string.IsNullOrWhiteSpace(hashedPassword)) return false;
        try 
        {
            return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
        }
        catch
        {
            // En caso de que el hash anterior fuera MD5 o inválido durante el periodo de transición
            return false;
        }
    }
}
