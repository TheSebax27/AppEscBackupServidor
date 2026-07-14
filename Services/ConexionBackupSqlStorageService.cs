using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

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

        try
        {
            return JsonSerializer.Deserialize<List<ConexionBackupSql>>(json)
                   ?? new List<ConexionBackupSql>();
        }
        catch (JsonException)
        {
            return new List<ConexionBackupSql>();
        }
    }

    public void Guardar(List<ConexionBackupSql> conexiones)
    {
        var opciones = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(conexiones, opciones);
        File.WriteAllText(_rutaArchivo, json);
    }
}