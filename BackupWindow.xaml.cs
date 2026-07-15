using BackupSyncApp.Models;
using BackupSyncApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BackupSyncApp;

public partial class BackupWindow : Window
{
    private readonly ConexionBackupSqlStorageService _storage = new();
    private List<ConexionBackupSql> _conexiones = new();

    private readonly ObservableCollection<LineaLog> _lineasLog = new();

    private bool _generando = false;
    private CancellationTokenSource? _cts;
    private GeneradorBackupService? _servicioActivo;
    private object? _tokenActual;

    public BackupWindow()
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
        var ventana = new ConexionBackupSqlWindow { Owner = this };
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
        if (ListaConexiones.SelectedItem is not ConexionBackupSql seleccionada)
        {
            MessageBox.Show("Selecciona un servidor de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ventana = new ConexionBackupSqlWindow(seleccionada) { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    private void BtnEliminar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaConexiones.SelectedItem is not ConexionBackupSql seleccionada)
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

    // ===================== Generación de backup =====================

    private async void BtnGenerarBackup_Click(object sender, RoutedEventArgs e)
    {
        if (_generando)
        {
            MessageBox.Show("Ya hay un backup en curso. Espera a que termine o cancélalo.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var boton = (Button)sender;
        var conexion = (ConexionBackupSql)boton.Tag;

        var confirmacion = MessageBox.Show(
            $"¿Generar un backup nuevo en \"{conexion.Nombre}\" ahora?\n\nEsto va a crear archivos .bak reales en el servidor.",
            "Confirmar generación de backup", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmacion != MessageBoxResult.Yes)
        {
            return;
        }

        await EjecutarGeneracion(conexion);
    }

    private async Task EjecutarGeneracion(ConexionBackupSql conexion)
    {
        var miToken = new object();
        _tokenActual = miToken;

        _generando = true;
        BtnCancelar.IsEnabled = true;
        _lineasLog.Clear();
        TxtEstadoActual.Text = $"Generando backup en {conexion.Nombre}...";

        _cts = new CancellationTokenSource();

        var servicio = new GeneradorBackupService();
        _servicioActivo = servicio;

        servicio.Log += texto =>
        {
            if (_tokenActual != miToken) return;
            Dispatcher.Invoke(() => AgregarLog(texto, Brushes.DimGray));
        };

        try
        {
            await Task.Run(() => servicio.GenerarBackup(conexion, _cts.Token), _cts.Token);

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
                MessageBox.Show($"Ocurrió un error al generar el backup:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            if (_tokenActual == miToken)
            {
                _generando = false;
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

        if (_tokenActual == tokenAlCancelar && _generando)
        {
            AgregarLog("=== CANCELADO (forzado; la conexión no respondió a tiempo) ===", Brushes.DarkOrange);
            TxtEstadoActual.Text = "Cancelado (forzado)";
            _generando = false;
            BtnCancelar.IsEnabled = false;
            _cts = null;
            _servicioActivo = null;
            _tokenActual = null;
        }
    }

    private void BtnVolverMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_generando)
        {
            MessageBox.Show("Espera a que termine o cancela el backup antes de volver al menú.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new MenuWindow();
        menu.Show();
        this.Close();
    }

    // ===================== Ventana custom (sin borde nativo) =====================

    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    private void BtnCerrar_Click(object sender, RoutedEventArgs e)
    {
        if (_generando)
        {
            MessageBox.Show("Espera a que termine o cancela el backup antes de cerrar la ventana.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        this.Close();
    }
}