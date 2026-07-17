using System.Diagnostics;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Crea/elimina entradas en el Programador de Tareas de Windows (schtasks.exe)
/// para que Windows mismo dispare esta app en modo silencioso a la hora
/// programada, aunque la app esté cerrada o la PC se haya reiniciado.
/// </summary>
public class ProgramadorWindowsService
{
    private const string PrefijoNombreTarea = "BackupSyncApp_";

    private static readonly Dictionary<DayOfWeek, string> CodigosDia = new()
    {
        { DayOfWeek.Monday, "MON" },
        { DayOfWeek.Tuesday, "TUE" },
        { DayOfWeek.Wednesday, "WED" },
        { DayOfWeek.Thursday, "THU" },
        { DayOfWeek.Friday, "FRI" },
        { DayOfWeek.Saturday, "SAT" },
        { DayOfWeek.Sunday, "SUN" },
    };

    public (bool Exito, string Mensaje) RegistrarTarea(TareaProgramada tarea)
    {
        var rutaExe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(rutaExe))
        {
            return (false, "No se pudo determinar la ruta del ejecutable actual.");
        }

        var nombreTareaWindows = PrefijoNombreTarea + tarea.Id;
        var horaFormateada = $"{tarea.Hora:D2}:{tarea.Minuto:D2}";
        var argumentos = $"--tarea {tarea.Id}";

        // Si corre los 7 días, usamos /sc DAILY (más simple). Si no, /sc WEEKLY con /d.
        var todosLosDias = tarea.Dias.Count == 7;

        var argsSchtasks = todosLosDias
            ? $"/create /tn \"{nombreTareaWindows}\" /tr \"\\\"{rutaExe}\\\" {argumentos}\" /sc DAILY /st {horaFormateada} /f"
            : $"/create /tn \"{nombreTareaWindows}\" /tr \"\\\"{rutaExe}\\\" {argumentos}\" /sc WEEKLY /d {ConstruirListaDias(tarea.Dias)} /st {horaFormateada} /f";

        return EjecutarSchtasks(argsSchtasks);
    }

    public (bool Exito, string Mensaje) EliminarTarea(TareaProgramada tarea)
    {
        var nombreTareaWindows = PrefijoNombreTarea + tarea.Id;
        var argsSchtasks = $"/delete /tn \"{nombreTareaWindows}\" /f";
        return EjecutarSchtasks(argsSchtasks);
    }

    private string ConstruirListaDias(List<DayOfWeek> dias)
    {
        return string.Join(",", dias.Select(d => CodigosDia[d]));
    }

    private (bool Exito, string Mensaje) EjecutarSchtasks(string argumentos)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = argumentos,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proceso = Process.Start(psi);
            if (proceso == null)
            {
                return (false, "No se pudo iniciar schtasks.exe.");
            }

            string salida = proceso.StandardOutput.ReadToEnd();
            string error = proceso.StandardError.ReadToEnd();
            proceso.WaitForExit();

            if (proceso.ExitCode == 0)
            {
                return (true, salida.Trim());
            }

            return (false, string.IsNullOrWhiteSpace(error) ? salida.Trim() : error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}