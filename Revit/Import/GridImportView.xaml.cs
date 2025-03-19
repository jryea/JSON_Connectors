using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;

namespace Revit.Import
{
    /// <summary>
    /// Interaction logic for GridImporterView.xaml
    /// </summary>
    public partial class GridImportView : Window
    {
        private readonly GridImportViewModel _viewModel;

        public GridImportView(UIDocument uiDoc)
        {
            InitializeComponent();

            // Create and set the ViewModel
            _viewModel = new GridImportViewModel(uiDoc);
            DataContext = _viewModel;

            // Handle window events
            Loaded += GridImporterView_Loaded;
            Closing += GridImporterView_Closing;
        }

        private void GridImporterView_Loaded(object sender, RoutedEventArgs e)
        {
            // Make dialog modal to Revit window
            var helper = new WindowInteropHelper(this);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        }

        private void GridImporterView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Show import result message if import was initiated
            string message = _viewModel.GetResultMessage();
            if (!string.IsNullOrEmpty(message))
            {
                TaskDialog.Show("Import Result", message);
            }
        }
    }
}