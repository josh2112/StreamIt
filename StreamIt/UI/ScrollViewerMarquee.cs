using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Com.Josh2112.StreamIt.UI
{
    public class ScrollViewerMarquee : Behavior<ScrollViewer>
    {
        public static readonly DependencyProperty PixelsPerSecondProperty = DependencyProperty.Register( nameof( PixelsPerSecond ),
            typeof( double ), typeof( ScrollViewerMarquee ), new PropertyMetadata( 10d ) );

        public static readonly DependencyProperty HorizontalOffsetProperty = DependencyProperty.Register(
            nameof( HorizontalOffset ), typeof( double ), typeof( ScrollViewerMarquee ),
            new PropertyMetadata( ( s, e ) => (s as ScrollViewerMarquee)!.OnHorizontalOffsetChanged( (double)e.NewValue ) ) );
        
        public double PixelsPerSecond
        {
            get => (double)GetValue( PixelsPerSecondProperty );
            set => SetValue( PixelsPerSecondProperty, value );
        }
        
        public double HorizontalOffset
        {
            get => (double)GetValue( HorizontalOffsetProperty );
            set => SetValue( HorizontalOffsetProperty, value );
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.ScrollChanged += ScrollViewer_ScrollChanged;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.ScrollChanged -= ScrollViewer_ScrollChanged;
            base.OnDetaching();
        }

        private void ScrollViewer_ScrollChanged( object sender, ScrollChangedEventArgs e )
        {
            if( e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0 )
            {
                var end = AssociatedObject.ExtentWidth - AssociatedObject.ViewportWidth;
                if( end <= 0 ) return;

                var anim = new DoubleAnimation( 0, end, TimeSpan.FromSeconds( end / PixelsPerSecond ) ) {
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                BeginAnimation( HorizontalOffsetProperty, anim );
            }
        }

        private void OnHorizontalOffsetChanged( double newValue ) =>
            AssociatedObject.ScrollToHorizontalOffset( newValue );
    }
}
