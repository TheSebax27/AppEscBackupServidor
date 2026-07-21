namespace BackupSyncApp.Models;

/// <summary>
/// Paquete con TODA la configuración guardada de la app (las 5 listas),
/// para poder moverla de un equipo a otro con un solo archivo.
/// </summary>
public class ConfiguracionExportada
{
    public string Version { get; set; } = "1.0";
    public DateTime FechaExportacion { get; set; } = DateTime.Now;

    public List<ConexionBackup> ConexionesArchivos { get; set; } = new();
    public List<ConexionBackupSql> ConexionesBackupSql { get; set; } = new();
    public List<ConexionRestauracion> ConexionesRestauracion { get; set; } = new();
    public List<ConexionBackupContabo> ConexionesContabo { get; set; } = new();
    public List<TareaProgramada> TareasProgramadas { get; set; } = new();
}

/// <summary>
/// Cuántos elementos nuevos se agregaron al importar (para mostrarle un
/// resumen al usuario).
/// </summary>
public class ResultadoImportacion
{
    public int NuevasArchivos { get; set; }
    public int NuevasBackupSql { get; set; }
    public int NuevasRestauracion { get; set; }
    public int NuevasContabo { get; set; }
    public int NuevasTareas { get; set; }

    public int Total => NuevasArchivos + NuevasBackupSql + NuevasRestauracion + NuevasContabo + NuevasTareas;
}