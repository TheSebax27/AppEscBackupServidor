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
using System.Windows.Shapes;

namespace BackupSyncApp;

public partial class ConfirmarRestauracionWindow : Window
{
    public string? ZipSeleccionado { get; private set; }

    private const string PalabraConfirmacion = "RESTAURAR";

    public ConfirmarRestauracionWindow(List<string> zipsDisponibles)
    {
        InitializeComponent();
        ListaZips.ItemsSource = zipsDisponibles;
    }

    private void ListaZips_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ActualizarBotonConfirmar();
    }

    private void TxtConfirmacion_TextChanged(object sender, TextChangedEventArgs e)
    {
        ActualizarBotonConfirmar();
    }

    private void ActualizarBotonConfirmar()
    {
        BtnConfirmar.IsEnabled =
            ListaZips.SelectedItem != null &&
            TxtConfirmacion.Text == PalabraConfirmacion;
    }

    private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
    {
        ZipSeleccionado = ListaZips.SelectedItem as string;
        DialogResult = true;
        Close();
    }

    private void BtnCancelar_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    // ===================== Ventana custom (sin borde nativo) =====================

    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            this.DragMove();
    }

    private void BtnCerrarVentana_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}