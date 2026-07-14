using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services
{
    public class ConexionRestauracionStorageService
    {

        private readonly string _rutaArchivo;

        public ConexionRestauracionStorageService()
        {
            var carpetaConfig = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BackupSyncApp");
            System.IO.Directory.CreateDirectory(carpetaConfig);
            _rutaArchivo = System.IO.Path.Combine(carpetaConfig, "conexiones_restauracion.json");
        }

        public List<Models.ConexionRestauracion> Cargar()
        {
            if (!File.Exists(_rutaArchivo))
            {
                return new List<ConexionRestauracion>();
            }
            var json = File.ReadAllText(_rutaArchivo);
            try
            {
                return JsonSerializer.Deserialize<List<ConexionRestauracion>>(json)
                       ?? new List<ConexionRestauracion>();
            }
            catch (JsonException)
            {
                return new List<Models.ConexionRestauracion>();
            }
        }

        public void Guardar(List<Models.ConexionRestauracion> conexiones)
        {
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(conexiones, opciones);
            File.WriteAllText(_rutaArchivo, json);
        }

    }
}
