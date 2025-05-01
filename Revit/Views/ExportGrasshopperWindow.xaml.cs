using System.Windows;
using Autodesk.Revit.UI;
using Revit.ViewModels;

namespace Revit.Views
{
    /// <summary>
    /// Interaction logic for ExportGrasshopperWindow.xaml
    /// </summary>
    public partial class ExportGrasshopperWindow : Window
    {
        private readonly ExportGrasshopperViewModel _viewModel;

        public ExportGrasshopperWindow(UIApplication uiApp)
        {
            InitializeComponent();

            // Create the view model with the Revit application
            _viewModel = new ExportGrasshopperViewModel(uiApp);
            DataContext = _viewModel;

            // Set up event handling for the view model
            _viewModel.RequestClose += () => DialogResult = true;
            _viewModel.RequestMinimize += () => this.Hide();
            _viewModel.RequestRestore += () =>
            {
                this.Show();
                this.Activate();
            };
        }
    }
}