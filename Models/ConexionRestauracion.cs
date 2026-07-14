using System;
using System.Collections.Generic;
using System.Text;

namespace BackupSyncApp.Models
{
    public class ConexionRestauracion
    {

        public string Nombre { get; set; } = "";
        public string Servidor { get; set; } = "";
        public string Usuario { get; set; } = "";
        public string RutaLlavePrivada { get; set; } = "";
        public string InstanciaSql { get; set; } = "";
        public string CarpetaZips { get; set; } = "";
        public string RutaDatosSql { get; set; } = "";

    }
}
