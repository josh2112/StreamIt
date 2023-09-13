using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using Com.Josh2112.StreamIt.UI;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using System.Windows;


/// TODO:
/// Automatically find station icon 
/// - https://www.radio.net/search?q= ... look for <script id="__NEXT_DATA__"... 


namespace Com.Josh2112.StreamIt
{
    public partial class MainWindow : Window
    {
        public ViewModel Model { get; } = new();
        
        public MainWindow()
        {
            Model.NowPlayingChanged += ( s, name ) => ChangeTitle( name );

            Loaded += async ( s, e ) => await Model.InitializeAsync();

            InitializeComponent();
            
            ChangeTitle( null );
        }

        private void ChangeTitle( string? title ) =>
            Title = $"{title ?? ""}{(title is not null ? " - " : "")}{Utils.AssemblyInfo.ProductName}";

        private void MediaEntry_MouseDoubleClick( object sender, System.Windows.Input.MouseButtonEventArgs e ) =>
            Model.Play( ((sender as FrameworkElement)!.DataContext as MediaEntry)! );


        private async void ShowAddStationDialogButton_Click( object sender, RoutedEventArgs e )
        {
            if( await this.ShowDialogForResultAsync( new AddStationDialog() ) is AddStationModel station )
            {
                var entry = Model.AddStation( station );
                Model.MediaEntries!.MoveCurrentTo( entry );
                Model.Play( entry );
            }
        }

        [RelayCommand]
        private async Task RenameMediaAsync( MediaEntry entry )
        {
            if( await this.ShowDialogForResultAsync( new TextInputDialog( entry.Name ) ) is string name )
            {
                entry.Name = name;
                Model.Settings.Save();
            }
        }

        private void CurrentSongInfo_MouseDown( object sender, System.Windows.Input.MouseButtonEventArgs e ) =>
            Model.MediaEntries!.MoveCurrentTo( Model.LoadedMedia );
    }
}