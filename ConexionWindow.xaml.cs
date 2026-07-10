using BackupSyncApp.Models;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BackupSyncApp
{
    
    public partial class ConexionWindow : Window
    {

        public ConexionBackup Conexion { get; private set; }

        public ConexionWindow (ConexionBackup? conexionExistente = null)
        {
            InitializeComponent ();

            Conexion = conexionExistente ?? new ConexionBackup();

            CargarDatosEnFormulario();
        }

        private void CargarDatosEnFormulario()
        {
            
                TxtNombre.Text = Conexion.Nombre;
                TxtServidor.Text = Conexion.Servidor;
                TxtUsuario.Text = Conexion.Usuario;
                TxtLlave.Text = Conexion.RutaLlavePrivada;
                TxtOrigen.Text = Conexion.RutaOrigen;
                TxtDestino.Text = Conexion.RutaDestino;
                ChkMoverArchivos.IsChecked = Conexion.MoverArchivos;
            
        }

        private void GuardarDatosDesdeFormulario()
        {
            Conexion.Nombre = TxtNombre.Text;
            Conexion.Servidor = TxtServidor.Text;
            Conexion.Usuario = TxtUsuario.Text;
            Conexion.RutaLlavePrivada = TxtLlave.Text;
            Conexion.RutaOrigen = TxtOrigen.Text;
            Conexion.RutaDestino = TxtDestino.Text;
            Conexion.MoverArchivos = ChkMoverArchivos.IsChecked ?? false;
        }

        private void BtnBuscarLlave_Click(object sender, RoutedEventArgs e)
        {
           
            var dialogo = new OpenFileDialog
            {
                Title = "Seleccionar tu llave privada SSH",
                Filter = "Archivos de llave (*.pem;*.ppk)|*.pem;*.ppk|Todos los archivos (*.*)|*.*"
            };

            if (dialogo.ShowDialog() == true)
            {

TxtLlave.Text = dialogo.FileName;

            }

        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(TxtNombre.Text) ||
                string.IsNullOrWhiteSpace(TxtServidor.Text) ||
                string.IsNullOrWhiteSpace(TxtUsuario.Text) ||
                string.IsNullOrWhiteSpace(TxtLlave.Text) ||
                string.IsNullOrWhiteSpace(TxtOrigen.Text) ||
                string.IsNullOrWhiteSpace(TxtDestino.Text))
            {
                
                MessageBox.Show("Por favor, completa todos los campos antes de guardar.", "Campos incompletos", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Conexion.Nombre = TxtNombre.Text.Trim();
            Conexion.Servidor = TxtServidor.Text.Trim();
            Conexion.Usuario = TxtUsuario.Text.Trim();
            Conexion.RutaLlavePrivada = TxtLlave.Text.Trim();
            Conexion.RutaOrigen = TxtOrigen.Text.Trim();
            Conexion.RutaDestino = TxtDestino.Text.Trim();
            Conexion.MoverArchivos = ChkMoverArchivos.IsChecked ?? false;

            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

    }
}
