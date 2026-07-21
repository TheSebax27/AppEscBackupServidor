using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using BackupSyncApp.Services;

namespace BackupSyncApp;

public partial class MenuWindow : Window
{
    public MenuWindow()
    {
        InitializeComponent();
    }

    private void BtnPasarArchivos_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new MainWindow();
        ventana.Show();
        this.Close();
    }

    private void BtnSacarBackups_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new BackupWindow();
        ventana.Show();
        this.Close();
    }

    private void BtnRestaurar_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new RestauracionWindow();
        ventana.Show();
        this.Close();
    }

    private void BtnProgramacion_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new ProgramacionWindow();
        ventana.Show();
        this.Close();
    }

    private void BtnContabo_Click(object sender, RoutedEventArgs e)
    {
        var ventana = new ContaboWindow();
        ventana.Show();
        this.Close();
    }

    // ==== Exportar / Importar configuración ====

    private void BtnExportarConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new SaveFileDialog
        {
            Title = "Exportar configuración",
            FileName = $"BackupSyncApp_config_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            Filter = "Archivo de configuración (*.json)|*.json"
        };

        if (dialogo.ShowDialog() != true)
        {
            return;
        }

        try
        {
            new ConfiguracionExportService().Exportar(dialogo.FileName);
            MessageBox.Show(
                $"Configuración exportada correctamente a:\n{dialogo.FileName}\n\n" +
                "Copia ese archivo al otro equipo y usa 'Importar configuración' ahí.",
                "Exportación exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo exportar la configuración:\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnImportarConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Importar configuración",
            Filter = "Archivo de configuración (*.json)|*.json"
        };

        if (dialogo.ShowDialog() != true)
        {
            return;
        }

        var respuesta = MessageBox.Show(
            "¿Cómo quieres importar este archivo?\n\n" +
            "SÍ = Reemplazar todo (lo del archivo importado manda, se pierde lo que tenías)\n" +
            "NO = Combinar (agrega lo nuevo, conserva lo que ya tenías en este equipo)\n" +
            "CANCELAR = no importar nada",
            "Importar configuración", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

        if (respuesta == MessageBoxResult.Cancel)
        {
            return;
        }

        bool reemplazarTodo = respuesta == MessageBoxResult.Yes;

        try
        {
            var resultado = new ConfiguracionExportService().Importar(dialogo.FileName, reemplazarTodo);

            if (reemplazarTodo)
            {
                MessageBox.Show(
                    "Configuración reemplazada correctamente.\n\n" +
                    "Cierra y vuelve a abrir las secciones de conexiones para ver los cambios.",
                    "Importación exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Se agregaron {resultado.Total} conexión(es)/tarea(s) nuevas:\n\n" +
                    $"Pasar archivos: {resultado.NuevasArchivos}\n" +
                    $"Sacar backups: {resultado.NuevasBackupSql}\n" +
                    $"Restaurar backups: {resultado.NuevasRestauracion}\n" +
                    $"Backups Contabo: {resultado.NuevasContabo}\n" +
                    $"Tareas programadas: {resultado.NuevasTareas}\n\n" +
                    "Las que ya tenías en este equipo no se tocaron.",
                    "Importación exitosa", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo importar la configuración:\n\n{ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ==== Lógica para la ventana sin bordes (WindowStyle="None") ====

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
        this.Close();
    }
}