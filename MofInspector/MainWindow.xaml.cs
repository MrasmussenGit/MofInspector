using System.Reflection;
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

namespace MofInspector
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ButtonState == MouseButtonState.Pressed)
                    DragMove();
            };

            // Set version text
            var infoVersion = System.Reflection.Assembly
                .GetExecutingAssembly()
                .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            VersionText.Text = $"Version {infoVersion ?? "1.0.0"}";

        }


        private void InspectMof_Click(object sender, RoutedEventArgs e)
        {

            var inspectWindow = new InspectWindow
            {
                Owner = this // Makes the new window open on top of the menu window
            };
            inspectWindow.ShowDialog();
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }


        private void Compare_Click(object sender, RoutedEventArgs e)
        {
            var compareWindow = new CompareWindow();
            compareWindow.Owner = this;
            compareWindow.ShowDialog();
        }


        }
    }