namespace BackupSyncApp.Models;

/// <summary>
/// Configuración para restaurar backups SQL en un servidor, equivalente a
/// los parámetros de tu script restaurar_backups_2.ps1.
/// </summary>
public class ConexionRestauracion
{
    public string? Id { get; set; }

    public string Nombre { get; set; } = "";

    public string Servidor { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string RutaLlavePrivada { get; set; } = "";

    public string InstanciaSql { get; set; } = "";   // ej: DESKTOP-4RG5RM3\SQLEXPRESS
    public string CarpetaZips { get; set; } = "";     // ej: C:\Backups_180 (donde están los .zip)

    // Puede quedar vacía y llenarse con el botón "Detectar automáticamente",
    // o escribirse a mano — ambas formas son válidas.
    public string RutaDatosSql { get; set; } = "";
}