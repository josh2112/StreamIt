using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Windows;

namespace Com.Josh2112.StreamIt.UI
{
    public partial class TextModel : ObservableValidator
    {
        private string _text = "";

        [Required]
        [MinLength( 1 )]
        public string Text
        {
            get => _text;
            set => SetProperty( ref _text, value, true );
        }

        public bool IsValid()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        public void ClearErrors() => base.ClearErrors();
    }

    public partial class TextInputDialog : System.Windows.Controls.UserControl, IDialogWithResponse<string?>
    {
        public TextModel Model { get; }

        public TextInputDialog( string? text )
        {
            Model = new() { Text = text ?? "" };
            InitializeComponent();
        }

        private void TextBox_TextChanged( object sender, System.Windows.Controls.TextChangedEventArgs e ) =>
            Model.ClearErrors();

        public void CancelButton_Click( object sender, RoutedEventArgs e ) =>
            DialogHost.CloseDialogCommand.Execute( null, this );

        public void OKButton_Click( object sender, RoutedEventArgs e )
        {
            if( Model.IsValid())
                DialogHost.CloseDialogCommand.Execute( Model.Text, this );
        }
    }
}
