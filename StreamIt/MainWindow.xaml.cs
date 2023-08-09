using Com.Josh2112.StreamIt.UI;
using GongSolutions.Wpf.DragDrop.Utilities;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;


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
            Model.RenameGroupInvoked += ( s, media ) => BeginRenameGroup( media );

            Loaded += async ( s, e ) => await Model.InitializeAsync();

            InitializeComponent();
            
            ChangeTitle( null );
        }

        private void ChangeTitle( string? title ) =>
            Title = $"{title ?? ""}{(title is not null ? " - " : "")}{Utils.AssemblyInfo.ProductName}";

        private void ShowSongHistoryButton_Click( object sender, RoutedEventArgs e )
        {
            if( sender is FrameworkElement { DataContext: MediaEntry media } )
            {
                songHistoryPopup.DataContext = media;
                songHistoryPopup.PlacementTarget = sender as UIElement;
                songHistoryPopup.IsOpen = true;
            }
        }

        private void MediaEntry_MouseDoubleClick( object sender, System.Windows.Input.MouseButtonEventArgs e ) =>
            Model.Play( ((sender as FrameworkElement)!.DataContext as MediaEntry)! );

        private void AddStationButton_Click( object sender, RoutedEventArgs e )
        {
            addStationPopup.PlacementTarget = sender as UIElement;
            addStationPopup.IsOpen = true;
        }

        private void SelectPlaylistFileButton_Click( object sender, RoutedEventArgs e )
        {
            var dlg = new OpenFileDialog {
                Filter = "Playlist files (*.pls)|*.pls",
                InitialDirectory = Environment.GetFolderPath( Environment.SpecialFolder.MyMusic ),
            };

            if( true == dlg.ShowDialog())
                Model.AddStationForm.UrlOrFilePath = dlg.FileName;
        }

        private void AddStationPopupCloseButton_Click( object sender, RoutedEventArgs e ) =>
            addStationPopup.IsOpen = false;

        private void AddStationPopupOKButton_Click( object sender, RoutedEventArgs e )
        {
            if( Model.AddStation())
                addStationPopup.IsOpen = false;
        }

        private void AddStationFormTextBox_TextChanged( object sender, System.Windows.Controls.TextChangedEventArgs e ) =>
            Model.AddStationForm.ClearErrors();

        private void BeginRenameGroup( MediaEntry media )
        {
            Dispatcher.BeginInvoke( () => {
                // Given this list item, we need to scroll it into view,
                // find the group header corresponding to its parent group,
                // then find its name text block. This will have the renaming
                // mechanism attached.
                listView.ScrollIntoView( media );
                var container = listView.ItemContainerGenerator.ContainerFromItem( media );
                var group = container.GetVisualAncestor<System.Windows.Controls.GroupItem>();
                BeginRename_Impl( group.GetVisualDescendent<System.Windows.Controls.TextBlock>( "groupNameText" ) );
            }, DispatcherPriority.ApplicationIdle );
        }

        private void renameGroupMenuItem_Click( object sender, RoutedEventArgs e )
        {
            PopupBox.ClosePopupCommand.Execute( null, null );

            if( (sender as FrameworkElement)!.Tag is System.Windows.Controls.TextBlock textBlock )
                BeginRename_Impl( textBlock );
        }

        private void renameMediaMenuItem_Click( object sender, RoutedEventArgs e )
        {
            PopupBox.ClosePopupCommand.Execute( null, null );

            if( (sender as FrameworkElement)!.Tag is System.Windows.Controls.TextBlock textBlock )
                BeginRename_Impl( textBlock );
        }

        private static void BeginRename_Impl( System.Windows.Controls.TextBlock renameTextBlock )
        {
            if( EditTextInPlaceBehavior.For( renameTextBlock ) is EditTextInPlaceBehavior editInPlace )
                editInPlace.IsEditing = true;
        }

        private void EditGroupName_TextChanging( object sender, ValueChangingEventArgs<string> e ) =>
            e.Cancel = e.NewValue.Length < 1 || Model.GroupNames.Contains( e.NewValue );

        private void EditGroupName_TextChanged( object sender, ValueChangedEventArgs<string> e ) =>
            Model.RenameGroup( e.OldValue, e.NewValue );

        private void EditMediaName_TextChanging( object sender, ValueChangingEventArgs<string> e ) =>
            e.Cancel = e.NewValue.Length < 1;

        private void EditMediaName_TextChanged( object sender, ValueChangedEventArgs<string> e ) =>
            Model.RenameMedia( e.OldValue, e.NewValue );
    }
}