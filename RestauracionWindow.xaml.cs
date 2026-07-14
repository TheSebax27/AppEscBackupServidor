using BackupSyncApp.Models;
using BackupSyncApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BackupSyncApp;

public partial class RestauracionWindow : Window
{
    private readonly ConexionRestauracionStorageService _storage = new();
    private List<ConexionRestauracion> _conexiones = new();

    private readonly ObservableCollection<LineaLog> _lineasLog = new();

    private bool _restaurando = false;
    private CancellationTokenSource? _cts;
    private RestauracionService? _servicioActivo;
    private object? _tokenActual;

    public RestauracionWindow()
    {
        InitializeComponent();

        _conexiones = _storage.Cargar();
        RefrescarLista();

        ListaLog.ItemsSource = _lineasLog;
    }

    private void RefrescarLista()
    {
        ListaConexiones.ItemsSource = null;
        ListaConexiones.ItemsSource = _conexiones;
    }

    // ===================== Gestión de conexiones =====================

    private void BtnAgregar_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new ConexionRestauracionWindow { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _conexiones.Add(ventana.Conexion);
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    private void BtnEditar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaConexiones.SelectedItem is not ConexionRestauracion seleccionada)
        {
            MessageBox.Show("Selecciona un servidor de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ventana = new ConexionRestauracionWindow(seleccionada) { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    private void BtnEliminar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaConexiones.SelectedItem is not ConexionRestauracion seleccionada)
        {
            MessageBox.Show("Selecciona un servidor de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacion = MessageBox.Show(
            $"¿Eliminar la conexión \"{seleccionada.Nombre}\"?\n\nEsto solo borra la configuración guardada.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacion == MessageBoxResult.Yes)
        {
            _conexiones.Remove(seleccionada);
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    // ===================== Restauración =====================

    private async void BtnRestaurar_Click(object sender, RoutedEventArgs e)
    {
        if (_restaurando)
        {
            MessageBox.Show("Ya hay una restauración en curso. Espera a que termine o cancélala.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var boton = (Button)sender;
        var conexion = (ConexionRestauracion)boton.Tag;

        List<string> zips;
        try
        {
            TxtEstadoActual.Text = $"Consultando ZIPs disponibles en {conexion.Nombre}...";
            var servicioListado = new RestauracionService();
            zips = await Task.Run(() => servicioListado.ListarZipsDisponibles(conexion));
        }
        catch (Exception ex)
        {
            TxtEstadoActual.Text = "Error al consultar ZIPs disponibles";
            MessageBox.Show($"No se pudo consultar la carpeta de ZIPs:\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (zips.Count == 0)
        {
            TxtEstadoActual.Text = "Sin ZIPs disponibles";
            MessageBox.Show($"No se encontraron archivos .zip en:\n{conexion.CarpetaZips}",
                "Sin backups disponibles", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ventanaConfirmar = new ConfirmarRestauracionWindow(zips) { Owner = this };
        bool? confirmado = ventanaConfirmar.ShowDialog();

        if (confirmado != true || ventanaConfirmar.ZipSeleccionado == null)
        {
            TxtEstadoActual.Text = "Restauración cancelada por el usuario";
            return;
        }

        await EjecutarRestauracion(conexion, ventanaConfirmar.ZipSeleccionado);
    }

    private async Task EjecutarRestauracion(ConexionRestauracion conexion, string nombreZip)
    {
        var miToken = new object();
        _tokenActual = miToken;

        _restaurando = true;
        BtnCancelar.IsEnabled = true;
        _lineasLog.Clear();
        TxtEstadoActual.Text = $"Restaurando en {conexion.Nombre} desde {nombreZip}...";

        _cts = new CancellationTokenSource();

        var servicio = new RestauracionService();
        _servicioActivo = servicio;

        servicio.Log += texto =>
        {
            if (_tokenActual != miToken) return;
            Dispatcher.Invoke(() => AgregarLog(texto, Brushes.DimGray));
        };

        try
        {
            await Task.Run(() => servicio.EjecutarRestauracion(conexion, nombreZip, _cts.Token), _cts.Token);

            if (_tokenActual != miToken) return;

            TxtEstadoActual.Text = $"Finalizado: {conexion.Nombre}";
            AgregarLog("=== PROCESO FINALIZADO ===", Brushes.SteelBlue);
        }
        catch (OperationCanceledException)
        {
            if (_tokenActual != miToken) return;
            TxtEstadoActual.Text = "Cancelado por el usuario";
            AgregarLog("=== CANCELADO POR EL USUARIO ===", Brushes.DarkOrange);
        }
        catch (Exception ex)
        {
            if (_tokenActual != miToken) return;

            if (_cts?.IsCancellationRequested == true)
            {
                TxtEstadoActual.Text = "Cancelado por el usuario";
                AgregarLog("=== CANCELADO POR EL USUARIO ===", Brushes.DarkOrange);
            }
            else
            {
                TxtEstadoActual.Text = "Error";
                AgregarLog($"[ERROR FATAL] {ex.Message}", Brushes.Red);
                MessageBox.Show($"Ocurrió un error al restaurar:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            if (_tokenActual == miToken)
            {
                _restaurando = false;
                BtnCancelar.IsEnabled = false;
                _cts = null;
                _servicioActivo = null;
                _tokenActual = null;
            }
        }
    }

    private void AgregarLog(string texto, Brush color)
    {
        _lineasLog.Add(new LineaLog { Texto = texto, Color = color });

        if (_lineasLog.Count > 0)
        {
            ListaLog.ScrollIntoView(_lineasLog[^1]);
        }
    }

    private async void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _servicioActivo?.ForzarDesconexion();
        TxtEstadoActual.Text = "Cancelando...";
        BtnCancelar.IsEnabled = false;

        var tokenAlCancelar = _tokenActual;
        await Task.Delay(5000);

        if (_tokenActual == tokenAlCancelar && _restaurando)
        {
            AgregarLog("=== CANCELADO (forzado; la conexión no respondió a tiempo) ===", Brushes.DarkOrange);
            TxtEstadoActual.Text = "Cancelado (forzado)";
            _restaurando = false;
            BtnCancelar.IsEnabled = false;
            _cts = null;
            _servicioActivo = null;
            _tokenActual = null;
        }
    }

    private void BtnVolverMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_restaurando)
        {
            MessageBox.Show("Espera a que termine o cancela la restauración antes de volver al menú.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new MenuWindow();
        menu.Show();
        this.Close();
    }
}