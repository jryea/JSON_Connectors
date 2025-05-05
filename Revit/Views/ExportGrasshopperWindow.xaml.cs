using System.Windows;
using Autodesk.Revit.UI;
using Revit.ViewModels;

namespace Revit.Views
{
    // Interaction logic for ExportGrasshopperWindow.xaml
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
            _viewModel.RequestClose += () =>
            {
                if (this.IsLoaded && this.IsVisible)
                {
                    // Only set DialogResult if the window is shown as a dialog
                    if (this.Owner != null)
                    {
                        this.DialogResult = false;
                    }
                    this.Close();
                }
            };

            _viewModel.RequestMinimize += () =>
            {
                if (this.IsLoaded && this.IsVisible)
                {
                    this.Hide();
                }
            };

            _viewModel.RequestRestore += () =>
            {
                if (!this.IsVisible)
                {
                    this.Show();
                    this.Activate();
                }
            };
        }
    }
}