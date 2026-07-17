using System.IO;
using System.Text;
using Renci.SshNet;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Se conecta por SSH a un servidor Contabo (Linux) y ejecuta remotamente un
/// script bash equivalente a tus scripts db-backup / archivos-backup,
/// parametrizado con los datos de la conexión. Log en vivo igual que
/// GeneradorBackupService, pero para bash en vez de PowerShell.
/// </summary>
public class BackupContaboService
{
    public event Action<string>? Log;

    private SshClient? _sshActivo;

    public void ForzarDesconexion()
    {
        try { _sshActivo?.Disconnect(); } catch { }
    }

    private ConnectionInfo ConstruirConnectionInfo(ConexionBackupContabo conexion)
    {
        var archivoLlave = new PrivateKeyFile(conexion.RutaLlavePrivada);
        var metodoAuth = new PrivateKeyAuthenticationMethod(conexion.Usuario, archivoLlave);
        return new ConnectionInfo(conexion.Servidor, conexion.Usuario, metodoAuth);
    }

    public void GenerarBackupBaseDatos(ConexionBackupContabo conexion, CancellationToken cancelacion = default)
    {
        var script = ConstruirScriptBaseDatos(conexion);
        EjecutarScriptConLogEnVivo(conexion, script, cancelacion);
    }

    public void GenerarBackupArchivos(ConexionBackupContabo conexion, CancellationToken cancelacion = default)
    {
        var script = ConstruirScriptArchivos(conexion);
        EjecutarScriptConLogEnVivo(conexion, script, cancelacion);
    }

    private void EjecutarScriptConLogEnVivo(ConexionBackupContabo conexion, string script, CancellationToken cancelacion)
    {
        var connectionInfo = ConstruirConnectionInfo(conexion);
        using var ssh = new SshClient(connectionInfo);
        _sshActivo = ssh;

        Log?.Invoke($"Conectando a {conexion.Servidor}...");
        ssh.Connect();
        Log?.Invoke("Conexión establecida.");

        try
        {
            // Igual truco que con PowerShell (Base64), pero para bash:
            // evita cualquier problema de comillas/escapes al enviar el script.
            var bytes = Encoding.UTF8.GetBytes(script);
            var base64 = Convert.ToBase64String(bytes);
            var comandoTexto = $"echo {base64} | base64 -d | bash";

            using var comando = ssh.CreateCommand(comandoTexto);
            comando.CommandTimeout = TimeSpan.FromHours(3);

            var asyncResult = comando.BeginExecute();
            using var lector = new StreamReader(comando.OutputStream);

            while (!asyncResult.IsCompleted)
            {
                cancelacion.ThrowIfCancellationRequested();

                var linea = lector.ReadLine();
                if (linea != null)
                {
                    Log?.Invoke(linea);
                }
                else
                {
                    Thread.Sleep(300);
                }
            }

            string? restante;
            while ((restante = lector.ReadLine()) != null)
            {
                Log?.Invoke(restante);
            }

            comando.EndExecute(asyncResult);

            if (comando.ExitStatus != 0)
            {
                Log?.Invoke($"[AVISO] El script remoto terminó con código de salida {comando.ExitStatus}.");
                if (!string.IsNullOrWhiteSpace(comando.Error))
                {
                    Log?.Invoke($"[ERROR] {comando.Error}");
                }
            }
        }
        finally
        {
            _sshActivo = null;
            try { ssh.Disconnect(); } catch { }
            Log?.Invoke("Desconectado del servidor.");
        }
    }

    private string EscaparBash(string valor) => valor.Replace("'", "'\\''");

