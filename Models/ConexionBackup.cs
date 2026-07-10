using System;
using System.Collections.Generic;
using System.Text;

namespace BackupSyncApp.Models
{
    public class ConexionBackup
    {
        public string Nombre { get; set; } = "";
        public string Servidor { get; set; } = "";
        public string Usuario { get; set; } = "";
        public string RutaLlavePrivada { get; set; } = "";
        public string RutaOrigen { get; set; } = "";
        public string RutaDestino { get; set; } = "";
        public bool MoverArchivos { get; set; } = false;
    }
}
