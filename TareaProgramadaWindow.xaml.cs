using System.Windows;
using System.Windows.Controls;
using BackupSyncApp.Models;
using BackupSyncApp.Services;

namespace BackupSyncApp;

public partial class TareaProgramadaWindow : Window
{
    public TareaProgramada Tarea { get; private set; }

    private readonly List<ConexionBackupSql> _conexionesSql;
    private readonly List<ConexionBackupContabo> _conexionesContabo;
    private readonly List<ConexionBackup> _conexionesArchivos;

    private bool _cargandoDatosIniciales = false;

    public TareaProgramadaWindow(TareaProgramada? tareaExistente = null)
    {
        InitializeComponent();

        Tarea = tareaExistente ?? new TareaProgramada();

        _conexionesSql = new ConexionBackupSqlStorageService().Cargar();
        _conexionesContabo = new ConexionBackupContaboStorageService().Cargar();
        _conexionesArchivos = new ConexionStorageService().Cargar();

        CmbConexionArchivos.ItemsSource = _conexionesArchivos;

        for (int h = 0; h < 24; h++) CmbHora.Items.Add(h.ToString("D2"));
        for (int m = 0; m < 60; m += 5) CmbMinuto.Items.Add(m.ToString("D2"));

        CargarDatosEnFormulario();
    }

    private void CargarDatosEnFormulario()
    {
        _cargandoDatosIniciales = true;

        TxtNombre.Text = Tarea.Nombre;

        CmbTipo.SelectedIndex = Tarea.Tipo switch
        {
            TipoTareaOrigen.BackupSqlWindows => 0,
            TipoTareaOrigen.ContaboBaseDatos => 1,
            TipoTareaOrigen.ContaboArchivos => 2,
            _ => 0
        };

        ActualizarListaConexionesSegunTipo();

        // Selecciona la conexión guardada, según a qué lista corresponda
        if (CmbConexionBackup.ItemsSource is List<ConexionBackupSql> listaSql)
        {
            CmbConexionBackup.SelectedItem = listaSql.FirstOrDefault(c => c.Id == Tarea.ConexionOrigenId);
        }
        else if (CmbConexionBackup.ItemsSource is List<ConexionBackupContabo> listaContabo)
        {
            CmbConexionBackup.SelectedItem = listaContabo.FirstOrDefault(c => c.Id == Tarea.ConexionOrigenId);
        }

        ChkEncadenar.IsChecked = Tarea.EncadenarConSincronizacion;
        CmbConexionArchivos.SelectedItem = _conexionesArchivos.FirstOrDefault(c => c.Id == Tarea.ConexionArchivosId);

        CmbHora.SelectedItem = Tarea.Hora.ToString("D2");
        CmbMinuto.SelectedItem = (Tarea.Minuto - (Tarea.Minuto % 5)).ToString("D2");

        ChkLun.IsChecked = Tarea.Dias.Contains(DayOfWeek.Monday);
        ChkMar.IsChecked = Tarea.Dias.Contains(DayOfWeek.Tuesday);
        ChkMie.IsChecked = Tarea.Dias.Contains(DayOfWeek.Wednesday);
        ChkJue.IsChecked = Tarea.Dias.Contains(DayOfWeek.Thursday);
        ChkVie.IsChecked = Tarea.Dias.Contains(DayOfWeek.Friday);
        ChkSab.IsChecked = Tarea.Dias.Contains(DayOfWeek.Saturday);
        ChkDom.IsChecked = Tarea.Dias.Contains(DayOfWeek.Sunday);

        ChkActivada.IsChecked = Tarea.Activada;

        ActualizarVisibilidadConexionArchivos();

        _cargandoDatosIniciales = false;
    }

