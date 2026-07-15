using BackupSyncApp.Models;
using BackupSyncApp.Services;
using Microsoft.Win32;
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

public partial class ConexionRestauracionWindow : Window
{
    public ConexionRestauracion Conexion { get; private set; }

    public ConexionRestauracionWindow(ConexionRestauracion? conexionExistente = null)
    {
        InitializeComponent();

        Conexion = conexionExistente ?? new ConexionRestauracion();
        CargarDatosEnFormulario();
    }

    private void CargarDatosEnFormulario()
    {
        TxtNombre.Text = Conexion.Nombre;
        TxtServidor.Text = Conexion.Servidor;
        TxtUsuario.Text = Conexion.Usuario;
        TxtLlave.Text = Conexion.RutaLlavePrivada;
        TxtInstanciaSql.Text = Conexion.InstanciaSql;
        TxtCarpetaZips.Text = Conexion.CarpetaZips;
        TxtRutaDatosSql.Text = Conexion.RutaDatosSql;
    }

    private void BtnBuscarLlave_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Selecciona tu llave privada SSH",
            Filter = "Todos los archivos|*.*"
        };

        if (dialogo.ShowDialog() == true)
        {
            TxtLlave.Text = dialogo.FileName;
        }
    }

    private async void BtnDetectar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtServidor.Text) ||
            string.IsNullOrWhiteSpace(TxtUsuario.Text) ||
            string.IsNullOrWhiteSpace(TxtLlave.Text) ||
            string.IsNullOrWhiteSpace(TxtInstanciaSql.Text))
        {
            MessageBox.Show(
                "Para detectar automáticamente, primero llena Servidor, Usuario, Llave e Instancia SQL.",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var conexionTemporal = new ConexionRestauracion
        {
            Servidor = TxtServidor.Text.Trim(),
            Usuario = TxtUsuario.Text.Trim(),
            RutaLlavePrivada = TxtLlave.Text.Trim(),
            InstanciaSql = TxtInstanciaSql.Text.Trim()
        };

        BtnDetectar.IsEnabled = false;
        TxtEstadoDeteccion.Foreground = System.Windows.Media.Brushes.Gray;
        TxtEstadoDeteccion.Text = "Conectando y consultando...";

        try
        {
            var servicio = new RestauracionService();
            var ruta = await Task.Run(() => servicio.DetectarRutaDatosSql(conexionTemporal));

            TxtRutaDatosSql.Text = ruta;
            TxtEstadoDeteccion.Foreground = System.Windows.Media.Brushes.SeaGreen;
            TxtEstadoDeteccion.Text = "Detectado correctamente. Puedes editarlo si lo necesitas.";
        }
        catch (Exception ex)
        {
            TxtEstadoDeteccion.Foreground = System.Windows.Media.Brushes.OrangeRed;
            TxtEstadoDeteccion.Text = $"No se pudo detectar: {ex.Message}";
        }
        finally
        {
            BtnDetectar.IsEnabled = true;
        }
    }

    private void BtnGuardar_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtNombre.Text) ||
            string.IsNullOrWhiteSpace(TxtServidor.Text) ||
            string.IsNullOrWhiteSpace(TxtUsuario.Text) ||
            string.IsNullOrWhiteSpace(TxtLlave.Text) ||
            string.IsNullOrWhiteSpace(TxtInstanciaSql.Text) ||
            string.IsNullOrWhiteSpace(TxtCarpetaZips.Text) ||
            string.IsNullOrWhiteSpace(TxtRutaDatosSql.Text))
        {
            MessageBox.Show("Todos los campos son obligatorios (usa 'Detectar automáticamente' o escribe la ruta de datos a mano).",
                "Datos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Conexion.Nombre = TxtNombre.Text.Trim();
        Conexion.Servidor = TxtServidor.Text.Trim();
        Conexion.Usuario = TxtUsuario.Text.Trim();
        Conexion.RutaLlavePrivada = TxtLlave.Text.Trim();
        Conexion.InstanciaSql = TxtInstanciaSql.Text.Trim();
        Conexion.CarpetaZips = TxtCarpetaZips.Text.Trim();
        Conexion.RutaDatosSql = TxtRutaDatosSql.Text.Trim();

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