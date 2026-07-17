namespace BackupSyncApp.Models;

/// <summary>
/// Configuración para un servidor Contabo (Linux), equivalente a tus scripts
/// db-backup y archivos-backup. Un solo servidor, dos acciones independientes.
/// </summary>
public class ConexionBackupContabo
{
    public string? Id { get; set; }
    public string Nombre { get; set; } = "";

    // Conexión SSH (NUESTRA, para que la app se conecte al Contabo)
    public string Servidor { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string RutaLlavePrivada { get; set; } = "";

    // ---- Backup de base de datos (mysqldump) ----
    public string NombreBaseDatos { get; set; } = "";
    public List<string> Tablas { get; set; } = new();
    public string CarpetaBackupLocalDb { get; set; } = "";   // ej: /var/backups/backup_db_documents

    // ---- Backup de archivos (tar) ----
    // La PRIMERA carpeta de la lista se conserva siempre (no se borra tras el
    // envío); el resto sí se vacía, igual que tu script original.
    public List<string> CarpetasOrigenArchivos { get; set; } = new();

    // ---- Envío al servidor central (compartido entre ambos backups) ----
    public string CentralUsuario { get; set; } = "";
    public string CentralIp { get; set; } = "";
    public string CentralRutaDestinoDb { get; set; } = "";
    public string CentralRutaDestinoArchivos { get; set; } = "";
    // La llave que YA está guardada en el Contabo (no la nuestra)
    public string RutaLlaveEnContabo { get; set; } = "";
}