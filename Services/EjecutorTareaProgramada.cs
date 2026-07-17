using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Corre una TareaProgramada de principio a fin, sin importar si el origen
/// es un backup SQL de Windows o un backup Contabo (BD o archivos). Si
/// corresponde, encadena la sincronización para bajarlo a disco.
/// </summary>
public class EjecutorTareaProgramada
{
    public event Action<string>? Log;

    public bool Ejecutar(TareaProgramada tarea, CancellationToken cancelacion = default)
    {
        Log?.Invoke($"=== Iniciando tarea programada: {tarea.Nombre} ===");

        bool pasoOk = EjecutarPasoDeBackup(tarea, cancelacion);

        if (!pasoOk || !tarea.EncadenarConSincronizacion || string.IsNullOrEmpty(tarea.ConexionArchivosId))
        {
            Log?.Invoke(pasoOk
                ? "=== Tarea finalizada (sin bajar archivos) ==="
                : "=== Tarea finalizada CON ERRORES ===");
            return pasoOk;
        }

        return EjecutarPasoDeSincronizacion(tarea, cancelacion);
    }

    private bool EjecutarPasoDeBackup(TareaProgramada tarea, CancellationToken cancelacion)
    {
        switch (tarea.Tipo)
        {
            case TipoTareaOrigen.BackupSqlWindows:
                {
                    var conexion = new ConexionBackupSqlStorageService().Cargar()
                        .FirstOrDefault(c => c.Id == tarea.ConexionOrigenId);

                    if (conexion == null)
                    {
                        Log?.Invoke($"[ERROR] No se encontró la conexión SQL configurada (Id: {tarea.ConexionOrigenId}). ¿Se eliminó?");
                        return false;
                    }

                    Log?.Invoke($"--- Paso 1: Generar backup SQL en {conexion.Nombre} ---");
                    try
                    {
                        var servicio = new GeneradorBackupService();
                        servicio.Log += linea => Log?.Invoke(linea);
                        servicio.GenerarBackup(conexion, cancelacion);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[ERROR] Falló la generación del backup SQL: {ex.Message}");
                        return false;
                    }
                }

            case TipoTareaOrigen.ContaboBaseDatos:
                {
                    var conexion = new ConexionBackupContaboStorageService().Cargar()
                        .FirstOrDefault(c => c.Id == tarea.ConexionOrigenId);

                    if (conexion == null)
                    {
                        Log?.Invoke($"[ERROR] No se encontró la conexión Contabo configurada (Id: {tarea.ConexionOrigenId}). ¿Se eliminó?");
                        return false;
                    }

                    Log?.Invoke($"--- Paso 1: Generar backup de BASE DE DATOS en {conexion.Nombre} ---");
                    try
                    {
                        var servicio = new BackupContaboService();
                        servicio.Log += linea => Log?.Invoke(linea);
                        servicio.GenerarBackupBaseDatos(conexion, cancelacion);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[ERROR] Falló la generación del backup de BD: {ex.Message}");
                        return false;
                    }
                }

            case TipoTareaOrigen.ContaboArchivos:
                {
                    var conexion = new ConexionBackupContaboStorageService().Cargar()
                        .FirstOrDefault(c => c.Id == tarea.ConexionOrigenId);

                    if (conexion == null)
                    {
                        Log?.Invoke($"[ERROR] No se encontró la conexión Contabo configurada (Id: {tarea.ConexionOrigenId}). ¿Se eliminó?");
                        return false;
                    }

                    Log?.Invoke($"--- Paso 1: Generar backup de ARCHIVOS en {conexion.Nombre} ---");
                    try
                    {
                        var servicio = new BackupContaboService();
                        servicio.Log += linea => Log?.Invoke(linea);
                        servicio.GenerarBackupArchivos(conexion, cancelacion);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log?.Invoke($"[ERROR] Falló la generación del backup de archivos: {ex.Message}");
                        return false;
                    }
                }

            default:
                Log?.Invoke("[ERROR] Tipo de tarea desconocido.");
                return false;
        }
    }

    private bool EjecutarPasoDeSincronizacion(TareaProgramada tarea, CancellationToken cancelacion)
    {
        var conexionArchivos = new ConexionStorageService().Cargar()
            .FirstOrDefault(c => c.Id == tarea.ConexionArchivosId);

        if (conexionArchivos == null)
        {
            Log?.Invoke($"[ERROR] No se encontró la conexión de 'Pasar archivos' configurada (Id: {tarea.ConexionArchivosId}). ¿Se eliminó?");
            return false;
        }

        Log?.Invoke($"--- Paso 2: Bajar archivos nuevos desde {conexionArchivos.Nombre} ---");

        try
        {
            var sincronizador = new SincronizacionService();
            sincronizador.Log += linea => Log?.Invoke(linea);
            sincronizador.ArchivoActualizado += evento => Log?.Invoke($"[{evento.Estado}] {evento.RutaRelativa}");

            var resumen = sincronizador.Sincronizar(conexionArchivos, cancelacion);
            Log?.Invoke(
                $"Descargados: {resumen.Descargados}  |  Ya existían: {resumen.Existentes}  |  " +
                $"Movidos: {resumen.Movidos}  |  Avisos: {resumen.Avisos}  |  Errores: {resumen.Errores}");
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[ERROR] Falló la bajada de archivos: {ex.Message}");
            Log?.Invoke("=== Tarea finalizada CON ERRORES ===");
            return false;
        }

        Log?.Invoke("=== Tarea finalizada correctamente ===");
        return true;
    }
}