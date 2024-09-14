using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using System;
using System.Reflection;
using System.Windows;

namespace Com.Josh2112.StreamIt.UI
{
    public partial class AboutDialog : System.Windows.Controls.UserControl, IHasDialogResult<bool>
    {
        public static Version? Version => Assembly.GetEntryAssembly()?.GetName().Version;

        public DialogResult<bool> Result { get; } = new();

        public AboutDialog() => InitializeComponent();

        public void OKButton_Click( object sender, RoutedEventArgs e ) => Result.Set( true );
    }
}
