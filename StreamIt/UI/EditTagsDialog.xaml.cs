using Com.Josh2112.Libs.MaterialDesign.DialogPlus;
using CommunityToolkit.Mvvm.ComponentModel;
using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Com.Josh2112.StreamIt.UI
{
    public partial class EditTagsDialog : UserControl, IHasDialogResult<bool>
    {
        public partial class TagNameModel : ObservableValidator
        {
            private string _name = "";

            [Required]
            [MinLength(3)]
            public string Name
            {
                get => _name;
                set => SetProperty( ref _name, value, true );
            }

            public bool IsValid()
            {
                ValidateAllProperties();
                return !HasErrors;
            }
        }

        public class TagDragDropHandler : IDropTarget
        {
            private ObservableCollection<MediaTag> tags;

            public TagDragDropHandler( ObservableCollection<MediaTag> tags ) { this.tags = tags; }

            void IDropTarget.DragOver( IDropInfo dropInfo )
            {
                if( dropInfo.Data is MediaTag tag && !tags.Contains( tag ) )
                {
                    dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                    dropInfo.Effects = DragDropEffects.Copy;
                }
            }

            void IDropTarget.Drop( IDropInfo dropInfo )
            {
                if( dropInfo.Data is MediaTag tag )
                    tags.Add( tag );
            }
        }

        public DialogResult<bool> Result { get; } = new();

        public MediaEntry Entry { get; }

        public ObservableCollection<MediaTag> Tags { get; }

        public TagNameModel AddTagModel { get; } = new();

        public TagDragDropHandler EntryTagsDragDropHandler { get; }
        public TagDragDropHandler AllTagsDragDropHandler { get; }

        public EditTagsDialog( MediaEntry entry, ObservableCollection<MediaTag> tags )
        {
            (Entry, Tags) = (entry, tags);
            EntryTagsDragDropHandler = new( Entry.Tags );
            AllTagsDragDropHandler = new( Tags );
            InitializeComponent();
        }

        private void OKButton_Click( object sender, RoutedEventArgs e ) =>
            Result.Set( true );

        private void AddTagButton_Click( object sender, RoutedEventArgs e )
        {
            if( AddTagModel.IsValid() )
            {
                Tags.Add( new() {
                    Name = AddTagModel.Name,
                    Color = Color.FromRgb( (byte)Random.Shared.Next( 256 ), (byte)Random.Shared.Next( 256 ), (byte)Random.Shared.Next( 256 ) )
                } );
            }
        }
    }
}
