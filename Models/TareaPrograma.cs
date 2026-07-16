namespace BackupSyncApp.Models;

/// <summary>
/// Representa una programación: "todos los lunes a medianoche, saca backup
/// del servidor X y luego bájalo a mi disco".
/// </summary>
public class TareaProgramada
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Nombre { get; set; } = "";

    // A cuál conexión de "Sacar backups" (ConexionBackupSql) referencia
    public string ConexionBackupSqlId { get; set; } = "";

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

    // Solo para mostrar en la lista, no se usa para nada más.
    public string ResumenHorario =>
        $"{Hora:D2}:{Minuto:D2} · {(Dias.Count == 7 ? "Diario" : string.Join(",", Dias.Select(d => d.ToString().Substring(0, 3))))} · " +
        (Activada ? "Activada" : "Desactivada");
}