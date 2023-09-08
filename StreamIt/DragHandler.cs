using GongSolutions.Wpf.DragDrop;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Com.Josh2112.StreamIt
{
    public class DragHandler : DefaultDragHandler
    {
        public override bool CanStartDrag( IDragInfo dragInfo ) =>
            dragInfo.PositionInDraggedItem.Y >= 0;
    }

    public class DropHandler : IDropTarget
    {
        public event EventHandler? ListModified;

        public ObservableCollection<MediaEntry>? Media { get; set; }

        public void DragOver( IDropInfo dropInfo )
        {
            if( Media is not null )
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        public void Drop( IDropInfo dropInfo )
        {
            if( Media is not null && dropInfo.Data is MediaEntry source && dropInfo.TargetGroup?.Name is string groupName )
            {
                if( source == dropInfo.TargetItem ) return;

                int sourceIdx = Media.IndexOf( source ),
                    targetIdx = dropInfo.TargetItem is MediaEntry target ? Media.IndexOf( target ) : 0;
                if( sourceIdx < targetIdx ) --targetIdx;
                if( dropInfo.InsertPosition.HasFlag( RelativeInsertPosition.AfterTargetItem ) ) ++targetIdx;

                var oldGroup = source.Group;

                source.Group = groupName;
                if( sourceIdx != targetIdx )
                {
                    Media.Move( sourceIdx, targetIdx );

                    foreach( var placeholder in Media.OfType<EmptyGroupPlaceholder>().Where( p => p.Group == groupName ).ToList() )
                        Media.Remove( placeholder );

                    // If we've removed the last item from this group, add an empty placeholder to keep it from getting deleted
                    if( !Media.Any( e => e.Group == oldGroup ) )
                        Media.Add( new EmptyGroupPlaceholder( oldGroup ) );

                    ListModified?.Invoke( this, EventArgs.Empty );
                }
            }
        }
    }
}