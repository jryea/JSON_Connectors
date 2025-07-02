using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPF.Shared.Controls
{
    public partial class IMEGWindowLayout : UserControl
    {
        public static readonly DependencyProperty WindowTitleProperty =
            DependencyProperty.Register(nameof(WindowTitle), typeof(string),
            typeof(IMEGWindowLayout), new PropertyMetadata("IMEG Tool"));

        public static readonly DependencyProperty MainContentProperty =
            DependencyProperty.Register(nameof(MainContent), typeof(object),
            typeof(IMEGWindowLayout));

        public static readonly DependencyProperty PrimaryButtonTextProperty =
            DependencyProperty.Register(nameof(PrimaryButtonText), typeof(string),
            typeof(IMEGWindowLayout), new PropertyMetadata("Apply"));

        public static readonly DependencyProperty PrimaryCommandProperty =
            DependencyProperty.Register(nameof(PrimaryCommand), typeof(ICommand),
            typeof(IMEGWindowLayout));

        public static readonly DependencyProperty CancelCommandProperty =
            DependencyProperty.Register(nameof(CancelCommand), typeof(ICommand),
            typeof(IMEGWindowLayout));

        public string WindowTitle
        {
            get => (string)GetValue(WindowTitleProperty);
            set => SetValue(WindowTitleProperty, value);
        }

        public object MainContent
        {
            get => GetValue(MainContentProperty);
            set => SetValue(MainContentProperty, value);
        }

        public string PrimaryButtonText
        {
            get => (string)GetValue(PrimaryButtonTextProperty);
            set => SetValue(PrimaryButtonTextProperty, value);
        }

        public ICommand PrimaryCommand
        {
            get => (ICommand)GetValue(PrimaryCommandProperty);
            set => SetValue(PrimaryCommandProperty, value);
        }

        public ICommand CancelCommand
        {
            get => (ICommand)GetValue(CancelCommandProperty);
            set => SetValue(CancelCommandProperty, value);
        }

        public IMEGWindowLayout()
        {
            InitializeComponent();
        }
    }
}