namespace BackupSyncApp.Models;

/// <summary>
/// Representa una conexión de backup configurada por el usuario
/// (ej: "Contabo 1", "Contabo 2"). Se guarda tal cual en el JSON local.
/// </summary>
public class ConexionBackup
{
    // Identificador estable (no cambia aunque renombres la conexión).
    public string? Id { get; set; }

    public string Nombre { get; set; } = "";
    public string Servidor { get; set; } = "";
    public string Usuario { get; set; } = "";

    // Solo guardamos la RUTA a la llave privada, nunca la llave en sí ni contraseñas.
    public string RutaLlavePrivada { get; set; } = "";

    public string RutaOrigen { get; set; } = "";
    public string RutaDestino { get; set; } = "";

    // Igual que el $MoverArchivos del script de PowerShell:
    // si es true, borra el original remoto tras verificar tamaño + hash.
    public bool MoverArchivos { get; set; } = false;
}