using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

public class ConexionBackupContaboStorageService
{
    private readonly string _rutaArchivo;

    public ConexionBackupContaboStorageService()
    {
        var carpetaConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BackupSyncApp");

        Directory.CreateDirectory(carpetaConfig);
        _rutaArchivo = Path.Combine(carpetaConfig, "conexiones_backup_contabo.json");
    }

    public List<ConexionBackupContabo> Cargar()
    {
        if (!File.Exists(_rutaArchivo))
        {
            return new List<ConexionBackupContabo>();
        }

        var json = File.ReadAllText(_rutaArchivo);
        List<ConexionBackupContabo> conexiones;

        try
        {
            conexiones = JsonSerializer.Deserialize<List<ConexionBackupContabo>>(json)
                         ?? new List<ConexionBackupContabo>();
        }
        catch (JsonException)
        {
            return new List<ConexionBackupContabo>();
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

    public void Guardar(List<ConexionBackupContabo> conexiones)
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