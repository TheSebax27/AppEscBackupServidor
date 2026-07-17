using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BackupSyncApp.Models;
using BackupSyncApp.Services;

namespace BackupSyncApp;

public partial class ProgramacionWindow : Window
{
    private readonly TareaProgramadaStorageService _storage = new();
    private readonly ProgramadorWindowsService _programadorWindows = new();
    private List<TareaProgramada> _tareas = new();

    private readonly ObservableCollection<LineaLog> _lineasLog = new();

    private bool _ejecutando = false;
    private CancellationTokenSource? _cts;
    private object? _tokenActual;

    public ProgramacionWindow()
    {
        InitializeComponent();

        _tareas = _storage.Cargar();
        RefrescarLista();

        ListaLog.ItemsSource = _lineasLog;
    }

    private void RefrescarLista()
    {
        ListaTareas.ItemsSource = null;
        ListaTareas.ItemsSource = _tareas;
    }

    // ===================== Gestión de tareas =====================

    private void BtnAgregar_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new TareaProgramadaWindow { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _tareas.Add(ventana.Tarea);
            _storage.Guardar(_tareas);

            if (ventana.Tarea.Activada)
            {
                AplicarRegistroEnWindows(ventana.Tarea);
            }

            RefrescarLista();
        }
    }

    private void BtnEditar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaTareas.SelectedItem is not TareaProgramada seleccionada)
        {
            MessageBox.Show("Selecciona una tarea de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        bool estabaActivada = seleccionada.Activada;

        var ventana = new TareaProgramadaWindow(seleccionada) { Owner = this };
        bool? resultado = ventana.ShowDialog();

        if (resultado == true)
        {
            _storage.Guardar(_tareas);

            // Si cambió cualquier dato del horario o el estado activada/desactivada,
            // hay que re-registrar en Windows para que tome los cambios.
            if (seleccionada.Activada)
            {
                AplicarRegistroEnWindows(seleccionada);
            }
            else if (estabaActivada && !seleccionada.Activada)
            {
                _programadorWindows.EliminarTarea(seleccionada);
            }

            RefrescarLista();
        }
    }

    private void BtnEliminar_Click(object sender, RoutedEventArgs e)
    {
        if (ListaTareas.SelectedItem is not TareaProgramada seleccionada)
        {
            MessageBox.Show("Selecciona una tarea de la lista primero.", "Aviso",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacion = MessageBox.Show(
            $"¿Eliminar la tarea \"{seleccionada.Nombre}\"?\n\nSi está activada, también se quitará del Programador de Windows.",
            "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacion == MessageBoxResult.Yes)
        {
            if (seleccionada.Activada)
            {
                _programadorWindows.EliminarTarea(seleccionada);
            }

            _tareas.Remove(seleccionada);
            _storage.Guardar(_tareas);
            RefrescarLista();
        }
    }

    private void BtnActivarDesactivar_Click(object sender, RoutedEventArgs e)
    {
        var boton = (Button)sender;
        var tarea = (TareaProgramada)boton.Tag;

        if (tarea.Activada)
        {
            // Estaba activada -> la desactivamos y la quitamos de Windows
            var (exito, mensaje) = _programadorWindows.EliminarTarea(tarea);
            tarea.Activada = false;
            tarea.RegistradaEnWindows = false;
            _storage.Guardar(_tareas);

            TxtEstadoActual.Text = exito
                ? $"'{tarea.Nombre}' desactivada."
                : $"'{tarea.Nombre}' desactivada localmente (aviso al quitar de Windows: {mensaje})";
        }
        else
        {
            tarea.Activada = true;
            AplicarRegistroEnWindows(tarea);
        }

        RefrescarLista();
    }

    private void AplicarRegistroEnWindows(TareaProgramada tarea)
    {
        var (exito, mensaje) = _programadorWindows.RegistrarTarea(tarea);
        tarea.RegistradaEnWindows = exito;
        _storage.Guardar(_tareas);

        TxtEstadoActual.Text = exito
            ? $"'{tarea.Nombre}' activada y registrada en el Programador de Windows."
            : $"[ERROR] No se pudo registrar '{tarea.Nombre}' en Windows: {mensaje}";

        if (!exito)
        {
            MessageBox.Show(
                $"No se pudo registrar la tarea en el Programador de Windows:\n\n{mensaje}\n\n" +
                "Prueba ejecutando Visual Studio (o el .exe) como Administrador.",
                "Error al activar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===================== Probar ahora =====================

    private async void BtnProbarAhora_Click(object sender, RoutedEventArgs e)
    {
        if (_ejecutando)
        {
            MessageBox.Show("Ya hay una prueba en curso. Espera a que termine o cancélala.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var boton = (Button)sender;
        var tarea = (TareaProgramada)boton.Tag;

        var confirmacion = MessageBox.Show(
            $"¿Ejecutar la tarea \"{tarea.Nombre}\" ahora mismo, como prueba?\n\nEsto genera un backup real.",
            "Confirmar prueba", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (confirmacion != MessageBoxResult.Yes)
        {
            return;
        }

        var miToken = new object();
        _tokenActual = miToken;

        _ejecutando = true;
        BtnCancelar.IsEnabled = true;
        _lineasLog.Clear();
        TxtEstadoActual.Text = $"Probando: {tarea.Nombre}...";

        _cts = new CancellationTokenSource();

        var ejecutor = new EjecutorTareaProgramada();
        ejecutor.Log += texto =>
        {
            if (_tokenActual != miToken) return;
            Dispatcher.Invoke(() => AgregarLog(texto, Brushes.DimGray));
        };

        try
        {
            bool exito = await Task.Run(() => ejecutor.Ejecutar(tarea, _cts.Token), _cts.Token);

            if (_tokenActual != miToken) return;

            TxtEstadoActual.Text = exito ? $"Prueba finalizada: {tarea.Nombre}" : $"Prueba con errores: {tarea.Nombre}";
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
            TxtEstadoActual.Text = "Error";
            AgregarLog($"[ERROR FATAL] {ex.Message}", Brushes.Red);
        }
        finally
        {
            if (_tokenActual == miToken)
            {
                _ejecutando = false;
                BtnCancelar.IsEnabled = false;
                _cts = null;
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
        TxtEstadoActual.Text = "Cancelando...";
        BtnCancelar.IsEnabled = false;

        var tokenAlCancelar = _tokenActual;
        await Task.Delay(5000);

        if (_tokenActual == tokenAlCancelar && _ejecutando)
        {
            AgregarLog("=== CANCELADO (forzado) ===", Brushes.DarkOrange);
            TxtEstadoActual.Text = "Cancelado (forzado)";
            _ejecutando = false;
            BtnCancelar.IsEnabled = false;
            _cts = null;
            _tokenActual = null;
        }
    }

    private void BtnVolverMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_ejecutando)
        {
            MessageBox.Show("Espera a que termine o cancela la prueba antes de volver al menú.",
                "Ocupado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new MenuWindow();
        menu.Show();
        this.Close();
    }
}