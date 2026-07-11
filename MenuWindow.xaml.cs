using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
}