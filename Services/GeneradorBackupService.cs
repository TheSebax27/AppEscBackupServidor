using System.IO;
using System.Text;
using Renci.SshNet;
using BackupSyncApp.Models;

namespace BackupSyncApp.Services;

/// <summary>
/// Se conecta por SSH al servidor satélite (180/181/182/etc.) y ejecuta ahí
/// un script PowerShell equivalente a tus .ps1 originales, parametrizado con
/// los datos de la conexión. Reporta cada línea en vivo (event Log).
/// </summary>
public class GeneradorBackupService
{
    public event Action<string>? Log;

    private SshClient? _sshActivo;

    /// <summary>
    /// Corta la conexión de inmediato (igual que en SincronizacionService),
    /// para poder cancelar aunque el backup remoto esté a mitad de camino.
    /// </summary>
    public void ForzarDesconexion()
    {
        try { _sshActivo?.Disconnect(); } catch { }
    }

    public void GenerarBackup(ConexionBackupSql conexion, CancellationToken cancelacion = default)
    {
        var connectionInfo = ConstruirConnectionInfo(conexion);

        using var ssh = new SshClient(connectionInfo);
        _sshActivo = ssh;

        Log?.Invoke($"Conectando a {conexion.Servidor}...");
        ssh.Connect();
        Log?.Invoke("Conexión establecida. Iniciando backup (puede tardar varios minutos según el tamaño de las bases)...");

        try
        {
            var script = ConstruirScriptBackup(conexion);
            var bytes = Encoding.Unicode.GetBytes(script);
            var base64 = Convert.ToBase64String(bytes);

            using var comando = ssh.CreateCommand($"powershell -NoProfile -EncodedCommand {base64}");
            comando.CommandTimeout = TimeSpan.FromHours(3); // backups grandes pueden tardar

            var asyncResult = comando.BeginExecute();
            using var lector = new StreamReader(comando.OutputStream);

            // Vamos leyendo línea por línea A MEDIDA que el script remoto las
            // va escribiendo (Write-Host), en vez de esperar a que termine todo.
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

    private ConnectionInfo ConstruirConnectionInfo(ConexionBackupSql conexion)
    {
        var archivoLlave = new PrivateKeyFile(conexion.RutaLlavePrivada);
        var metodoAuth = new PrivateKeyAuthenticationMethod(conexion.Usuario, archivoLlave);
        return new ConnectionInfo(conexion.Servidor, conexion.Usuario, metodoAuth);
    }

    /// <summary>
    /// Construye el mismo script que tenías en los .ps1, pero parametrizado
    /// con los datos de la conexión en vez de tenerlo repetido 4 veces.
    /// </summary>
    private string ConstruirScriptBackup(ConexionBackupSql conexion)
    {
        var instancia = conexion.InstanciaSql.Replace("'", "''");
        var identificador = conexion.Identificador.Replace("'", "''");
        var backupPath = conexion.RutaBackupLocal.Replace("'", "''");

        var sb = new StringBuilder();

        sb.AppendLine($"$ServerInstance = '{instancia}'");
        sb.AppendLine($"$Identifier = '{identificador}'");
        sb.AppendLine("$TimestampPath = Get-Date -Format 'yyyyMMdd_HHmmss'");
        sb.AppendLine($"$BackupPath = '{backupPath}'");
        sb.AppendLine("$DatedBackupPath = Join-Path $BackupPath $TimestampPath");
        sb.AppendLine("$SQLQuery = \"SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name NOT IN ('master', 'tempdb', 'model', 'msdb')\"");
        sb.AppendLine("Write-Host \"Consultando bases de datos de $ServerInstance...\"");
        sb.AppendLine("$DatabaseList = sqlcmd -S $ServerInstance -E -Q $SQLQuery -h -1 -W | Select-Object -Skip 1");
        sb.AppendLine("$DatabaseList = $DatabaseList | Where-Object { $_ -ne $null -and $_ -ne '' }");
        sb.AppendLine("Write-Host \"Se encontraron $($DatabaseList.Count) bases de datos para respaldar.\"");
        sb.AppendLine("if (-not (Test-Path $DatedBackupPath)) {");
        sb.AppendLine("    Write-Host \"Creando directorio: $DatedBackupPath\"");
        sb.AppendLine("    New-Item -Path $DatedBackupPath -ItemType Directory | Out-Null");
        sb.AppendLine("}");
        sb.AppendLine("foreach ($DatabaseName in $DatabaseList) {");
        sb.AppendLine("    Write-Host \"--- Backup de $DatabaseName ---\"");
        sb.AppendLine("    $BackupFile = Join-Path $DatedBackupPath \"$($DatabaseName).bak\"");
        sb.AppendLine("    $SQLCommand = \"BACKUP DATABASE [$DatabaseName] TO DISK = N'$BackupFile' WITH STATS = 10\"");
        sb.AppendLine("    sqlcmd -S $ServerInstance -E -Q $SQLCommand | Out-Null");
        sb.AppendLine("}");
        sb.AppendLine("Write-Host \"Comprimiendo la carpeta de backup $TimestampPath...\"");
        sb.AppendLine("$FinalZipName = \"$($Identifier)_$($TimestampPath)_TODAS_DBs.zip\"");
        sb.AppendLine("$FinalZipFile = Join-Path $BackupPath $FinalZipName");
        sb.AppendLine("Compress-Archive -Path \"$DatedBackupPath\\*\" -DestinationPath $FinalZipFile -Force");

        // Solo borramos la carpeta con los .bak sueltos si el ZIP quedó bien
        // creado — así no duplicamos el espacio en disco (antes quedaban
        // los .bak Y el .zip al mismo tiempo, ocupando el doble).
        sb.AppendLine("if (Test-Path $FinalZipFile) {");
        sb.AppendLine("    Write-Host \"ZIP creado correctamente. Borrando carpeta temporal $TimestampPath para liberar espacio...\"");
        sb.AppendLine("    Remove-Item $DatedBackupPath -Recurse -Force");
        sb.AppendLine("} else {");
        sb.AppendLine("    Write-Host \"[AVISO] El ZIP no se creó correctamente. Se conserva la carpeta $TimestampPath por seguridad.\"");
        sb.AppendLine("}");

        if (conexion.EnviarAlCentralPorScp)
        {
            var centralUsuario = conexion.CentralUsuario.Replace("'", "''");
            var centralIp = conexion.CentralIp.Replace("'", "''");
            var centralDestino = conexion.CentralDestino.Replace("'", "''");
            var centralLlave = conexion.CentralRutaLlaveEnSatelite.Replace("'", "''");

            sb.AppendLine("Write-Host \"Enviando el archivo ZIP al servidor central...\"");
            sb.AppendLine($"$PathKeySSH = '{centralLlave}'");
            sb.AppendLine("if (Test-Path $FinalZipFile) {");
            sb.AppendLine($"    scp -q -i $PathKeySSH -o StrictHostKeyChecking=accept-new -o BatchMode=yes $FinalZipFile \"{centralUsuario}@{centralIp}:{centralDestino}\"");
            sb.AppendLine("    if ($LASTEXITCODE -eq 0) {");
            sb.AppendLine("        Write-Host \"Envío completado.\"");
            sb.AppendLine("    } else {");
            sb.AppendLine("        Write-Host \"[ERROR] scp terminó con código de salida $LASTEXITCODE\"");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine("else {");
            sb.AppendLine("    Write-Host \"[AVISO] No se encontró el ZIP final, no se envió nada.\"");
            sb.AppendLine("}");
        }

        sb.AppendLine("Write-Host '=== BACKUP FINALIZADO ==='");

        return sb.ToString();
    }
}