    /// <summary>
    /// Traducción parametrizada de tu script "db-backup".
    /// </summary>
    private string ConstruirScriptBaseDatos(ConexionBackupContabo conexion)
    {
        var dbName = EscaparBash(conexion.NombreBaseDatos);
        var carpeta = EscaparBash(conexion.CarpetaBackupLocalDb);
        var llave = EscaparBash(conexion.RutaLlaveEnContabo);
        var usuarioCentral = EscaparBash(conexion.CentralUsuario);
        var ipCentral = EscaparBash(conexion.CentralIp);
        var destinoCentral = EscaparBash(conexion.CentralRutaDestinoDb);
        var tablas = string.Join(" ", conexion.Tablas.Select(t => $"'{EscaparBash(t)}'"));

        var sb = new StringBuilder();
        sb.AppendLine("set -o pipefail");
        sb.AppendLine($"DB_NAME='{dbName}'");
        sb.AppendLine($"TABLAS=({tablas})");
        sb.AppendLine("FECHA=$(date +%Y-%m-%d_%H-%M)");
        sb.AppendLine($"CARPETA='{carpeta}'");
        sb.AppendLine("ARCHIVO_GZ=\"$CARPETA/backup_tablas_$FECHA.sql.gz\"");
        sb.AppendLine($"LLAVE='{llave}'");
        sb.AppendLine($"USUARIO_REMOTO='{usuarioCentral}'");
        sb.AppendLine($"IP_REMOTA='{ipCentral}'");
        sb.AppendLine($"RUTA_REMOTA='{destinoCentral}'");
        sb.AppendLine();
        sb.AppendLine("mkdir -p \"$CARPETA\"");
        sb.AppendLine("echo \"Iniciando backup de $DB_NAME...\"");
        sb.AppendLine("echo \"Tablas a procesar: ${TABLAS[*]}\"");
        sb.AppendLine();
        sb.AppendLine("/usr/bin/mysqldump --single-transaction --quick --lock-tables=false \"$DB_NAME\" \"${TABLAS[@]}\" | /bin/gzip > \"$ARCHIVO_GZ\"");
        sb.AppendLine("if [ $? -eq 0 ]; then");
        sb.AppendLine("    echo \"Backup local creado: $ARCHIVO_GZ\"");
        sb.AppendLine("else");
        sb.AppendLine("    echo \"[ERROR] Falló el mysqldump\"");
        sb.AppendLine("    exit 1");
        sb.AppendLine("fi");
        sb.AppendLine();
        sb.AppendLine("echo \"Enviando el backup al servidor central...\"");
        sb.AppendLine("scp -q -i \"$LLAVE\" -o StrictHostKeyChecking=accept-new -o BatchMode=yes \"$ARCHIVO_GZ\" \"$USUARIO_REMOTO@$IP_REMOTA:$RUTA_REMOTA\"");
        sb.AppendLine("if [ $? -eq 0 ]; then");
        sb.AppendLine("    echo \"Transferencia exitosa. Borrando copia local temporal.\"");
        sb.AppendLine("    rm -f \"$ARCHIVO_GZ\"");
        sb.AppendLine("    echo '=== BACKUP DE BASE DE DATOS FINALIZADO ==='");
        sb.AppendLine("else");
        sb.AppendLine("    echo \"[ERROR] Falló el envío. Se conserva el backup local por seguridad.\"");
        sb.AppendLine("    exit 1");
        sb.AppendLine("fi");

        return sb.ToString();
    }

    /// <summary>
    /// Traducción parametrizada de tu script "archivos-backup". La primera
    /// carpeta de la lista se conserva siempre; el resto se vacía tras el envío.
    /// </summary>
    private string ConstruirScriptArchivos(ConexionBackupContabo conexion)
    {
        var llave = EscaparBash(conexion.RutaLlaveEnContabo);
        var usuarioCentral = EscaparBash(conexion.CentralUsuario);
        var ipCentral = EscaparBash(conexion.CentralIp);
        var destinoCentral = EscaparBash(conexion.CentralRutaDestinoArchivos);
        var origenes = string.Join(" ", conexion.CarpetasOrigenArchivos.Select(o => $"'{EscaparBash(o)}'"));

        var sb = new StringBuilder();
        sb.AppendLine("set -o pipefail");
        sb.AppendLine("FECHA=$(date +%Y-%m-%d_%H%M)");
        sb.AppendLine($"ORIGENES=({origenes})");
        sb.AppendLine("ARCHIVO_ZIP=\"/tmp/backup_$FECHA.tar.gz\"");
        sb.AppendLine($"LLAVE='{llave}'");
        sb.AppendLine($"USUARIO_REMOTO='{usuarioCentral}'");
        sb.AppendLine($"IP_REMOTA='{ipCentral}'");
        sb.AppendLine($"RUTA_REMOTA='{destinoCentral}'");
        sb.AppendLine();
        sb.AppendLine("echo \"Comprimiendo archivos...\"");
        sb.AppendLine("tar -czf \"$ARCHIVO_ZIP\" \"${ORIGENES[@]}\"");
        sb.AppendLine();
        sb.AppendLine("echo \"Enviando al servidor central...\"");
        sb.AppendLine("scp -q -i \"$LLAVE\" -o StrictHostKeyChecking=accept-new -o BatchMode=yes \"$ARCHIVO_ZIP\" \"$USUARIO_REMOTO@$IP_REMOTA:$RUTA_REMOTA\"");
        sb.AppendLine("if [ $? -eq 0 ]; then");
        sb.AppendLine("    echo \"Transferencia exitosa. Limpiando carpetas de origen (excepto la primera)...\"");
        sb.AppendLine("    rm -f \"$ARCHIVO_ZIP\"");
        sb.AppendLine("    for i in \"${!ORIGENES[@]}\"; do");
        sb.AppendLine("        if [ \"$i\" -ne 0 ]; then");
        sb.AppendLine("            rm -rf \"${ORIGENES[$i]:?}\"/*");
        sb.AppendLine("        fi");
        sb.AppendLine("    done");
        sb.AppendLine("    echo '=== BACKUP DE ARCHIVOS FINALIZADO ==='");
        sb.AppendLine("else");
        sb.AppendLine("    echo \"[ERROR] No se pudo enviar el archivo. Se mantienen los originales.\"");
        sb.AppendLine("    exit 1");
        sb.AppendLine("fi");

        return sb.ToString();
    }
}