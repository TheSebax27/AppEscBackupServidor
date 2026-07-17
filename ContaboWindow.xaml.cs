using BackupSyncApp.Models;
using BackupSyncApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BackupSyncApp;

public partial class ContaboWindow : Window
{
    private readonly ConexionBackupContaboStorageService _storage = new();
    private List<ConexionBackupContabo> _conexiones = new();

    private readonly ObservableCollection<LineaLog> _lineasLog = new();

    private bool _ejecutando = false;
    private CancellationTokenSource? _cts;
    private BackupContaboService? _servicioActivo;
    private object? _tokenActual;

    public ContaboWindow()
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
        var ventana = new ConexionBackupContaboWindow { Owner = this };
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
        if (ListaConexiones.SelectedItem is not ConexionBackupContabo seleccionada)
        {
            MessageBox.Show("Selecciona un servidor de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ventana = new ConexionBackupContaboWindow(seleccionada) { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    private void BtnEliminar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaConexiones.SelectedItem is not ConexionBackupContabo seleccionada)
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

    // ===================== Ejecución de backups =====================

    private async void BtnBackupBd_Click(object sender, RoutedEventArgs e)
    {
        var boton = (Button)sender;
        var conexion = (ConexionBackupContabo)boton.Tag;

        if (string.IsNullOrWhiteSpace(conexion.NombreBaseDatos) || conexion.Tablas.Count == 0)
        {
            MessageBox.Show("Esta conexión no tiene configurado el backup de base de datos. Edítala primero.",
                "Sin configurar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacion = MessageBox.Show(
            $"¿Generar backup de base de datos en \"{conexion.Nombre}\" ahora?",
            "Confirmar backup de BD", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmacion != MessageBoxResult.Yes) return;

        await EjecutarAccion(conexion, esBaseDatos: true);
    }

    private async void BtnBackupArchivos_Click(object sender, RoutedEventArgs e)
    {
        var boton = (Button)sender;
        var conexion = (ConexionBackupContabo)boton.Tag;

        if (conexion.CarpetasOrigenArchivos.Count == 0)
        {
            MessageBox.Show("Esta conexión no tiene configurado el backup de archivos. Edítala primero.",
                "Sin configurar", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacion = MessageBox.Show(
            $"¿Generar backup de archivos en \"{conexion.Nombre}\" ahora?\n\n" +
            "Esto BORRA el contenido de las carpetas de origen (excepto la primera) tras enviar el zip con éxito.",
            "Confirmar backup de archivos", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacion != MessageBoxResult.Yes) return;

        await EjecutarAccion(conexion, esBaseDatos: false);
    }

    private async Task EjecutarAccion(ConexionBackupContabo conexion, bool esBaseDatos)
    {
        if (_ejecutando)
        {
            MessageBox.Show("Ya hay un backup en curso. Espera a que termine o cancélalo.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var miToken = new object();
        _tokenActual = miToken;

        _ejecutando = true;
        BtnCancelar.IsEnabled = true;
        _lineasLog.Clear();
        TxtEstadoActual.Text = $"{(esBaseDatos ? "Backup de BD" : "Backup de archivos")} en {conexion.Nombre}...";

        _cts = new CancellationTokenSource();

        var servicio = new BackupContaboService();
        _servicioActivo = servicio;

        servicio.Log += texto =>
        {
            if (_tokenActual != miToken) return;
            Dispatcher.Invoke(() => AgregarLog(texto, Brushes.DimGray));
        };

        try
        {
            await Task.Run(() =>
            {
                if (esBaseDatos)
                    servicio.GenerarBackupBaseDatos(conexion, _cts.Token);
                else
                    servicio.GenerarBackupArchivos(conexion, _cts.Token);
            }, _cts.Token);

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
                MessageBox.Show($"Ocurrió un error:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            if (_tokenActual == miToken)
            {
                _ejecutando = false;
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

        if (_tokenActual == tokenAlCancelar && _ejecutando)
        {
            AgregarLog("=== CANCELADO (forzado; la conexión no respondió a tiempo) ===", Brushes.DarkOrange);
            TxtEstadoActual.Text = "Cancelado (forzado)";
            _ejecutando = false;
            BtnCancelar.IsEnabled = false;
            _cts = null;
            _servicioActivo = null;
            _tokenActual = null;
        }
    }

    private void BtnVolverMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_ejecutando)
        {
            MessageBox.Show("Espera a que termine o cancela el backup antes de volver al menú.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new MenuWindow();
        menu.Show();
        this.Close();
    }
}