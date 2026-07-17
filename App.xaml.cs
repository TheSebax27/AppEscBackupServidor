using System.IO;
using System.Windows;
using BackupSyncApp.Services;

namespace BackupSyncApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Modo silencioso: Windows nos lanzó con "--tarea <id>" desde el
        // Programador de Tareas. No mostramos NINGUNA ventana — ejecutamos
        // la tarea, escribimos el resultado en un log de texto, y cerramos.
        var indiceTarea = Array.IndexOf(e.Args, "--tarea");
        if (indiceTarea >= 0 && indiceTarea + 1 < e.Args.Length)
        {
            var idTarea = e.Args[indiceTarea + 1];
            EjecutarModoSilencioso(idTarea);
            Shutdown();
            return;
        }

        // Modo normal: arranca la app visualmente en el menú.
        var menu = new MenuWindow();
        menu.Show();
    }

    private void EjecutarModoSilencioso(string idTarea)
    {
        var carpetaLogs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BackupSyncApp", "logs");
        Directory.CreateDirectory(carpetaLogs);

        var rutaLog = Path.Combine(carpetaLogs,
            $"tarea_{idTarea}_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        using var escritor = new StreamWriter(rutaLog, append: false);
        escritor.AutoFlush = true;

        void EscribirLinea(string texto)
        {
            escritor.WriteLine($"[{DateTime.Now:HH:mm:ss}] {texto}");
        }

        var storageTareas = new TareaProgramadaStorageService();
        var tarea = storageTareas.BuscarPorId(idTarea);

        if (tarea == null)
        {
            EscribirLinea($"[ERROR] No se encontró la tarea programada con Id: {idTarea}");
            return;
        }

        if (!tarea.Activada)
        {
            EscribirLinea($"[AVISO] La tarea '{tarea.Nombre}' está desactivada. No se ejecuta.");
            return;
        }

        try
        {
            var ejecutor = new EjecutorTareaProgramada();
            ejecutor.Log += EscribirLinea;
            ejecutor.Ejecutar(tarea);
        }
        catch (Exception ex)
        {
            EscribirLinea($"[ERROR FATAL] {ex.Message}");
        }
    }
}