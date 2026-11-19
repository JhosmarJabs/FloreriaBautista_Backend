using Microsoft.Extensions.Caching.Memory;

namespace FloreriaBautista.Services;

/// <summary>
/// Limita los intentos fallidos de cupón por usuario: máximo <see cref="MaxFallos"/>
/// en una ventana de <see cref="Ventana"/> contada desde el primer fallo.
/// El contador vive en memoria del proceso (una sola instancia del backend).
/// </summary>
public class CuponIntentosLimiter
{
    public const int MaxFallos = 10;
    public static readonly TimeSpan Ventana = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    public CuponIntentosLimiter(IMemoryCache cache) => _cache = cache;

    private sealed class Contador
    {
        public int Fallos;
    }

    private static string Clave(Guid usuarioId) => $"cupon-fallos:{usuarioId}";

    public bool EstaBloqueado(Guid usuarioId)
        => _cache.TryGetValue(Clave(usuarioId), out Contador? c) && c!.Fallos >= MaxFallos;

    public void RegistrarFallo(Guid usuarioId)
    {
        lock (_lock)
        {
            var clave = Clave(usuarioId);
            if (!_cache.TryGetValue(clave, out Contador? c) || c == null)
            {
                c = new Contador();
                _cache.Set(clave, c, Ventana); // expira 15 min después del primer fallo
            }
            c.Fallos++;
        }
    }
}
