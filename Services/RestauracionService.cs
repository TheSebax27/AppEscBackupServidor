using BackupSyncApp.Models;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Linq;
using System.Threading;

namespace BackupSyncApp.Services
{
    public class RestauracionService
    {

        public event Action<string>? Log;

        private SshClient? _sshActivo;


        public void ForzarDesconexion()
        {
            try { _sshActivo?.Disconnect(); } catch { }
        }

        private ConnectionInfo ConstruirConnecionInfo(Models.ConexionRestauracion conexion)
        {
            var archivoLlave = new PrivateKeyFile(conexion.RutaLlavePrivada);
            var metodoAuth = new PrivateKeyAuthenticationMethod(conexion.Usuario, archivoLlave);
            return new ConnectionInfo(conexion.Usuario, conexion.Servidor, metodoAuth);
        }

        private string EjecutarPowerShellRemoto(SshClient ssh, string script)
        {
            var bytes = Encoding.Unicode.GetBytes(script);
            var base64 = Convert.ToBase64String(bytes);
            using var comando = ssh.CreateCommand($"powershell -NoProfile -EncodedCommand {base64}");
            return comando.Execute();
        }

        public string DetectarRutaDatosSql(Models.ConexionRestauracion conexion)
        {
            var connectionInfo = ConstruirConnecionInfo(conexion);
            using var ssh = new SshClient(connectionInfo);
            ssh.Connect();

            try
            {

                var instancia = conexion.InstanciaSql.Replace("'", "''");
                var script = $"$r = sqlcmd -S '{instancia}' -E -Q \"SET NOCOUNT ON; SELECT SERVERPROPERTY('InstanceDefaultDataPath')\" -h -1 -W; " +
                "Write-Output ($r | Select-Object -First 1)";

                var salida = EjecutarPowerShellRemoto(ssh, script).Trim();

                if (string.IsNullOrWhiteSpace(salida))
                {

                    throw new ValidacionSincronizacionException("No se pudo detectar la ruta de datos de SQL Server. Asegúrese de que la instancia SQL esté accesible y que el usuario tenga permisos para ejecutar consultas.");

                }

                return salida;

            }
            finally
            {

                ssh.Disconnect();

            }
        }


        public List<string> ListarZipsDisponibles(ConexionRestauracion conexion)
        {

            var connectionInfo = ConstruirConnecionInfo(conexion);
            using var ssh = new SshClient(connectionInfo);
            ssh.Connect();

            try
            {

                var carpetaEscapada = conexion.CarpetaZips.Replace("'", "''");

                var existeScript = $"Test-Path -LiteralPath '{carpetaEscapada}'";
                var existe = EjecutarPowerShellRemoto(ssh, existeScript).Trim();

                if (!string.Equals(existe, "True", StringComparison.OrdinalIgnoreCase))
                {

                    throw new ValidacionSincronizacionException("La carpeta de zips especificada no existe en el servidor remoto. Verifique la ruta y los permisos de acceso.");


                }

                var script = $"Get-ChildItem -LiteralPath '{carpetaEscapada}' -Filter '*.zip' | Select-Object -ExpandProperty Name";
                var salida = EjecutarPowerShellRemoto(ssh, script);

                return salida
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .OrderByDescending(s => s)
                    .ToList();
            }
            finally
            {
                ssh.Disconnect();

            }

        }

