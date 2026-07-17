using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

public class TareaProgramadaStorageService
{
    private readonly string _rutaArchivo;

    public TareaProgramadaStorageService()
    {
        var carpetaConfig = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BackupSyncApp");

        Directory.CreateDirectory(carpetaConfig);
        _rutaArchivo = Path.Combine(carpetaConfig, "tareas_programadas.json");
    }

    public List<TareaProgramada> Cargar()
    {
        if (!File.Exists(_rutaArchivo))
        {
            return new List<TareaProgramada>();
        }

        var json = File.ReadAllText(_rutaArchivo);

        try
        {
            return JsonSerializer.Deserialize<List<TareaProgramada>>(json)
                   ?? new List<TareaProgramada>();
        }
        catch (JsonException)
        {
            return new List<TareaProgramada>();
        }
    }

    public void Guardar(List<TareaProgramada> tareas)
    {
        var opciones = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(tareas, opciones);
        File.WriteAllText(_rutaArchivo, json);
    }

    public TareaProgramada? BuscarPorId(string id)
    {
        return Cargar().FirstOrDefault(t => t.Id == id);
    }
}