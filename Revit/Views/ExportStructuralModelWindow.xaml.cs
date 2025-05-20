using System.Windows;
using Autodesk.Revit.UI;
using Revit.ViewModels;

namespace Revit.Views
{
    public partial class ExportStructuralModelWindow : Window
    {
        private readonly ExportStructuralModelViewModel _viewModel;

        public ExportStructuralModelWindow(UIApplication uiApp)
        {
            InitializeComponent();

            // Create the view model with the Revit application
            _viewModel = new ExportStructuralModelViewModel(uiApp);
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