        public void EjecutarRestauracion(ConexionRestauracion conexion, string nombreZip, CancellationToken cancelacion = default)
        {
            var connectionInfo = ConstruirConnecionInfo(conexion);
            using var ssh = new SshClient(connectionInfo);
            _sshActivo = ssh;

            Log?.Invoke($"Conectando al servidor {conexion.Servidor}...");
            ssh.Connect();
            Log?.Invoke($"Conexión establecida. Restaurando desde: {nombreZip}");

            try
            {

                var script = ConstruirScriptRestauracion(conexion, nombreZip);
                var bytes = Encoding.Unicode.GetBytes(script);
                var base64 = Convert.ToBase64String(bytes);

                using var comando = ssh.CreateCommand($"powershell -NoProfile -EncodedCommand {base64}");
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
        private string ConstruirScriptRestauracion(ConexionRestauracion conexion, string nombreZip)
        {
            var instancia = conexion.InstanciaSql.Replace("'", "''");
            var carpetaZips = conexion.CarpetaZips.Replace("'", "''");
            var rutaDatos = conexion.RutaDatosSql.Replace("'", "''");
            var zipEscapado = nombreZip.Replace("'", "''");

            var sb = new StringBuilder();

            sb.AppendLine($"$ServerInstance = '{instancia}'");
            sb.AppendLine($"$LocalRestorePath = '{carpetaZips}'");
            sb.AppendLine("$UnzipPath = Join-Path $LocalRestorePath 'TempRestore'");
            sb.AppendLine($"$LocalDataPath = '{rutaDatos}'");
            sb.AppendLine($"$MasterZipFile = Join-Path $LocalRestorePath '{zipEscapado}'");

            sb.AppendLine("if (-not (Test-Path $MasterZipFile)) {");
            sb.AppendLine("    Write-Host \"[ERROR] No se encontró el ZIP: $MasterZipFile\"");
            sb.AppendLine("    exit 1");
            sb.AppendLine("}");

            sb.AppendLine("if (-not (Test-Path $UnzipPath)) {");
            sb.AppendLine("    New-Item -Path $UnzipPath -ItemType Directory | Out-Null");
            sb.AppendLine("}");

            sb.AppendLine("Write-Host \"--- Descomprimiendo $(Split-Path $MasterZipFile -Leaf) ---\"");
            sb.AppendLine("Expand-Archive -Path $MasterZipFile -DestinationPath $UnzipPath -Force");

            sb.AppendLine("$BakFiles = Get-ChildItem -Path $UnzipPath -Filter '*.bak'");
            sb.AppendLine("if (-not $BakFiles) {");
            sb.AppendLine("    Write-Host '[AVISO] No se encontraron .bak dentro del ZIP.'");
            sb.AppendLine("    exit 0");
            sb.AppendLine("}");

            sb.AppendLine("foreach ($BakFile in $BakFiles) {");
            sb.AppendLine("    $DBName = $BakFile.BaseName");
            sb.AppendLine("    Write-Host \"--- Procesando: $($BakFile.Name) ---\"");
            sb.AppendLine("    $FilelistQuery = \"RESTORE FILELISTONLY FROM DISK = N'$($BakFile.FullName)'\"");
            sb.AppendLine("    $RestoreList = sqlcmd -S $ServerInstance -E -Q $FilelistQuery -h-1 -W -s\",\"");
            sb.AppendLine("    $DataLogicalName = ''");
            sb.AppendLine("    $LogLogicalName = ''");
            sb.AppendLine("    foreach ($line in $RestoreList) {");
            sb.AppendLine("        if ($line -like '*D*PRIMARY*') { $DataLogicalName = ($line -split ',')[0].Trim() }");
            sb.AppendLine("        if ($line -like '*L*') { $LogLogicalName = ($line -split ',')[0].Trim() }");
            sb.AppendLine("    }");
            sb.AppendLine("    if (-not $DataLogicalName -or -not $LogLogicalName) {");
            sb.AppendLine("        Write-Host \"[AVISO] No se encontraron nombres lógicos para $($BakFile.Name). Omitiendo.\"");
            sb.AppendLine("        continue");
            sb.AppendLine("    }");
            sb.AppendLine("    $DataFile = Join-Path $LocalDataPath \"$($DBName).mdf\"");
            sb.AppendLine("    $LogFile = Join-Path $LocalDataPath \"$($DBName)_log.ldf\"");
            sb.AppendLine("    $RestoreCommand = \"RESTORE DATABASE [$DBName] FROM DISK = N'$($BakFile.FullName)' WITH REPLACE, STATS = 10,\"");
            sb.AppendLine("    $RestoreCommand += \"MOVE '$DataLogicalName' TO '$DataFile',\"");
            sb.AppendLine("    $RestoreCommand += \"MOVE '$LogLogicalName' TO '$LogFile';\"");
            sb.AppendLine("    Write-Host \"Restaurando $DBName...\"");
            sb.AppendLine("    sqlcmd -S $ServerInstance -E -Q $RestoreCommand | Out-Null");
            sb.AppendLine("    Write-Host \"Restauración de $DBName completada.\"");
            sb.AppendLine("}");

            sb.AppendLine("Remove-Item $UnzipPath -Recurse -Force");
            sb.AppendLine("Write-Host '=== RESTAURACIÓN FINALIZADA ==='");

            return sb.ToString();
        }
    }
}
 