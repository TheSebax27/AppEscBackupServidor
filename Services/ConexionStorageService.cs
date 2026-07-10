using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace BackupSyncApp.Services
{
    public class ConexionStorageService
    {

        private readonly string _rutaArchivo;

        public ConexionStorageService()
        {
            var carpetaConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BackupSyncApp");

            Directory.CreateDirectory(carpetaConfig);
            _rutaArchivo = Path.Combine(carpetaConfig, "conexiones.json");
        }

        public List<Models.ConexionBackup> Cargar()
        {
            if (!File.Exists(_rutaArchivo))
            {
                return new List<Models.ConexionBackup>();
            }
            var json = File.ReadAllText(_rutaArchivo);

            try
            {

                return System.Text.Json.JsonSerializer.Deserialize<List<Models.ConexionBackup>>(json) ?? new List<Models.ConexionBackup>();


            }
            catch (JsonException)
            {

                return new List<Models.ConexionBackup>();

            }
        }

        public void Guardar(List<Models.ConexionBackup> conexiones)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(conexiones, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_rutaArchivo, json);
        }

    }
}
