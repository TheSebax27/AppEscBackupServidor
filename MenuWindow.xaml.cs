using System.Windows;
using System.Windows.Input;

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