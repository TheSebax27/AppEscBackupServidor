namespace BackupSyncApp.Models;

/// <summary>
/// De qué tipo de conexión sale el backup que se va a programar.
/// </summary>
public enum TipoTareaOrigen
{
    BackupSqlWindows,   // ConexionBackupSql (180/181/182)
    ContaboBaseDatos,   // ConexionBackupContabo -> GenerarBackupBaseDatos
    ContaboArchivos     // ConexionBackupContabo -> GenerarBackupArchivos
}

/// <summary>
/// Representa una programación: "todos los lunes a medianoche, saca backup
/// del servidor X y luego bájalo a mi disco". Soporta cualquiera de los 3
/// tipos de backup que existen en la app.
/// </summary>
public class TareaProgramada
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Nombre { get; set; } = "";

    public TipoTareaOrigen Tipo { get; set; } = TipoTareaOrigen.BackupSqlWindows;

    // Id de la conexión de origen: ConexionBackupSql.Id o ConexionBackupContabo.Id
    // según lo que diga Tipo.
    public string ConexionOrigenId { get; set; } = "";

    // Encadenar con "Pasar archivos" después de generar el backup.
    // ConexionArchivosId es opcional: solo se usa si EncadenarConSincronizacion = true.
    public bool EncadenarConSincronizacion { get; set; } = true;
    public string? ConexionArchivosId { get; set; }

    // Hora del día (0-23 y 0-59), y qué días de la semana corre.
    public int Hora { get; set; } = 0;
    public int Minuto { get; set; } = 0;
    public List<DayOfWeek> Dias { get; set; } = new()
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    };

    public bool Activada { get; set; } = false;

    // Se marca en true una vez que la tarea correspondiente ya quedó
    // registrada en el Programador de Tareas de Windows.
    public bool RegistradaEnWindows { get; set; } = false;

    private string EtiquetaTipo => Tipo switch
    {
        TipoTareaOrigen.BackupSqlWindows => "SQL Windows",
        TipoTareaOrigen.ContaboBaseDatos => "Contabo · BD",
        TipoTareaOrigen.ContaboArchivos => "Contabo · Archivos",
        _ => "?"
    };

    // Solo para mostrar en la lista, no se usa para nada más.
    public string ResumenHorario =>
        $"{EtiquetaTipo} · {Hora:D2}:{Minuto:D2} · " +
        $"{(Dias.Count == 7 ? "Diario" : string.Join(",", Dias.Select(d => d.ToString().Substring(0, 3))))} · " +
        (Activada ? "Activada" : "Desactivada");
}