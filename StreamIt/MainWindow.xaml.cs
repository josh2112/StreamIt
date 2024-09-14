using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using Com.Josh2112.Libs.MaterialDesign.DialogPlus.Dialogs;
using Com.Josh2112.StreamIt.UI;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

/// TODO:
/// Automatically find station icon 
/// - https://www.radio.net/search?q= ... look for <script id="__NEXT_DATA__"... 


namespace Com.Josh2112.StreamIt
{
    public partial class MainWindow : Window, IDropTarget
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

        private void MediaEntry_MouseDoubleClick( object sender, MouseButtonEventArgs e )
        {
            if( sender is FrameworkElement { DataContext: MediaEntry entry } && entry.State != MediaStates.Playing )
                Model.Play( entry );
        }

        private async void ShowAddStationDialogButton_Click( object sender, RoutedEventArgs e )
        {
            if( await this.ShowDialogForResultAsync( new AddStationDialog() ) is AddStationModel station )
            {
                if( await Model.AddStationAsync( station ) is MediaEntry entry )
                {
                    Model.MediaEntries!.MoveCurrentTo( entry );
                    Model.Play( entry );
                }
                else
                {
                    await this.ShowDialogForResultAsync( new Dialog( "Couldn't add station",
                        "Ran into a problem adding the station.", ButtonDef.Positive( "OK" ) ) );
                }
            }
        }

        [RelayCommand]
        private async Task RenameMediaAsync( MediaEntry entry )
        {
            if( await this.ShowDialogForResultAsync( new TextInputDialog( new TextModel
            {
                Title = "Rename station",
                OKButtonName = "Rename",
                Text = entry.Name ?? ""
            } ) ) is string name )
            {
                entry.Name = name;
                Model.Settings.Save();
            }
        }

        [RelayCommand]
        private async Task DeleteMediaAsync( MediaEntry entry )
        {
            ButtonDef yesButton = ButtonDef.Negative( "Yes" ), cancelButton = ButtonDef.Neutral( "Cancel", isCancel: true );

            if( await this.ShowDialogForResultAsync( new Dialog( "Delete",
                "Do you really want to delete this station?", cancelButton, yesButton )) == yesButton )
            {
                Model.DeleteMedia( entry );
            }
        }

        [RelayCommand]
        private async Task EditTagsAsync( MediaEntry entry )
        {
            if( await this.ShowDialogForResultAsync( new EditTagsDialog( entry.Tags, entry.Tags ) ) is List<string> tags )
            {
                entry.Tags.Clear();
                foreach( var tag in tags ) entry.Tags.Add( tag );
                Model.Settings.Save();
            }
        }

        [RelayCommand]
        private Task<bool> ShowAboutDialogAsync() => this.ShowDialogForResultAsync( new AboutDialog() );

        private void CurrentSongInfo_MouseDown( object sender, MouseButtonEventArgs e ) =>
            Model.MediaEntries!.MoveCurrentTo( Model.LoadedMedia );

        private static string[] GetValidDroppedPaths( object dropData )
        {
            if( dropData is DataObject data )
            {
                var paths = data.GetData( DataFormats.FileDrop ) as string[];
                
                if( paths is null && data.GetData( DataFormats.Text ) is string str )
                    paths = [str];
                
                if( paths is not null )
                    return paths;
            }
            return [];
        }

        void IDropTarget.DragOver( IDropInfo dropInfo )
        {
            if( !(GetValidDroppedPaths( dropInfo.Data )?.Length == 0 ))
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Copy;
            }
        }

        async void IDropTarget.Drop( IDropInfo dropInfo )
        {
            var errors = new List<string>();
            var newMedia = new List<MediaEntry>();

            foreach( var path in GetValidDroppedPaths( dropInfo.Data ) )
            {
                if( await Model.AddStationAsync( new() { UrlOrFilePath = path } ) is MediaEntry media )
                    newMedia.Add( media );
                else
                    errors.Add( path );
            }

            if( errors.Count != 0 )
            {
                await this.ShowDialogForResultAsync( new Dialog( "Couldn't add station",
                    "Ran into a problem adding the following station(s):\n\n" +
                    string.Join( "\n", errors.Select( e => $" • {e}" ) ),
                    ButtonDef.Positive( "OK" ) ) );
            }

            if( newMedia.FirstOrDefault() is MediaEntry entry )
            {
                Model.MediaEntries!.MoveCurrentTo( entry );
                Model.Play( entry );
            }
        }

        private void Window_PreviewTextInput( object sender, TextCompositionEventArgs e )
        {
            if( e.Text == "/" ) Model.IsSearching = true;
        }

        private void SearchTextBox_PreviewKeyDown( object sender, KeyEventArgs e )
        {
            if( e.Key == Key.Enter || e.Key == Key.Escape )
            {
                if( e.Key == Key.Escape ) Model.SearchText = string.Empty;
                Model.IsSearching = false;
            }
        }

        private void SearchTextBox_IsVisibleChanged( object sender, DependencyPropertyChangedEventArgs e )
        {
            if( sender is TextBox textBox && (bool)e.NewValue )
                Dispatcher.BeginInvoke( () => {
                    textBox.Focus();
                    textBox.SelectAll();
                }, DispatcherPriority.Render );
        }

        private void SearchTextBlock_MouseDown( object sender, MouseButtonEventArgs e ) =>
            Model.IsSearching = true;

        private void SearchTextBox_LostFocus( object sender, RoutedEventArgs e ) =>
            Model.IsSearching = false;

        private void ClearSearchButton_Click( object sender, RoutedEventArgs e ) =>
            Model.SearchText = "";

        private void VolumeSlider_MouseWheel( object sender, MouseWheelEventArgs e )
        {
            if( e.Delta > 0 ) Model.Settings.Volume = Math.Min( Model.Settings.Volume + 5, 100 );
            else Model.Settings.Volume = Math.Max( Model.Settings.Volume - 5, 0 );
        }
    }
}