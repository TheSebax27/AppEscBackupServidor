using BackupSyncApp.Services;
using System.Text;
using System.Windows;
using BackupSyncApp.Models;
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

    public partial class MainWindow : Window
    {
        private readonly ConexionStorageService _storage = new();
        private List<ConexionBackup> _conexiones = new ();
        public MainWindow()
        {
            InitializeComponent();
            _conexiones = _storage.Cargar();
            RefrescarLista();

        }

        private void RefrescarLista()
        {
            ListaConexiones.ItemsSource = null;
            ListaConexiones.ItemsSource = _conexiones;
        }

        private void BtnAgregar_Click(Object sender, RoutedEventArgs e)
        {

            var ventana = new ConexionWindow { Owner = this};

            bool? resultado = ventana.ShowDialog();

            if (resultado == true)
            {
                _conexiones.Add(ventana.Conexion);
                _storage.Guardar(_conexiones);
                RefrescarLista();
            }

        }

        private void BtnEditar_Click(Object sender, RoutedEventArgs e)
        {
            if (ListaConexiones.SelectedItem is not ConexionBackup Seleccionada)
            {
               MessageBox.Show("Seleccione una conexión para editar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ventana = new ConexionWindow(Seleccionada) { Owner = this };
            bool? resultado = ventana.ShowDialog();

            if (resultado == true)
            {

                _storage.Guardar(_conexiones);
                RefrescarLista();
            }

            else
            {
                MessageBox.Show("Seleccione una conexión para editar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnEliminar_Click(Object sender, RoutedEventArgs e)
        {
            if (ListaConexiones.SelectedItem is not ConexionBackup Seleccionada)
            {
                MessageBox.Show("Seleccione una conexión para eliminar.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var confirmacion = MessageBox.Show($"¿Está seguro de que desea eliminar la conexión '{Seleccionada.Nombre}'?", "Confirmar eliminación", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirmacion == MessageBoxResult.Yes)
            {
                _conexiones.Remove(Seleccionada);
                _storage.Guardar(_conexiones);
                RefrescarLista();
            }
        }
    }
}