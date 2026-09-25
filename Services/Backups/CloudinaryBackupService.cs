using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace FloreriaBautista.Services.Backups;

public class CloudinaryBackupService
{
    private readonly ILogger<CloudinaryBackupService> _logger;
    private readonly Lazy<Cloudinary> _cloudinary;

    public CloudinaryBackupService(ILogger<CloudinaryBackupService> logger)
    {
        _logger = logger;
        _cloudinary = new Lazy<Cloudinary>(() =>
        {
            var account = new Account(
                Env("CLOUDINARY_CLOUD_NAME"),
                Env("CLOUDINARY_API_KEY"),
                Env("CLOUDINARY_API_SECRET"));
            return new Cloudinary(account);
        });
    }

    public async Task<string> SubirArchivoAsync(string rutaArchivoLocal, string nombreArchivo)
    {
        var cloudinary = _cloudinary.Value;

        await using var stream = new FileStream(rutaArchivoLocal, FileMode.Open, FileAccess.Read);

        var uploadParams = new RawUploadParams
        {
            File   = new FileDescription(nombreArchivo, stream),
            Folder = "FloreriaBautista/Respaldos"
        };

        var result = await cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new Exception($"Error al subir a Cloudinary: {result.Error.Message}");

        _logger.LogInformation(
            "Backup subido a Cloudinary. PublicId: {PublicId} | Tamaño: {Bytes} bytes",
            result.PublicId, result.Bytes);

        return result.PublicId;
    }

    public async Task EliminarArchivoAsync(string publicId)
    {
        var cloudinary = _cloudinary.Value;

        var result = await cloudinary.DestroyAsync(
            new DeletionParams(publicId) { ResourceType = ResourceType.Raw });

        if (result.Result == "ok")
            _logger.LogInformation("Archivo eliminado de Cloudinary. PublicId: {Id}", publicId);
        else
            _logger.LogWarning("No se pudo eliminar de Cloudinary ({Id}): {Result}", publicId, result.Result);
    }

    public bool EstaConfigurado()
    {
        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME"))
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY"))
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET"));
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
