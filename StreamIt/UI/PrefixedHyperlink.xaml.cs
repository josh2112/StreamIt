using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Com.Josh2112.StreamIt.UI
{
    /// <summary>
    /// Interaction logic for PrefixedHyperlink.xaml
    /// </summary>
    public partial class PrefixedHyperlink : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register( nameof( Text ), typeof( string ), typeof( PrefixedHyperlink ), new PropertyMetadata( null ) );
        
        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register( nameof( Prefix ), typeof( string ), typeof( PrefixedHyperlink ), new PropertyMetadata( null ) );

        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register( nameof( Command ), typeof( ICommand ), typeof( PrefixedHyperlink ), new PropertyMetadata( null ) );

        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register( nameof( CommandParameter ), typeof( object ), typeof( PrefixedHyperlink ), new PropertyMetadata( null ) );

        public string Text
        {
            get => (string)GetValue( TextProperty );
            set => SetValue( TextProperty, value );
        }

        public string Prefix
        {
            get => (string)GetValue( PrefixProperty );
            set => SetValue( PrefixProperty, value );
        }

        public ICommand Command
        {
            get => (ICommand)GetValue( CommandProperty );
            set => SetValue( CommandProperty, value );
        }

        public object CommandParameter
        {
            get => GetValue( CommandParameterProperty );
            set => SetValue( CommandParameterProperty, value );
        }

        public PrefixedHyperlink() => InitializeComponent();

        private void Hyperlink_Navigate( object sender, RoutedEventArgs e ) =>
            Command.Execute( CommandParameter );
    }
}
