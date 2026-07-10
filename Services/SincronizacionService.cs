using System.IO;
using System.Security.Cryptography;
using System.Text;
using Renci.SshNet;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Resumen numérico al terminar una sincronización.
/// </summary>
public class ResumenSincronizacion
{
    public int Existentes { get; set; }
    public int Descargados { get; set; }
    public int Movidos { get; set; }
    public int Avisos { get; set; }
    public int Errores { get; set; }
}

/// <summary>
/// Se lanza cuando algo de la CONFIGURACIÓN está mal (ruta origen/destino
/// no existe, etc.) antes de intentar transferir nada. Se distingue de un
/// error de conexión real para poder mostrar un mensaje claro al usuario.
/// </summary>
public class ValidacionSincronizacionException : Exception
{
    public ValidacionSincronizacionException(string mensaje) : base(mensaje) { }
}

/// <summary>
/// Hace lo mismo que el script de PowerShell original, pero como librería C#:
/// 1) Se conecta por SSH y lista los archivos remotos (vía PowerShell remoto, igual que antes)
/// 2) Para cada archivo que no exista localmente, lo descarga por SCP (con progreso real)
/// 3) Si MoverArchivos está activo, verifica tamaño+hash y borra el original remoto
/// </summary>
public class SincronizacionService
{
    // Eventos para que la UI (Etapa 4) se entere de lo que va pasando en vivo
    public event Action<EventoArchivo>? ArchivoActualizado;
    public event Action<string>? Log;

    private string _archivoRelativoActual = "";

    // Referencias a las conexiones activas, para poder forzar el corte desde
    // afuera (ver ForzarDesconexion) cuando el usuario cancela a mitad de una
    // descarga en curso (que de otro modo no se puede interrumpir).
    private SshClient? _sshActivo;
    private ScpClient? _scpActivo;

    /// <summary>
    /// Corta la conexión de inmediato. Si hay una descarga o comando en curso,
    /// esto hace que esa llamada bloqueante lance una excepción de inmediato
    /// en vez de esperar a que termine sola.
    /// </summary>
    public void ForzarDesconexion()
    {
        try { _scpActivo?.Disconnect(); } catch { /* ya se está cortando, ignoramos errores aquí */ }
        try { _sshActivo?.Disconnect(); } catch { }
    }

    public ResumenSincronizacion Sincronizar(ConexionBackup conexion, CancellationToken cancelacion = default)
    {
        var resumen = new ResumenSincronizacion();

        var connectionInfo = ConstruirConnectionInfo(conexion);

        using var ssh = new SshClient(connectionInfo);
        using var scp = new ScpClient(connectionInfo);

        _sshActivo = ssh;
        _scpActivo = scp;

        Log?.Invoke($"Conectando a {conexion.Servidor}...");
        ssh.Connect();
        scp.Connect();
        Log?.Invoke("Conexión establecida.");

        // Se suscribe UNA sola vez; usamos _archivoRelativoActual para saber
        // a qué archivo pertenece cada evento de progreso.
        scp.Downloading += (s, e) =>
        {
            if (e.Size > 0)
            {
                int porcentaje = (int)((double)e.Downloaded / e.Size * 100);
                ArchivoActualizado?.Invoke(new EventoArchivo
                {
                    RutaRelativa = _archivoRelativoActual,
                    Estado = EstadoArchivo.Descargando,
                    PorcentajeProgreso = porcentaje
                });
            }
        };

        try
        {
            // ---- Validación previa: evita quedarse "colgado" con rutas inválidas ----
            var origenExiste = VerificarExistenciaRemota(ssh, conexion.RutaOrigen);
            if (!origenExiste)
            {
                throw new ValidacionSincronizacionException(
                    $"La ruta de ORIGEN no existe en el servidor:\n{conexion.RutaOrigen}\n\nPor favor verificar.");
            }

            try
            {
                Directory.CreateDirectory(conexion.RutaDestino);
            }
            catch (Exception ex)
            {
                throw new ValidacionSincronizacionException(
                    $"La ruta de DESTINO no existe o no se pudo crear/acceder:\n{conexion.RutaDestino}\n\nPor favor verificar.\n\nDetalle: {ex.Message}");
            }

            var archivosRemotos = ListarArchivosRemotos(ssh, conexion.RutaOrigen);
            Log?.Invoke($"Se encontraron {archivosRemotos.Count} archivos en el origen remoto.");

            foreach (var archivoRemoto in archivosRemotos)
            {
                cancelacion.ThrowIfCancellationRequested();
                ProcesarArchivo(ssh, scp, conexion, archivoRemoto, resumen);
            }
        }
        catch (ValidacionSincronizacionException)
        {
            // La dejamos pasar tal cual, sin envolverla, para que la UI la reconozca
            throw;
        }
        catch (Exception) when (cancelacion.IsCancellationRequested)
        {
            // Si la excepción vino de un ForzarDesconexion() en medio de una
            // descarga, la reportamos como cancelación, no como error real.
            throw new OperationCanceledException(cancelacion);
        }
        finally
        {
            _sshActivo = null;
            _scpActivo = null;

            try { scp.Disconnect(); } catch { }
            try { ssh.Disconnect(); } catch { }
            Log?.Invoke("Desconectado del servidor.");
        }

        return resumen;
    }

