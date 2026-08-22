using System.Windows;

namespace AutoHitCounter.Views.Windows;

public partial class MultirunSettingsWindow : Window
{
    public MultirunSettingsWindow()
    {
        InitializeComponent();
        if (Application.Current.MainWindow != null)
        {
            Application.Current.MainWindow.Closing += (sender, args) => { Close(); };
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
