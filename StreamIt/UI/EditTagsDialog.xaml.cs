using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace Com.Josh2112.StreamIt.UI
{
    [ObservableObject]
    public partial class EditTagsDialog : System.Windows.Controls.UserControl, IHasDialogResult<List<string>?>
    {
        private ObservableCollection<string> allTags;

        public ListCollectionView FilteredTags { get; }

        public ObservableCollection<string> MediaTags { get; } = new();

        public DialogResult<List<string>?> Result { get; } = new();

        [ObservableProperty]
        private string _selectedTag = string.Empty;

        public EditTagsDialog( IList<string> allTags, IList<string> mediaTags )
        {
            this.allTags = new( allTags );
            FilteredTags = new( this.allTags );
            FilteredTags.Filter = ( obj ) => obj is string t && (string.IsNullOrWhiteSpace( SelectedTag ) || t.Contains( SelectedTag.ToLower()));

            MediaTags = new( mediaTags );

            InitializeComponent();
        }

        public void CancelButton_Click( object sender, RoutedEventArgs e ) =>
            Result.Set( null );

        public void OKButton_Click( object sender, RoutedEventArgs e ) =>
            Result.Set( MediaTags.ToList() );

        private void TagChip_Delete_Click( object sender, RoutedEventArgs e )
        {
            if( sender is FrameworkElement { DataContext: string tag } )
                MediaTags.Remove( tag );
        }

        private void ComboBox_PreviewKeyDown( object sender, System.Windows.Input.KeyEventArgs e )
        {
            if( e.Key == System.Windows.Input.Key.Enter )
            {
                e.Handled = true;

                if( !string.IsNullOrWhiteSpace( SelectedTag ) &&
                    SelectedTag.ToLower() is string selectedTagLowercase && !MediaTags.Contains( selectedTagLowercase ) )
                {
                    allTags.Add( selectedTagLowercase );
                    MediaTags.Add( selectedTagLowercase );

                    SelectedTag = string.Empty;
                }
            }
        }
    }
}