    private ConnectionInfo ConstruirConnectionInfo(ConexionBackup conexion)
    {
        var archivoLlave = new PrivateKeyFile(conexion.RutaLlavePrivada);
        var metodoAuth = new PrivateKeyAuthenticationMethod(conexion.Usuario, archivoLlave);
        return new ConnectionInfo(conexion.Servidor, conexion.Usuario, metodoAuth);
    }

    /// <summary>
    /// Ejecuta un bloque de PowerShell en el servidor remoto, codificado en Base64
    /// (mismo truco que usamos en el script .ps1, para evitar problemas de comillas).
    /// </summary>
    private string EjecutarPowerShellRemoto(SshClient ssh, string script)
    {
        var bytes = Encoding.Unicode.GetBytes(script);
        var base64 = Convert.ToBase64String(bytes);

        using var comando = ssh.CreateCommand($"powershell -NoProfile -EncodedCommand {base64}");
        var resultado = comando.Execute();

        if (comando.ExitStatus != 0 && !string.IsNullOrWhiteSpace(comando.Error))
        {
            Log?.Invoke($"[AVISO] El comando remoto devolvió un error: {comando.Error}");
        }

        return resultado;
    }

    private bool VerificarExistenciaRemota(SshClient ssh, string ruta)
    {
        var rutaEscapada = ruta.Replace("'", "''");
        var script = $"Test-Path -LiteralPath '{rutaEscapada}'";
        var salida = EjecutarPowerShellRemoto(ssh, script).Trim();
        return string.Equals(salida, "True", StringComparison.OrdinalIgnoreCase);
    }