    private void CmbTipo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ActualizarListaConexionesSegunTipo();
    }

    private void ActualizarListaConexionesSegunTipo()
    {
        // Al cambiar de tipo, la lista de servidores disponibles cambia.
        // Si el usuario ya había elegido uno, se pierde la selección (es
        // normal: un servidor SQL no aplica para un backup Contabo y viceversa).
        switch (CmbTipo.SelectedIndex)
        {
            case 0: // Backup SQL (Windows)
                CmbConexionBackup.ItemsSource = _conexionesSql;
                break;
            case 1: // Contabo - Base de datos
            case 2: // Contabo - Archivos
                CmbConexionBackup.ItemsSource = _conexionesContabo;
                break;
        }
    }

    private void ChkEncadenar_Changed(object sender, RoutedEventArgs e)
    {
        ActualizarVisibilidadConexionArchivos();
    }

    private void ActualizarVisibilidadConexionArchivos()
    {
        var visible = ChkEncadenar.IsChecked == true;
        LblConexionArchivos.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        CmbConexionArchivos.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNombre.Text))
        {
            MessageBox.Show("El nombre es obligatorio.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (CmbTipo.SelectedIndex < 0)
        {
            MessageBox.Show("Selecciona el tipo de backup.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string conexionOrigenId;
        TipoTareaOrigen tipo;

        switch (CmbTipo.SelectedIndex)
        {
            case 0:
                tipo = TipoTareaOrigen.BackupSqlWindows;
                if (CmbConexionBackup.SelectedItem is not ConexionBackupSql conexionSql)
                {
                    MessageBox.Show("Selecciona qué servidor SQL respaldar.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                conexionOrigenId = conexionSql.Id!;
                break;

            case 1:
                tipo = TipoTareaOrigen.ContaboBaseDatos;
                if (CmbConexionBackup.SelectedItem is not ConexionBackupContabo conexionContaboBd)
                {
                    MessageBox.Show("Selecciona qué servidor Contabo respaldar.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrWhiteSpace(conexionContaboBd.NombreBaseDatos) || conexionContaboBd.Tablas.Count == 0)
                {
                    MessageBox.Show("Ese servidor Contabo no tiene configurado el backup de base de datos. Edítalo primero en la sección Backups Contabo.",
                        "Sin configurar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                conexionOrigenId = conexionContaboBd.Id!;
                break;

            case 2:
                tipo = TipoTareaOrigen.ContaboArchivos;
                if (CmbConexionBackup.SelectedItem is not ConexionBackupContabo conexionContaboArch)
                {
                    MessageBox.Show("Selecciona qué servidor Contabo respaldar.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (conexionContaboArch.CarpetasOrigenArchivos.Count == 0)
                {
                    MessageBox.Show("Ese servidor Contabo no tiene configurado el backup de archivos. Edítalo primero en la sección Backups Contabo.",
                        "Sin configurar", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                conexionOrigenId = conexionContaboArch.Id!;
                break;

            default:
                return;
        }

        bool encadenar = ChkEncadenar.IsChecked == true;
        ConexionBackup? conexionArchivos = null;

        if (encadenar)
        {
            if (CmbConexionArchivos.SelectedItem is not ConexionBackup seleccionada)
            {
                MessageBox.Show("Selecciona qué conexión de 'Pasar archivos' usar, o desmarca la opción de bajar automáticamente.",
                    "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            conexionArchivos = seleccionada;
        }

        if (CmbHora.SelectedItem == null || CmbMinuto.SelectedItem == null)
        {
            MessageBox.Show("Selecciona la hora.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var diasSeleccionados = new List<DayOfWeek>();
        if (ChkLun.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Monday);
        if (ChkMar.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Tuesday);
        if (ChkMie.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Wednesday);
        if (ChkJue.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Thursday);
        if (ChkVie.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Friday);
        if (ChkSab.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Saturday);
        if (ChkDom.IsChecked == true) diasSeleccionados.Add(DayOfWeek.Sunday);

        if (diasSeleccionados.Count == 0)
        {
            MessageBox.Show("Selecciona al menos un día de la semana.", "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Tarea.Nombre = TxtNombre.Text.Trim();
        Tarea.Tipo = tipo;
        Tarea.ConexionOrigenId = conexionOrigenId;
        Tarea.EncadenarConSincronizacion = encadenar;
        Tarea.ConexionArchivosId = conexionArchivos?.Id;
        Tarea.Hora = int.Parse((string)CmbHora.SelectedItem);
        Tarea.Minuto = int.Parse((string)CmbMinuto.SelectedItem);
        Tarea.Dias = diasSeleccionados;
        Tarea.Activada = ChkActivada.IsChecked == true;

        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}