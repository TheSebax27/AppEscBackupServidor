using System;
using System.Collections.Generic;
using System.Text;

namespace BackupSyncApp.Models
{
    public enum EstadoArchivo
    {

        Existe,
        Descargando,
        Descargado,
        VerificandoIntegridad,
        Movido,
        ErrorDescarga,
        ErrorVerificacion,
        ErrorConexion

    }
}
