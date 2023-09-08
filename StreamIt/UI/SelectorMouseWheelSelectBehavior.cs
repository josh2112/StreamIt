using Microsoft.Xaml.Behaviors;
using System;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Com.Josh2112.StreamIt.UI
{
    public class SelectorMouseWheelSelectBehavior : Behavior<Selector>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseWheel += ListView_PreviewMouseWheel;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseWheel -= ListView_PreviewMouseWheel;
            base.OnDetaching();
        }

        private void ListView_PreviewMouseWheel( object sender, MouseWheelEventArgs e )
        {
            var idx = AssociatedObject.SelectedItem is null ? 0 : AssociatedObject.SelectedIndex;
            AssociatedObject.SelectedIndex = Math.Clamp( idx + (e.Delta < 1 ? -1 : 1), 0, AssociatedObject.Items.Count - 1 );
        }
    }
}
