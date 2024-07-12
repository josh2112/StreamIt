using MaterialDesignThemes.Wpf;
using Microsoft.Xaml.Behaviors;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Com.Josh2112.StreamIt.UI
{
    public class ThemeAwareWindowChromeBehavior : Behavior<Window>
    {
        static ThemeAwareWindowChromeBehavior() => DetermineWindowsVersion();

        protected override void OnAttached()
        {
            base.OnAttached();

            var hwnd = new WindowInteropHelper( AssociatedObject ).Handle;

            AssociatedObject.SourceInitialized += ( s, e ) => ConfigureWindowChrome(
                new PaletteHelper().GetTheme().GetBaseTheme() == BaseTheme.Dark );

            AssociatedObject.Loaded += ( s, e ) =>
            {
                var paletteHelper = new PaletteHelper();
                UpdateWindowChrome( paletteHelper.GetTheme().GetBaseTheme() );

                if( paletteHelper.GetThemeManager() is IThemeManager mgr )
                    mgr.ThemeChanged += ThemeManager_ThemeChanged;
            };

            AssociatedObject.Unloaded += ( s, e ) =>
            {
                Detach();
                if( new PaletteHelper().GetThemeManager() is IThemeManager mgr )
                    mgr.ThemeChanged -= ThemeManager_ThemeChanged;
            };
        }

        private void ThemeManager_ThemeChanged( object? sender, ThemeChangedEventArgs e )
        {
            if( e.NewTheme.GetBaseTheme() != e.OldTheme.GetBaseTheme() )
                UpdateWindowChrome( e.NewTheme.GetBaseTheme() );
        }

        void ConfigureWindowChrome( bool isDarkMode )
        {
            // Set the window background to transparent to see the mica/acrylic effects.
            // Unfortunately MDIX has its own background color, MaterialDesignPaper, 
            // which is used as the background of many controls, so doing this creates
            // an inconsistent look.
            //if( windowsVersion == 11 )
            //    AssociatedObject.Background = System.Windows.Media.Brushes.Transparent;

            var hwnd = new WindowInteropHelper( AssociatedObject ).Handle;

            // The CompositionTarget is where the window visuals are actually built. Make sure its
            // background color is transparent so we can see the Mica effects.
            HwndSource.FromHwnd( hwnd ).CompositionTarget.BackgroundColor = System.Windows.Media.Color.FromArgb( 0, 0, 0, 0 );

            DwmMargins margins = new()
            {
                cxLeftWidth = -1,
                cxRightWidth = -1,
                cyTopHeight = -1,
                cyBottomHeight = -1
            };

            // Normally the Mica effect only applies to the title bar. Extend it to cover the whole window.
            _ = DwmExtendFrameIntoClientArea( hwnd, ref margins );


            int darkMode = isDarkMode ? 1 : 0;
            
            if( windowsBuildNumber > 22523 )
            {
                // Enable Mica (newer Win 11 builds)
                int backdropType = (int)DwmSystemBackdropType.DWMSBT_MAINWINDOW;
                _ = DwmSetWindowAttribute( hwnd, DwmWindowAttribute.DWMWA_SYSTEMBACKDROP_TYPE,
                        ref backdropType, Marshal.SizeOf( typeof( int ) ) );
            }
            else if( windowsBuildNumber > 22000 )
            {
                // Enable Mica (older Win 11 builds)
                int trueValue = 1;
                _ = DwmSetWindowAttribute( hwnd, DwmWindowAttribute.DWMWA_MICA_EFFECT,
                    ref trueValue, Marshal.SizeOf( typeof( int ) ) );
            }
            else if( windowsBuildNumber >= 18985 )
            {
                // Enable immersive dark mode (newer Win 10 builds)
                _ = DwmSetWindowAttribute( hwnd, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref darkMode, Marshal.SizeOf( typeof( int ) ) );
            }
            else if( windowsBuildNumber >= 17763 )
            {
                // Enable immersive dark mode (older Win 10 builds)
                _ = DwmSetWindowAttribute( hwnd, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                    ref darkMode, Marshal.SizeOf( typeof( int ) ) );
            }
        }

        void UpdateWindowChrome( BaseTheme baseTheme )
        {
            var hwnd = new WindowInteropHelper( AssociatedObject ).Handle;
            int isDarkMode = baseTheme == BaseTheme.Dark ? 1 : 0;

            if( windowsBuildNumber >= 18985 )
            {
                // Enable immersive dark mode (newer Win 10 builds)
                _ = DwmSetWindowAttribute( hwnd, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE,
                    ref isDarkMode, Marshal.SizeOf( typeof( int ) ) );
            }
            else if( windowsBuildNumber >= 17763 )
            {
                // Enable immersive dark mode (older Win 10 builds)
                _ = DwmSetWindowAttribute( hwnd, DwmWindowAttribute.DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                    ref isDarkMode, Marshal.SizeOf( typeof( int ) ) );
            }

            if( windowsVersion == 10 )
            {
                AssociatedObject.Hide();
                AssociatedObject.Show();
            }
        }

        #region DLL imports

        [DllImport( "DwmApi.dll" )]
        private static extern int DwmSetWindowAttribute( IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute );

        [DllImport( "DwmApi.dll" )]
        private static extern int DwmExtendFrameIntoClientArea( IntPtr hwnd, ref DwmMargins pMarInset );

        [DllImport( "user32.dll", CharSet = CharSet.Auto )]
        static extern IntPtr SendMessage( IntPtr hWnd, uint Msg, UIntPtr wParam, IntPtr lParam );


        [StructLayout( LayoutKind.Sequential )]
        private struct DwmMargins
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        };

        private enum WmMessages : uint
        {
            WM_SETREDRAW = 11,
        }

        [Flags]
        private enum DwmWindowAttribute : uint
        {
            DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19,
            DWMWA_USE_IMMERSIVE_DARK_MODE = 20,
            DWMWA_SYSTEMBACKDROP_TYPE = 38,
            DWMWA_MICA_EFFECT = 1029,
        }

        private enum DwmSystemBackdropType : uint
        {
            DWMSBT_AUTO = 0,
            DWMSBT_DISABLE = 1, // None
            DWMSBT_MAINWINDOW = 2, // Mica
            DWMSBT_TRANSIENTWINDOW = 3, // Acrylic
            DWMSBT_TABBEDWINDOW = 4 // Tabbed
        }

        #endregion DLL imports

        #region Windows version

        private static int? windowsVersion
        {
            get
            {
                if( windowsBuildNumber >= 22000 ) return 11;
                else if( windowsBuildNumber >= 10000 ) return 10;
                else if( windowsBuildNumber >= 9200 ) return 8;
                else return 0;
            }
        }
        private static int? windowsBuildNumber;

        private static void DetermineWindowsVersion()
        {
            if( windowsBuildNumber.HasValue )
                return;

#if NETCOREAPP
            // .NET or .NET Core: we can rely on Environment.OSVersion
            windowsBuildNumber = Environment.OSVersion.Version.Build;
#else
            // .NET Framework never returns a higher version than Windows 8
            // so we need to access the Registry
            const string path = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
            const string key = "CurrentBuild";
            using( RegistryKey reg = Registry.LocalMachine.OpenSubKey( path ) )
            {
                int.TryParse( (string)reg.GetValue( key ), out int build );
                windowsBuildNumber = build;
            }
#endif
        }

        #endregion // Windows version
    }
}
