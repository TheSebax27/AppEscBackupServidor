using System.Windows;

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
}