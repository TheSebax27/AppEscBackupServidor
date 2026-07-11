using System.Windows;
using Microsoft.Win32;
using BackupSyncApp.Models;

namespace BackupSyncApp;

public partial class ConexionBackupSqlWindow : Window
{
    public ConexionBackupSql Conexion { get; private set; }

    public ConexionBackupSqlWindow(ConexionBackupSql? conexionExistente = null)
    {
        InitializeComponent();

        Conexion = conexionExistente ?? new ConexionBackupSql();
        CargarDatosEnFormulario();
    }

    private void CargarDatosEnFormulario()
    {
        TxtNombre.Text = Conexion.Nombre;
        TxtServidor.Text = Conexion.Servidor;
        TxtUsuario.Text = Conexion.Usuario;
        TxtLlave.Text = Conexion.RutaLlavePrivada;

        TxtInstanciaSql.Text = Conexion.InstanciaSql;
        TxtIdentificador.Text = Conexion.Identificador;
        TxtRutaBackupLocal.Text = Conexion.RutaBackupLocal;

        ChkEnviarAlCentral.IsChecked = Conexion.EnviarAlCentralPorScp;
        TxtCentralUsuario.Text = Conexion.CentralUsuario;
        TxtCentralIp.Text = Conexion.CentralIp;
        TxtCentralDestino.Text = Conexion.CentralDestino;
        TxtCentralLlave.Text = Conexion.CentralRutaLlaveEnSatelite;

        ActualizarVisibilidadPanelCentral();
    }

    private void ChkEnviarAlCentral_Changed(object sender, RoutedEventArgs e)
    {
        ActualizarVisibilidadPanelCentral();
    }

    private void ActualizarVisibilidadPanelCentral()
    {
        PanelCentral.Visibility = (ChkEnviarAlCentral.IsChecked == true)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void BtnBuscarLlave_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Selecciona tu llave privada SSH",
            Filter = "Todos los archivos|*.*"
        };

        if (dialogo.ShowDialog() == true)
        {
            TxtLlave.Text = dialogo.FileName;
        }
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNombre.Text) ||
            string.IsNullOrWhiteSpace(TxtServidor.Text) ||
            string.IsNullOrWhiteSpace(TxtUsuario.Text) ||
            string.IsNullOrWhiteSpace(TxtLlave.Text) ||
            string.IsNullOrWhiteSpace(TxtInstanciaSql.Text) ||
            string.IsNullOrWhiteSpace(TxtIdentificador.Text) ||
            string.IsNullOrWhiteSpace(TxtRutaBackupLocal.Text))
        {
            MessageBox.Show("Todos los campos de arriba son obligatorios.", "Datos incompletos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool enviarAlCentral = ChkEnviarAlCentral.IsChecked == true;

        if (enviarAlCentral &&
            (string.IsNullOrWhiteSpace(TxtCentralUsuario.Text) ||
             string.IsNullOrWhiteSpace(TxtCentralIp.Text) ||
             string.IsNullOrWhiteSpace(TxtCentralDestino.Text) ||
             string.IsNullOrWhiteSpace(TxtCentralLlave.Text)))
        {
            MessageBox.Show("Si activas el envío al central, todos esos campos son obligatorios.",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Conexion.Nombre = TxtNombre.Text.Trim();
        Conexion.Servidor = TxtServidor.Text.Trim();
        Conexion.Usuario = TxtUsuario.Text.Trim();
        Conexion.RutaLlavePrivada = TxtLlave.Text.Trim();

        Conexion.InstanciaSql = TxtInstanciaSql.Text.Trim();
        Conexion.Identificador = TxtIdentificador.Text.Trim();
        Conexion.RutaBackupLocal = TxtRutaBackupLocal.Text.Trim();

        Conexion.EnviarAlCentralPorScp = enviarAlCentral;
        Conexion.CentralUsuario = TxtCentralUsuario.Text.Trim();
        Conexion.CentralIp = TxtCentralIp.Text.Trim();
        Conexion.CentralDestino = TxtCentralDestino.Text.Trim();
        Conexion.CentralRutaLlaveEnSatelite = TxtCentralLlave.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}