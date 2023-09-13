using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
using System.ComponentModel.DataAnnotations;
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
    }

    public partial class TextInputDialog : System.Windows.Controls.UserControl, IHasDialogResult<string?>
    {
        public TextModel Model { get; }

        public DialogResult<string?> Result { get; } = new();

        public TextInputDialog( string? text )
        {
            Model = new() { Text = text ?? "" };
            InitializeComponent();
        }

        public void CancelButton_Click( object sender, RoutedEventArgs e ) =>
            Result.Set( null );

        public void OKButton_Click( object sender, RoutedEventArgs e )
        {
            if( Model.IsValid() )
                Result.Set( Model.Text );
        }
    }
}
