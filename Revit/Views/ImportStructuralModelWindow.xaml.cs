using System.Windows;
using Autodesk.Revit.UI;
using Revit.ViewModels;

namespace Revit.Views
{
    public partial class ImportStructuralModelWindow : Window
    {
        private readonly ImportStructuralModelViewModel _viewModel;

        public ImportStructuralModelViewModel ViewModel => _viewModel;

        public ImportStructuralModelWindow(UIApplication uiApp)
        {
            InitializeComponent();

            // Create the view model with the Revit application
            _viewModel = new ImportStructuralModelViewModel(uiApp);
            DataContext = _viewModel;


            // Set up event handling for the view model
            _viewModel.RequestClose += () =>
            {
                if (this.IsLoaded && this.IsVisible)
                {
                    this.DialogResult = _viewModel.DialogResult ?? false;
                    this.Close();
                }
            };
        }
    }
}