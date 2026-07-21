using System.IO;
using System.Text.Json;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Junta las 5 listas de configuración en un solo archivo (Exportar) y las
/// vuelve a repartir en sus respectivos JSON al importar, ya sea
/// reemplazando todo o combinando con lo que ya existía en este equipo.
/// </summary>
public class ConfiguracionExportService
{
    public void Exportar(string rutaArchivo)
    {
        var config = new ConfiguracionExportada
        {
            ConexionesArchivos = new ConexionStorageService().Cargar(),
            ConexionesBackupSql = new ConexionBackupSqlStorageService().Cargar(),
            ConexionesRestauracion = new ConexionRestauracionStorageService().Cargar(),
            ConexionesContabo = new ConexionBackupContaboStorageService().Cargar(),
            TareasProgramadas = new TareaProgramadaStorageService().Cargar()
        };

        var opciones = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(config, opciones);
        File.WriteAllText(rutaArchivo, json);
    }

    public ResultadoImportacion Importar(string rutaArchivo, bool reemplazarTodo)
    {
        var json = File.ReadAllText(rutaArchivo);
        var config = JsonSerializer.Deserialize<ConfiguracionExportada>(json)
            ?? throw new InvalidOperationException("El archivo no tiene un formato válido o está vacío.");

        var resultado = new ResultadoImportacion();

        var storageArchivos = new ConexionStorageService();
        resultado.NuevasArchivos = CombinarOReemplazar(
            storageArchivos.Cargar(), config.ConexionesArchivos, reemplazarTodo,
            c => c.Id, storageArchivos.Guardar);

        var storageSql = new ConexionBackupSqlStorageService();
        resultado.NuevasBackupSql = CombinarOReemplazar(
            storageSql.Cargar(), config.ConexionesBackupSql, reemplazarTodo,
            c => c.Id, storageSql.Guardar);

        var storageRestauracion = new ConexionRestauracionStorageService();
        resultado.NuevasRestauracion = CombinarOReemplazar(
            storageRestauracion.Cargar(), config.ConexionesRestauracion, reemplazarTodo,
            c => c.Id, storageRestauracion.Guardar);

        var storageContabo = new ConexionBackupContaboStorageService();
        resultado.NuevasContabo = CombinarOReemplazar(
            storageContabo.Cargar(), config.ConexionesContabo, reemplazarTodo,
            c => c.Id, storageContabo.Guardar);

        var storageTareas = new TareaProgramadaStorageService();
        resultado.NuevasTareas = CombinarOReemplazar(
            storageTareas.Cargar(), config.TareasProgramadas, reemplazarTodo,
            t => t.Id, storageTareas.Guardar);

        return resultado;
    }

    /// <summary>
    /// Si reemplazarTodo es true, guarda tal cual la lista importada.
    /// Si es false ("combinar"), agrega solo las que no existían ya
    /// (comparando por Id) y conserva las que el equipo ya tenía.
    /// Devuelve cuántas se agregaron.
    /// </summary>
    private int CombinarOReemplazar<T>(
        List<T> actuales, List<T> importadas, bool reemplazarTodo,
        Func<T, string?> obtenerId, Action<List<T>> guardar)
    {
        if (reemplazarTodo)
        {
            guardar(importadas);
            return importadas.Count;
        }

        var idsExistentes = actuales
            .Select(obtenerId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet();

        var nuevas = importadas
            .Where(x =>
            {
                var id = obtenerId(x);
                return string.IsNullOrEmpty(id) || !idsExistentes.Contains(id);
            })
            .ToList();

        if (nuevas.Count > 0)
        {
            actuales.AddRange(nuevas);
            guardar(actuales);
        }

        return nuevas.Count;
    }
}