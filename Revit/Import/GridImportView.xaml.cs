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

            // Set owner before showing the dialog
            var helper = new WindowInteropHelper(this);
            helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            // Remove Loaded event if it's defined in XAML
            //this.Loaded -= GridImporterView_Loaded;

            // Handle closing event
            Closing += GridImporterView_Closing;
        }

        //private void GridImporterView_Loaded(object sender, RoutedEventArgs e)
        //{
        //    // Make dialog modal to Revit window
        //    var helper = new WindowInteropHelper(this);
        //    helper.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
        //}

        private void GridImporterView_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Show import result message if import was initiated
            string message = _viewModel.GetResultMessage();
            if (!string.IsNullOrEmpty(message))
            {
                TaskDialog.Show("Import Result", message);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            // The import is handled by the command
            // Just set DialogResult and close after import is done
            this.DialogResult = true;
            this.Close();
        }
    }
}