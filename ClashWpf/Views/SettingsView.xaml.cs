using System.Windows.Controls;
using ClashWpf.ViewModels;

namespace ClashWpf.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SecretBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm && SecretBox is not null)
            vm.OnSecretChanged(SecretBox.Password);
    }
}
