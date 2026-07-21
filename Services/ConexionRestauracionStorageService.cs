using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

public class ConexionRestauracionStorageService
{
    private readonly string _rutaArchivo;

    public ConexionRestauracionStorageService()
    {
        var carpetaConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BackupSyncApp");

        Directory.CreateDirectory(carpetaConfig);
        _rutaArchivo = Path.Combine(carpetaConfig, "conexiones_restauracion.json");
    }

    public List<ConexionRestauracion> Cargar()
    {
        if (!File.Exists(_rutaArchivo))
        {
            return new List<ConexionRestauracion>();
        }

        var json = File.ReadAllText(_rutaArchivo);
        List<ConexionRestauracion> conexiones;

        try
        {
            conexiones = JsonSerializer.Deserialize<List<ConexionRestauracion>>(json)
                         ?? new List<ConexionRestauracion>();
        }
        catch (JsonException)
        {
            return new List<ConexionRestauracion>();
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

    public void Guardar(List<ConexionRestauracion> conexiones)
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