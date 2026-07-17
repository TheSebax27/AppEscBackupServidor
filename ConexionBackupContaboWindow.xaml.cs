using System.Windows;
using Microsoft.Win32;
using BackupSyncApp.Models;

namespace BackupSyncApp;

public partial class ConexionBackupContaboWindow : Window
{
    public ConexionBackupContabo Conexion { get; private set; }

    public ConexionBackupContaboWindow(ConexionBackupContabo? conexionExistente = null)
    {
        InitializeComponent();

        Conexion = conexionExistente ?? new ConexionBackupContabo();
        CargarDatosEnFormulario();
    }

    private void CargarDatosEnFormulario()
    {
        TxtNombre.Text = Conexion.Nombre;
        TxtServidor.Text = Conexion.Servidor;
        TxtUsuario.Text = Conexion.Usuario;
        TxtLlave.Text = Conexion.RutaLlavePrivada;

        TxtNombreBd.Text = Conexion.NombreBaseDatos;
        TxtTablas.Text = string.Join(Environment.NewLine, Conexion.Tablas);
        TxtCarpetaBackupDb.Text = Conexion.CarpetaBackupLocalDb;

        TxtCarpetasOrigen.Text = string.Join(Environment.NewLine, Conexion.CarpetasOrigenArchivos);

        TxtCentralUsuario.Text = Conexion.CentralUsuario;
        TxtCentralIp.Text = Conexion.CentralIp;
        TxtCentralDestinoDb.Text = Conexion.CentralRutaDestinoDb;
        TxtCentralDestinoArchivos.Text = Conexion.CentralRutaDestinoArchivos;
        TxtLlaveEnContabo.Text = Conexion.RutaLlaveEnContabo;
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

    private static List<string> DividirLineas(string texto)
    {
        return texto
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNombre.Text) ||
            string.IsNullOrWhiteSpace(TxtServidor.Text) ||
            string.IsNullOrWhiteSpace(TxtUsuario.Text) ||
            string.IsNullOrWhiteSpace(TxtLlave.Text))
        {
            MessageBox.Show("Nombre, servidor, usuario y llave son obligatorios.", "Datos incompletos",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var tablas = DividirLineas(TxtTablas.Text);
        var carpetasOrigen = DividirLineas(TxtCarpetasOrigen.Text);

        bool tieneConfigDb = !string.IsNullOrWhiteSpace(TxtNombreBd.Text) && tablas.Count > 0
            && !string.IsNullOrWhiteSpace(TxtCarpetaBackupDb.Text);
        bool tieneConfigArchivos = carpetasOrigen.Count > 0;

        if (!tieneConfigDb && !tieneConfigArchivos)
        {
            MessageBox.Show(
                "Completa al menos la configuración de base de datos O la de archivos (pueden dejarse ambas, o solo una).",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        bool necesitaEnvioCentral = tieneConfigDb || tieneConfigArchivos;
        if (necesitaEnvioCentral &&
            (string.IsNullOrWhiteSpace(TxtCentralUsuario.Text) ||
             string.IsNullOrWhiteSpace(TxtCentralIp.Text) ||
             string.IsNullOrWhiteSpace(TxtLlaveEnContabo.Text)))
        {
            MessageBox.Show(
                "Usuario, IP y ruta de llave del central son obligatorios para poder enviar los backups.",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (tieneConfigDb && string.IsNullOrWhiteSpace(TxtCentralDestinoDb.Text))
        {
            MessageBox.Show("Falta la ruta destino en el central para el backup de base de datos.",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (tieneConfigArchivos && string.IsNullOrWhiteSpace(TxtCentralDestinoArchivos.Text))
        {
            MessageBox.Show("Falta la ruta destino en el central para el backup de archivos.",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Conexion.Nombre = TxtNombre.Text.Trim();
        Conexion.Servidor = TxtServidor.Text.Trim();
        Conexion.Usuario = TxtUsuario.Text.Trim();
        Conexion.RutaLlavePrivada = TxtLlave.Text.Trim();

        Conexion.NombreBaseDatos = TxtNombreBd.Text.Trim();
        Conexion.Tablas = tablas;
        Conexion.CarpetaBackupLocalDb = TxtCarpetaBackupDb.Text.Trim();

        Conexion.CarpetasOrigenArchivos = carpetasOrigen;

        Conexion.CentralUsuario = TxtCentralUsuario.Text.Trim();
        Conexion.CentralIp = TxtCentralIp.Text.Trim();
        Conexion.CentralRutaDestinoDb = TxtCentralDestinoDb.Text.Trim();
        Conexion.CentralRutaDestinoArchivos = TxtCentralDestinoArchivos.Text.Trim();
        Conexion.RutaLlaveEnContabo = TxtLlaveEnContabo.Text.Trim();

        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}