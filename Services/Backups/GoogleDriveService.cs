using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using FloreriaBautista.Models.DTOs.Backups;

namespace FloreriaBautista.Services.Backups;

/// <summary>
/// Cliente de Google Drive con OAuth2 (Desktop/Installed).
/// Primera vez: abre el navegador para autorizar → genera google_token.json
/// Siguientes veces: usa y renueva el token automáticamente.
/// Para servidores sin navegador: genera el token en PC local y copia google_token.json al servidor.
/// </summary>
public class GoogleDriveService
{
    private readonly ILogger<GoogleDriveService> _logger;
    private const string AppName = "FloreriaBautista Backups";
    private static readonly string[] Scopes = [DriveService.Scope.DriveFile];

    public GoogleDriveService(IConfiguration config, ILogger<GoogleDriveService> logger)
    {
        _logger = logger;
    }

    // ── Subir archivo ─────────────────────────────────────────────
    public async Task<string> SubirArchivoAsync(string rutaArchivoLocal, string nombreArchivo)
    {
        var drive    = await CrearDriveServiceAsync();
        var folderId = Env("GOOGLE_DRIVE_FOLDER_ID");

        var metadata = new Google.Apis.Drive.v3.Data.File
        {
            Name    = nombreArchivo,
            Parents = [folderId]
        };

        await using var stream = new FileStream(rutaArchivoLocal, FileMode.Open, FileAccess.Read);

        var request = drive.Files.Create(metadata, stream, "application/octet-stream");
        request.Fields = "id, name, size, webViewLink";

        var progress = await request.UploadAsync();

        if (progress.Status != Google.Apis.Upload.UploadStatus.Completed)
            throw new Exception($"Error al subir a Drive: {progress.Exception?.Message}");

        _logger.LogInformation("Archivo subido a Drive: {Nombre} | ID: {Id}",
            request.ResponseBody.Name, request.ResponseBody.Id);

        return request.ResponseBody.Id;
    }

    // ── Descargar archivo ─────────────────────────────────────────
    public async Task<string> DescargarArchivoAsync(string fileId, string carpetaDestino)
    {
        var drive = await CrearDriveServiceAsync();

        var metaRequest = drive.Files.Get(fileId);
        metaRequest.Fields = "name";
        var meta = await metaRequest.ExecuteAsync();

        var rutaDestino = Path.Combine(carpetaDestino, meta.Name ?? $"{fileId}.backup");
        Directory.CreateDirectory(carpetaDestino);

        await using var stream = new FileStream(rutaDestino, FileMode.Create, FileAccess.Write);
        var request = drive.Files.Get(fileId);
        await request.DownloadAsync(stream);

        _logger.LogInformation("Archivo descargado desde Drive: {Nombre} → {Ruta}",
            meta.Name, rutaDestino);

        return rutaDestino;
    }

    // ── Listar archivos en la carpeta de backups ──────────────────
    public async Task<List<DriveFileDto>> ListarArchivosAsync()
    {
        var drive    = await CrearDriveServiceAsync();
        var folderId = Env("GOOGLE_DRIVE_FOLDER_ID");

        var request = drive.Files.List();
        request.Q       = $"'{folderId}' in parents and trashed = false";
        request.Fields  = "files(id, name, size, createdTime, webViewLink)";
        request.OrderBy = "createdTime desc";

        var result = await request.ExecuteAsync();

        return result.Files.Select(f => new DriveFileDto
        {
            Id          = f.Id,
            Nombre      = f.Name,
            TamanoBytes = f.Size,
            CreadoEn    = f.CreatedTimeDateTimeOffset?.UtcDateTime,
            Enlace      = f.WebViewLink
        }).ToList();
    }

    // ── Crear cliente OAuth2 ──────────────────────────────────────
    private static async Task<DriveService> CrearDriveServiceAsync()
    {
        var credPath  = Env("GOOGLE_CREDENTIALS_PATH");
        var tokenPath = Env("GOOGLE_TOKEN_PATH");

        if (!File.Exists(credPath))
            throw new FileNotFoundException(
                $"No se encontró google_credentials.json en: {Path.GetFullPath(credPath)}");

        UserCredential credential;
        await using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
        {
            var tokenDir = Path.GetDirectoryName(Path.GetFullPath(tokenPath)) ?? ".";
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                user: "floreria",
                CancellationToken.None,
                new FileDataStore(tokenDir, fullPath: true));
        }

        return new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName       = AppName
        });
    }

    private static string Env(string key) =>
        Environment.GetEnvironmentVariable(key)
        ?? throw new InvalidOperationException($"Variable '{key}' no configurada en .env");
}
