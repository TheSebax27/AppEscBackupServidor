using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Igual que ConexionStorageService, pero para las conexiones de "Sacar backups"
/// (archivo JSON separado para no mezclarlas con las de "Pasar archivos").
/// </summary>
public class ConexionBackupSqlStorageService
{
    private readonly string _rutaArchivo;

    public ConexionBackupSqlStorageService()
    {
        var carpetaConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BackupSyncApp");

        Directory.CreateDirectory(carpetaConfig);
        _rutaArchivo = Path.Combine(carpetaConfig, "conexiones_backup_sql.json");
    }

    public List<ConexionBackupSql> Cargar()
    {
        if (!File.Exists(_rutaArchivo))
        {
            return new List<ConexionBackupSql>();
        }

        var json = File.ReadAllText(_rutaArchivo);
        List<ConexionBackupSql> conexiones;

        try
        {
            conexiones = JsonSerializer.Deserialize<List<ConexionBackupSql>>(json)
                         ?? new List<ConexionBackupSql>();
        }
        catch (JsonException)
        {
            return new List<ConexionBackupSql>();
        }

        bool huboCambios = false;
        foreach (var conexion in conexiones)
        {
            if (string.IsNullOrEmpty(conexion.Id))
            {
                conexion.Id = Guid.NewGuid().ToString();
                huboCambios = true;
            }
        }
        if (huboCambios)
        {
            Guardar(conexiones);
        }

        return conexiones;
    }

    public void Guardar(List<ConexionBackupSql> conexiones)
    {
        foreach (var conexion in conexiones)
        {
            if (string.IsNullOrEmpty(conexion.Id))
            {
                conexion.Id = Guid.NewGuid().ToString();
            }
        }

        var opciones = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(conexiones, opciones);
        File.WriteAllText(_rutaArchivo, json);
    }
}