using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;

namespace Com.Josh2112.StreamIt.UI
{
    public partial class TextModel : ObservableValidator
    {
        public string Title { get; init; } = "Text Input";
        public string OKButtonName { get; init; } = "OK";

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

        public TextInputDialog( TextModel model )
        {
            Model = model;
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
