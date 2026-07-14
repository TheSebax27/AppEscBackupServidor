using System;
using System.Collections.Generic;
using System.Text;

namespace BackupSyncApp.Models;

public class ConexionBackupSql
{
    public string Nombre { get; set; } = "";

    // Datos para que NOSOTROS (esta app) nos conectemos por SSH al servidor satélite
    public string Servidor { get; set; } = "";
    public string Usuario { get; set; } = "";
    public string RutaLlavePrivada { get; set; } = "";

    // Equivalentes a las variables que cambiaban en cada script .ps1
    public string InstanciaSql { get; set; } = "";      // ej: DESKTOP-SIS\SQLEXPRESS
    public string Identificador { get; set; } = "";     // ej: backup_180
    public string RutaBackupLocal { get; set; } = "";    // ej: C:\Backup_Local_180 (en el satélite)

    // Envío del ZIP resultante al servidor central por SCP (opcional, como en los scripts)
    public bool EnviarAlCentralPorScp { get; set; } = false;
    public string CentralUsuario { get; set; } = "";
    public string CentralIp { get; set; } = "";
    public string CentralDestino { get; set; } = "";
    // Ojo: esta es la llave que YA está guardada en el servidor satélite
    // (la usa el script remoto para conectarse al central), no la nuestra.
    public string CentralRutaLlaveEnSatelite { get; set; } = "";
}