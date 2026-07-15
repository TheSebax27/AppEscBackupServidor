using BackupSyncApp.Models;
using BackupSyncApp.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BackupSyncApp;

public partial class MainWindow : Window
{
    private readonly ConexionStorageService _storage = new();
    private List<ConexionBackup> _conexiones = new();

    // Colección "observable": cuando le agregamos elementos, la UI se
    // actualiza sola automáticamente, sin que tengamos que refrescar a mano.
    private readonly ObservableCollection<LineaLog> _lineasLog = new();

    private bool _sincronizando = false;
    private CancellationTokenSource? _cts;
    private SincronizacionService? _servicioActivo;
    private object? _tokenSincronizacionActual;

    public MainWindow()
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

    // ===================== Gestión de conexiones (igual que antes) =====================

    private void BtnAgregar_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new ConexionWindow { Owner = this };
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
        if (ListaConexiones.SelectedItem is not ConexionBackup seleccionada)
        {
            MessageBox.Show("Selecciona una conexión de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ventana = new ConexionWindow(seleccionada) { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    private void BtnEliminar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaConexiones.SelectedItem is not ConexionBackup seleccionada)
        {
            MessageBox.Show("Selecciona una conexión de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacion = MessageBox.Show(
            $"¿Eliminar la conexión \"{seleccionada.Nombre}\"?\n\nEsto solo borra la configuración guardada, NO borra archivos.",
            "Confirmar eliminación",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacion == MessageBoxResult.Yes)
        {
            _conexiones.Remove(seleccionada);
            _storage.Guardar(_conexiones);
            RefrescarLista();
        }
    }

    // ===================== Sincronización (nuevo en Etapa 4) =====================

    private async void BtnSincronizar_Click(object sender, RoutedEventArgs e)
    {
        if (_sincronizando)
        {
            MessageBox.Show("Ya hay una sincronización en curso. Espera a que termine o cancélala.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var boton = (Button)sender;
        var conexion = (ConexionBackup)boton.Tag;

        await EjecutarSincronizacion(conexion);
    }

    private async Task EjecutarSincronizacion(ConexionBackup conexion)
    {
        var miToken = new object();
        _tokenSincronizacionActual = miToken;

        _sincronizando = true;
        BtnCancelar.IsEnabled = true;
        BarraProgreso.Value = 0;
        _lineasLog.Clear();
        TxtResumen.Text = "";
        TxtEstadoActual.Text = $"Conectando a {conexion.Nombre}...";

        _cts = new CancellationTokenSource();

        var servicio = new SincronizacionService();
        _servicioActivo = servicio;

        // Los eventos del servicio llegan desde un hilo de fondo (Task.Run),
        // así que usamos Dispatcher.Invoke para tocar la UI de forma segura.
        // Además comparamos el token: si esta sincronización ya fue "abandonada"
        // (ver BtnCancelar_Click), ignoramos cualquier evento tardío suyo.
        servicio.Log += texto =>
        {
            if (_tokenSincronizacionActual != miToken) return;
            Dispatcher.Invoke(() => AgregarLog(texto, Brushes.DimGray));
        };

        servicio.ArchivoActualizado += evento =>
        {
            if (_tokenSincronizacionActual != miToken) return;
            Dispatcher.Invoke(() => ManejarEventoArchivo(evento));
        };

        try
        {
            var resumen = await Task.Run(
                () => servicio.Sincronizar(conexion, _cts.Token), _cts.Token);

            if (_tokenSincronizacionActual != miToken) return; // ya fue abandonada

            TxtEstadoActual.Text = $"Finalizado: {conexion.Nombre}";
            TxtResumen.Text =
                $"Descargados: {resumen.Descargados}  |  Ya existían: {resumen.Existentes}  |  " +
                $"Movidos: {resumen.Movidos}  |  Avisos: {resumen.Avisos}  |  Errores: {resumen.Errores}";

            AgregarLog("=== PROCESO FINALIZADO ===", Brushes.SteelBlue);
        }
        catch (OperationCanceledException)
        {
            if (_tokenSincronizacionActual != miToken) return;
            TxtEstadoActual.Text = "Cancelado por el usuario";
            AgregarLog("=== CANCELADO POR EL USUARIO ===", Brushes.DarkOrange);
        }
        catch (ValidacionSincronizacionException ex)
        {
            if (_tokenSincronizacionActual != miToken) return;
            TxtEstadoActual.Text = "Origen o destino no existente, por favor verificar";
            AgregarLog($"[VALIDACIÓN] {ex.Message}", Brushes.DarkOrange);
            MessageBox.Show(ex.Message, "Origen o destino no existente, por favor verificar",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            if (_tokenSincronizacionActual != miToken) return;

            if (_cts?.IsCancellationRequested == true)
            {
                TxtEstadoActual.Text = "Cancelado por el usuario";
                AgregarLog("=== CANCELADO POR EL USUARIO ===", Brushes.DarkOrange);
            }
            else
            {
                TxtEstadoActual.Text = "Error";
                AgregarLog($"[ERROR FATAL] {ex.Message}", Brushes.Red);
                MessageBox.Show($"Ocurrió un error al sincronizar:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            // Si el token ya no coincide, esta tarea fue abandonada por el
            // "watchdog" de Cancelar (ver BtnCancelar_Click) y puede que ya
            // haya una sincronización nueva corriendo — no tocamos su estado.
            if (_tokenSincronizacionActual == miToken)
            {
                _sincronizando = false;
                BtnCancelar.IsEnabled = false;
                _cts = null;
                _servicioActivo = null;
                _tokenSincronizacionActual = null;
            }
        }
    }

    private void ManejarEventoArchivo(EventoArchivo evento)
    {
        switch (evento.Estado)
        {
            case EstadoArchivo.Existe:
                AgregarLog($"[EXISTE] {evento.RutaRelativa}", Brushes.Gray);
                break;

            case EstadoArchivo.Descargando:
                BarraProgreso.Value = evento.PorcentajeProgreso ?? 0;
                TxtEstadoActual.Text = $"Descargando: {evento.RutaRelativa} ({evento.PorcentajeProgreso}%)";
                break;

            case EstadoArchivo.Descargado:
                BarraProgreso.Value = 100;
                AgregarLog($"[DESCARGADO] {evento.RutaRelativa}", Brushes.ForestGreen);
                break;

            case EstadoArchivo.VerificandoIntegridad:
                AgregarLog($"[VERIFICANDO] {evento.RutaRelativa}", Brushes.Gray);
                break;

            case EstadoArchivo.Movido:
                AgregarLog($"[MOVIDO] {evento.RutaRelativa} (original borrado tras verificar)", Brushes.MediumPurple);
                break;

            case EstadoArchivo.ErrorDescarga:
                AgregarLog($"[ERROR] {evento.RutaRelativa}: {evento.Mensaje}", Brushes.Red);
                break;

            case EstadoArchivo.ErrorVerificacion:
                AgregarLog($"[AVISO] {evento.RutaRelativa}: {evento.Mensaje}", Brushes.DarkOrange);
                break;
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

        var tokenAlCancelar = _tokenSincronizacionActual;

        // Le damos un margen para que la desconexión se propague normalmente.
        await Task.Delay(5000);

        // Si después de 5s seguimos "atascados" en la MISMA sincronización,
        // la abandonamos: liberamos la UI para que puedas seguir usando la app,
        // aunque la tarea vieja siga muriendo sola en segundo plano.
        if (_tokenSincronizacionActual == tokenAlCancelar && _sincronizando)
        {
            AgregarLog("=== CANCELADO (forzado; la conexión no respondió a tiempo) ===", Brushes.DarkOrange);
            TxtEstadoActual.Text = "Cancelado (forzado)";
            _sincronizando = false;
            BtnCancelar.IsEnabled = false;
            _cts = null;
            _servicioActivo = null;
            _tokenSincronizacionActual = null;
        }
    }

    private void BtnVolverMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_sincronizando)
        {
            MessageBox.Show("Espera a que termine o cancela la sincronización antes de volver al menú.",
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
        if (_sincronizando)
        {
            MessageBox.Show("Espera a que termine o cancela la sincronización antes de cerrar la ventana.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        this.Close();
    }
}