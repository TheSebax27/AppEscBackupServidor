using System;
using System.Collections.Generic;
using System.Text;

namespace BackupSyncApp.Models
{
    public class EventoArchivo
    {

        public string RutaRelativa { get; set; } = "";
        public EstadoArchivo Estado { get; set; }
        public string? Mensaje { get; set; }
        public int? PorcentajeProgreso { get; set; }

    }
}
