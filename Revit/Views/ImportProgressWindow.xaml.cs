using System.Windows;
using Revit.ViewModels;

namespace Revit.Views
{
    public partial class ImportProgressWindow : Window
    {
        public ImportProgressViewModel ViewModel { get; private set; }

        public ImportProgressWindow()
        {
            InitializeComponent();
            ViewModel = new ImportProgressViewModel();
            DataContext = ViewModel;

            // Handle window closing
            Closing += OnClosing;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Allow closing only if import is complete or user confirms cancellation
            if (ViewModel.OverallProgress < 100)
            {
                var result = MessageBox.Show(
                    "Import is still in progress. Are you sure you want to cancel?",
                    "Cancel Import",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Clean up resources
            ViewModel?.Dispose();
        }

        // Method to safely close window from background thread
        public void SafeClose()
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    try
                    {
                        this.Close();
                    }
                    catch
                    {
                        // Ignore closing errors
                    }
                }));
            }
        }
    }
}