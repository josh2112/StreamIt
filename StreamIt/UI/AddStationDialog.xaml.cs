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
    public partial class AddStationModel : ObservableValidator
    {
        private string _urlOrFilePath = "";

        [Required]
        [CustomValidation( typeof( AddStationModel ), nameof( ValidateUrlOrFilePath ) )]
        public string UrlOrFilePath
        {
            get => _urlOrFilePath;
            set => SetProperty( ref _urlOrFilePath, value, true );
        }

        public static ValidationResult ValidateUrlOrFilePath( string value, ValidationContext _ )
        {
            if( Uri.TryCreate( value, UriKind.Absolute, out Uri? uri ) )
            {
                if( uri.IsFile && File.Exists( uri.LocalPath ) && Path.GetExtension( uri.LocalPath ).ToLower() == ".pls" &&
                    ParseFirstPlaylistEntry( uri ).Item1 != null )
                    return ValidationResult.Success!;

                if( uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp )
                    return ValidationResult.Success!;
            }
            return new( "Must be a URL or playlist file" );
        }

        public static (string?, string?) ParseFirstPlaylistEntry( Uri uri )
        {
            var lines = File.ReadAllLines( uri.LocalPath );
            var title = lines.FirstOrDefault( l => l.StartsWith( "Title1" ) )?.Split( "=" )?.ElementAt( 1 ) ?? null;
            var url = lines.FirstOrDefault( l => l.StartsWith( "File1" ) )?.Split( "=" )?.ElementAt( 1 ) ?? null;
            return (url, title);
        }

        public bool IsValid()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        public void ClearErrors() => base.ClearErrors();
    }

    public partial class AddStationDialog : System.Windows.Controls.UserControl, IDialogWithResponse<AddStationModel?>
    {
        public AddStationModel Model { get; } = new AddStationModel();

        public AddStationDialog() => InitializeComponent();

        private void SelectPlaylistFileButton_Click( object sender, RoutedEventArgs e )
        {
            var dlg = new OpenFileDialog {
                Filter = "Playlist files (*.pls)|*.pls",
                InitialDirectory = Environment.GetFolderPath( Environment.SpecialFolder.MyMusic ),
            };

            if( true == dlg.ShowDialog() )
                Model.UrlOrFilePath = dlg.FileName;
        }

        private void AddStationFormTextBox_TextChanged( object sender, System.Windows.Controls.TextChangedEventArgs e ) =>
            Model.ClearErrors();

        public void CancelButton_Click( object sender, RoutedEventArgs e ) =>
            DialogHost.CloseDialogCommand.Execute( null, this );

        public void AddButton_Click( object sender, RoutedEventArgs e )
        {
            if( Model.IsValid())
                DialogHost.CloseDialogCommand.Execute( Model, this );
        }
    }
}
