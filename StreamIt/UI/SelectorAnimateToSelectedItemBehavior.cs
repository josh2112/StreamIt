using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Com.Josh2112.StreamIt.UI
{
    public class SelectorAnimateToSelectedItemBehavior : Behavior<Selector>
    {
        public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
            nameof( HorizontalOffset ), typeof( double ), typeof( SelectorAnimateToSelectedItemBehavior ),
            new PropertyMetadata( (s,e) => (s as SelectorAnimateToSelectedItemBehavior)!.OnHorizontalOffsetChanged( (double)e.NewValue ) ) );

        public double HorizontalOffset
        {
            get => (double)GetValue( HorizontalOffsetProperty );
            set => SetValue( HorizontalOffsetProperty, value );
        }

        private ScrollViewer? scrollViewer;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.LayoutUpdated += Selector_LayoutUpdated;
            AssociatedObject.SelectionChanged += Selector_SelectionChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SelectionChanged -= Selector_SelectionChanged;
            base.OnDetaching();
        }

        private void Selector_LayoutUpdated( object? sender, EventArgs e )
        {
            AssociatedObject.LayoutUpdated -= Selector_LayoutUpdated;
            scrollViewer = AssociatedObject.FirstVisualDescendantOfType<ScrollViewer>();

            HorizontalOffset = scrollViewer!.HorizontalOffset;
        }

        private void Selector_SelectionChanged( object sender, SelectionChangedEventArgs e )
        {
            if( AssociatedObject.SelectedIndex < 0 ) return;

            if( AssociatedObject.ItemContainerGenerator.ContainerFromIndex( AssociatedObject.SelectedIndex ) is FrameworkElement listItem )
            {
                var newX = VisualTreeHelper.GetOffset( listItem ).X - scrollViewer!.ViewportWidth / 2 + listItem.ActualWidth / 2;

                var anim = new DoubleAnimation {
                    To = newX,
                    Duration = TimeSpan.FromSeconds( 0.3 ),
                    EasingFunction = new QuadraticEase(),
                };
                BeginAnimation( HorizontalOffsetProperty, anim, HandoffBehavior.Compose );
            }
        }

        private void OnHorizontalOffsetChanged( double newValue ) =>
            scrollViewer?.ScrollToHorizontalOffset( newValue );
    }

    public static class VisualTreeExtensions
    {
        public static T? FirstVisualDescendantOfType<T>( this DependencyObject obj ) where T  : DependencyObject
        {
            if( obj is null ) return null;

            for( var i = 0; i < VisualTreeHelper.GetChildrenCount( obj ); i++ )
            {
                var child = VisualTreeHelper.GetChild( obj, i );
                var result = child as T ?? FirstVisualDescendantOfType<T>( child );
                if( result is not null ) return result;
            }

            return null;
        }

        public static T? FindVisualAncestorOfType<T>( DependencyObject child ) where T : DependencyObject
        {
            if( child is null ) return null;

            do
            {
                child = VisualTreeHelper.GetParent( child );
            }
            while( child != null && !typeof( T ).IsAssignableFrom( child.GetType() ) );

            return child as T;
        }
    }
}
