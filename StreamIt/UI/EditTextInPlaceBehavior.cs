using Microsoft.Xaml.Behaviors;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Com.Josh2112.StreamIt.UI
{
    public class ValueChangedEventArgs<T> : EventArgs
    {
        public DependencyObject Source { get; set; }

        public T OldValue { get; }

        public T NewValue { get; }

        public ValueChangedEventArgs( T oldValue, T newValue, DependencyObject source ) =>
            (Source, OldValue, NewValue) = (source, oldValue, newValue);
    }

    public class ValueChangingEventArgs<T> : ValueChangedEventArgs<T>
    {
        public bool Cancel { get; set; }

        public ValueChangingEventArgs( T oldValue, T newValue, DependencyObject source ) : base( oldValue, newValue, source ) { }
    }

    internal class VisualAdorner : Adorner
    {
        private readonly VisualCollection visualCollection;
        private readonly ContentPresenter contentPresenter = new();

        public Visual Content
        {
            get => (Visual)contentPresenter.Content;
            set => contentPresenter.Content = value;
        }
        
        public VisualAdorner( UIElement adornedElement ) : base( adornedElement )
        {
            visualCollection = new( this ) {
                contentPresenter
            };
        }

        protected override int VisualChildrenCount => visualCollection.Count; 
        
        protected override Visual GetVisualChild( int index ) => visualCollection[index];

        protected override Size MeasureOverride( Size constraint )
        {
            contentPresenter.Measure( constraint );
            return contentPresenter.DesiredSize;
        }

        protected override Size ArrangeOverride( Size finalSize )
        {
            contentPresenter.Arrange( new Rect( 0, 0, finalSize.Width, finalSize.Height ) );
            return contentPresenter.RenderSize;
        }
    }

    public class EditTextInPlaceBehavior : Behavior<TextBlock>
    {
        public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register( nameof( IsEditing ), typeof( bool ),
            typeof( EditTextInPlaceBehavior ), new PropertyMetadata( false,
                ( s, e ) => ((EditTextInPlaceBehavior)s)!.OnIsEditingChanged() ) );

        public static readonly DependencyProperty UpdateSourceProperty = DependencyProperty.Register( nameof( UpdateSource ), typeof( bool ),
            typeof( EditTextInPlaceBehavior ), new PropertyMetadata( true ) );

        public static readonly DependencyProperty TextBoxProperty = DependencyProperty.Register( nameof( TextBox ), typeof( TextBox ),
            typeof( EditTextInPlaceBehavior ), new PropertyMetadata() );

        public event EventHandler<ValueChangingEventArgs<string>>? TextChanging;
        public event EventHandler<ValueChangedEventArgs<string>>? TextChanged;

        private VisualAdorner? adorner;

        public bool IsEditing
        {
            get => (bool)GetValue( IsEditingProperty );
            set => SetValue( IsEditingProperty, value );
        }

        public bool UpdateSource
        {
            get => (bool)GetValue( UpdateSourceProperty );
            set => SetValue( UpdateSourceProperty, value );
        }

        public TextBox TextBox
        {
            get => (TextBox)GetValue( TextBoxProperty );
            set
            {
                if( TextBox is null )
                    SetValue( TextBoxProperty, value );
                else
                    throw new InvalidOperationException( $"{nameof( EditTextInPlaceBehavior )}: Once set, {nameof( TextBox )} binding cannot be changed!" );
            }
        }

        private void OnIsEditingChanged()
        {
            if( IsEditing )
            {
                if( adorner is null )
                {
                    TextBox.KeyUp += (s, e) => {
                        if( e.Key == Key.Enter )
                            OnEditEnded( accepted: true );
                        else if( e.Key == Key.Escape )
                            OnEditEnded();
                    };
                    TextBox.LostFocus += ( s, e ) => OnEditEnded();

                    adorner = new VisualAdorner( AssociatedObject ) {
                        Content = TextBox
                    };
                }

                AdornerLayer.GetAdornerLayer( AssociatedObject ).Add( adorner );
                AssociatedObject.Visibility = Visibility.Hidden;

                TextBox.Text = AssociatedObject.Text;

                TextBox.Dispatcher.BeginInvoke( async () => {
                    await Task.Delay( 100 );
                    TextBox.SelectAll();
                    TextBox.Focus();
                } );
            }
            else
            {
                AdornerLayer.GetAdornerLayer( AssociatedObject ).Remove( adorner );
                AssociatedObject.Visibility = Visibility.Visible;
            }
        }

        private void OnEditEnded( bool accepted = false )
        {
            IsEditing = false;
            if( accepted )
            {
                var args = new ValueChangingEventArgs<string>( AssociatedObject.Text, TextBox.Text, AssociatedObject );
                TextChanging?.Invoke( this, args );

                if( !args.Cancel )
                {
                    if( UpdateSource ) AssociatedObject.Text = TextBox.Text;
                    TextChanged?.Invoke( this, new( args.OldValue, TextBox.Text, AssociatedObject ) );
                }
            }
        }

        public static EditTextInPlaceBehavior? For( TextBlock textBlock ) =>
            Interaction.GetBehaviors( textBlock )?.OfType<EditTextInPlaceBehavior>().FirstOrDefault();
    }
}