    private List<string> ListarArchivosRemotos(SshClient ssh, string origen)
    {
        var origenEscapado = origen.Replace("'", "''");
        // -ErrorAction SilentlyContinue: si algún archivo individual da error de
        // acceso, lo ignora y sigue, en vez de escribir al stream de error
        // (eso podía dejar el comando "colgado" esperando que alguien lo lea).
        var script = $"Get-ChildItem -Path '{origenEscapado}' -Recurse -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName";
        var salida = EjecutarPowerShellRemoto(ssh, script);

        return salida
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    private void ProcesarArchivo(SshClient ssh, ScpClient scp, ConexionBackup conexion,
        string archivoRemoto, ResumenSincronizacion resumen)
    {
        var origenNormalizado = conexion.RutaOrigen.TrimEnd('\\');

        if (!archivoRemoto.StartsWith(origenNormalizado, StringComparison.OrdinalIgnoreCase))
        {
            Log?.Invoke($"[OMITIDO] Ruta inesperada: {archivoRemoto}");
            return;
        }

        var relativa = archivoRemoto.Substring(origenNormalizado.Length).TrimStart('\\');
        var destinoArchivo = Path.Combine(conexion.RutaDestino, relativa);
        _archivoRelativoActual = relativa;

        // Ya existe: igual que en el script de PowerShell, se omite sin tocar nada
        if (File.Exists(destinoArchivo))
        {
            resumen.Existentes++;
            ArchivoActualizado?.Invoke(new EventoArchivo
            {
                RutaRelativa = relativa,
                Estado = EstadoArchivo.Existe
            });
            return;
        }

        var carpeta = Path.GetDirectoryName(destinoArchivo);
        if (!string.IsNullOrEmpty(carpeta) && !Directory.Exists(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        ArchivoActualizado?.Invoke(new EventoArchivo
        {
            RutaRelativa = relativa,
            Estado = EstadoArchivo.Descargando,
            PorcentajeProgreso = 0
        });

        try
        {
            scp.Download(archivoRemoto, new FileInfo(destinoArchivo));
        }
        catch (Exception ex)
        {
            Log?.Invoke($"[ERROR] Falló la descarga de {relativa}: {ex.Message}");
            resumen.Errores++;
            ArchivoActualizado?.Invoke(new EventoArchivo
            {
                RutaRelativa = relativa,
                Estado = EstadoArchivo.ErrorDescarga,
                Mensaje = ex.Message
            });
            return;
        }

        resumen.Descargados++;
        ArchivoActualizado?.Invoke(new EventoArchivo
        {
            RutaRelativa = relativa,
            Estado = EstadoArchivo.Descargado
        });

        if (!conexion.MoverArchivos)
        {
            return;
        }

        VerificarYBorrarOriginal(ssh, conexion, archivoRemoto, destinoArchivo, relativa, resumen);
    }

    private void VerificarYBorrarOriginal(SshClient ssh, ConexionBackup conexion, string archivoRemoto,
        string destinoArchivo, string relativa, ResumenSincronizacion resumen)
    {
        ArchivoActualizado?.Invoke(new EventoArchivo
        {
            RutaRelativa = relativa,
            Estado = EstadoArchivo.VerificandoIntegridad
        });

        var tamanoLocal = new FileInfo(destinoArchivo).Length;
        var hashLocal = CalcularHashLocal(destinoArchivo);

        var archivoEscapado = archivoRemoto.Replace("'", "''");
        var scriptVerificacion =
            $"$item = Get-Item -LiteralPath '{archivoEscapado}'; " +
            $"$hash = Get-FileHash -LiteralPath '{archivoEscapado}' -Algorithm SHA256; " +
            "Write-Output (\"$($item.Length)|$($hash.Hash)\")";

        var salida = EjecutarPowerShellRemoto(ssh, scriptVerificacion).Trim();
        var partes = salida.Split('|');

        if (partes.Length != 2)
        {
            Log?.Invoke($"  [AVISO] No se pudo verificar el original remoto de {relativa}. NO se borra.");
            resumen.Avisos++;
            ArchivoActualizado?.Invoke(new EventoArchivo
            {
                RutaRelativa = relativa,
                Estado = EstadoArchivo.ErrorVerificacion,
                Mensaje = "No se pudo leer tamaño/hash remoto"
            });
            return;
        }

        var tamanoRemoto = partes[0].Trim();
        var hashRemoto = partes[1].Trim();

        bool coincide = tamanoLocal.ToString() == tamanoRemoto &&
                         string.Equals(hashLocal, hashRemoto, StringComparison.OrdinalIgnoreCase);

        if (!coincide)
        {
            Log?.Invoke($"  [AVISO] La verificación NO coincide para {relativa}. Original NO se borra.");
            resumen.Avisos++;
            ArchivoActualizado?.Invoke(new EventoArchivo
            {
                RutaRelativa = relativa,
                Estado = EstadoArchivo.ErrorVerificacion,
                Mensaje = "Tamaño/hash no coinciden"
            });
            return;
        }

        var scriptBorrado = $"Remove-Item -LiteralPath '{archivoEscapado}' -Force";
        EjecutarPowerShellRemoto(ssh, scriptBorrado);

        Log?.Invoke($"  [MOVIDO] Original borrado tras verificación OK: {relativa}");
        resumen.Movidos++;
        ArchivoActualizado?.Invoke(new EventoArchivo
        {
            RutaRelativa = relativa,
            Estado = EstadoArchivo.Movido
        });
    }

    private string CalcularHashLocal(string ruta)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(ruta);
        var hashBytes = sha256.ComputeHash(stream);
        return Convert.ToHexString(hashBytes); // formato hex igual al de Get-FileHash de PowerShell
    }
}