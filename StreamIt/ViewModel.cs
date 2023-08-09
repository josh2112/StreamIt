using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreAudio;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace Com.Josh2112.StreamIt
{
    public partial class AddStationForm : ObservableValidator
    {
        [ObservableProperty]
        [Required]
        [CustomValidation( typeof( AddStationForm ), nameof( ValidateUrlOrFilePath ) )]
        private string _urlOrFilePath = "";

        public static ValidationResult ValidateUrlOrFilePath( string value, ValidationContext context )
        {
            if( Uri.TryCreate( value, UriKind.Absolute, out Uri? uri ) )
            {
                if( uri.IsFile && File.Exists( uri.LocalPath ) && Path.GetExtension( uri.LocalPath ).ToLower() == ".pls" &&
                    ParseFirstPlaylistEntry( uri ).Item1 != null )
                    return ValidationResult.Success!;

                if( uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp )
                    return ValidationResult.Success!;
            }
            return new( "Must be a URL or playlist file" );
        }

        public static (string?, string?) ParseFirstPlaylistEntry( Uri uri )
        {
            var lines = File.ReadAllLines( uri.LocalPath );
            var title = lines.FirstOrDefault( l => l.StartsWith( "Title1" ) )?.Split( "=" )?.ElementAt( 1 ) ?? null;
            var url = lines.FirstOrDefault( l => l.StartsWith( "File1" ) )?.Split( "=" )?.ElementAt( 1 ) ?? null;
            return (url, title);
        }

        public bool Validate()
        {
            ValidateAllProperties();
            return !HasErrors;
        }

        public void ClearErrors() => base.ClearErrors();
    }

    public partial class ViewModel : ObservableObject
    {
        private readonly Dispatcher dispatcher;

        private LibVLCSharp.Shared.LibVLC? vlc;
        private LibVLCSharp.Shared.MediaPlayer? mediaPlayer;

        private readonly DispatcherTimer cutOffTimer = new();
        private readonly DispatcherTimer elapsedTimeTimer = new() { Interval = TimeSpan.FromSeconds( 0.5 ) };

        public SnackbarMessageQueue SnackbarMessages { get; } = new();

        [ObservableProperty]
        private Settings _settings = Settings.DEFAULT;

        [ObservableProperty]
        private ListCollectionView? _mediaEntries;

        public AddStationForm AddStationForm { get; } = new();

        public DragHandler DragHandler { get; } = new();

        public DropHandler DropHandler { get; } = new();

        private DateTime? lastStartTime;

        public TimeSpan? ElapsedTime => lastStartTime is not null ? TimeSpan.FromSeconds(
            Math.Round( (DateTime.Now - lastStartTime).Value.TotalSeconds )) : null;

        public MediaEntry? SelectedMedia => MediaEntries?.CurrentItem as MediaEntry;

        public MediaEntry? LoadedMedia => mediaPlayer?.Media is null ? null : Settings.MediaEntries.FirstOrDefault( m => m.Media?.Mrl == mediaPlayer?.Media?.Mrl );

        public ViewModel() => dispatcher = Dispatcher.CurrentDispatcher;

        [ObservableProperty]
        private SimpleAudioVolume? _mixer;

        [ObservableProperty]
        private bool _isVlcInitialized;

        public IEnumerable<string> GroupNames => Settings.MediaEntries.Select( m => m.Group ).Distinct();

        public event EventHandler<string?>? NowPlayingChanged;

        public event EventHandler<EmptyGroupPlaceholder>? RenameGroupInvoked;

        public async Task InitializeAsync()
        {
            elapsedTimeTimer.Tick += ( s, e ) => OnPropertyChanged( nameof( ElapsedTime ) );
            elapsedTimeTimer.Start();

            Settings = Settings.Load();

            MediaEntries = new( Settings.MediaEntries );
            MediaEntries.GroupDescriptions.Add( new PropertyGroupDescription( nameof( MediaEntry.Group ) ) );
            MediaEntries.IsLiveGrouping = true;

            DropHandler.Media = Settings.MediaEntries;
            DropHandler.ListModified += ( s, e ) => Settings.Save();

            MediaEntries.CurrentChanged += ( s, e ) => OnPropertyChanged( nameof( SelectedMedia ) );

            await Task.Run( () => {
                vlc = new LibVLCSharp.Shared.LibVLC();
                mediaPlayer = new LibVLCSharp.Shared.MediaPlayer( vlc );
            } );

            IsVlcInitialized = true;

            mediaPlayer!.MediaChanged += ( s, e ) => dispatcher.Invoke( () => OnPropertyChanged( nameof( LoadedMedia ) ) );
            mediaPlayer!.Opening += ( s, e ) => dispatcher.Invoke( () => SetMediaState( MediaStates.Opening ) );

            mediaPlayer.Playing += ( s, e ) => dispatcher.Invoke( () => SetMediaState( MediaStates.Playing ) );

            mediaPlayer.EncounteredError += ( s, e ) => dispatcher.Invoke( () => {
                SetMediaState( MediaStates.Stopped );
                SnackbarMessages.Enqueue( $"Can't play {LoadedMedia!.DisplayName}" );
            } );

            foreach( var entry in Settings.MediaEntries.Except( Settings.MediaEntries.OfType<EmptyGroupPlaceholder>()))
                entry.Media = new LibVLCSharp.Shared.Media( vlc!, new Uri( entry.Uri ), ":no-video" );

            cutOffTimer.Tick += ( s, e ) => Stop( LoadedMedia! );
            SetCutOffTimer();

            var device = new MMDeviceEnumerator( Guid.NewGuid() ).GetDefaultAudioEndpoint( DataFlow.Render, Role.Multimedia );
            var session = device.AudioSessionManager2?.Sessions?.FirstOrDefault( s => s.ProcessID == Environment.ProcessId );
            session!.OnSimpleVolumeChanged += OnMixerVolumeChanged;
            Mixer = session?.SimpleAudioVolume;

            if( Mixer is not null )
            {
                void UpdateVolume() => Mixer.MasterVolume = (float)Math.Pow( Settings.Volume / 100.0, 3 );

                UpdateVolume();

                Settings.PropertyChanged += ( s, e ) => {
                    if( e.PropertyName == nameof( Settings.Volume ) )
                    {
                        UpdateVolume();
                        Settings.IsMute = false;
                    }
                    else if( e.PropertyName == nameof( Settings.IsMute ) )
                        Mixer.Mute = Settings.IsMute;
                };
            }
        }

        private void OnMixerVolumeChanged( object sender, float newVolume, bool newMute ) =>
            Settings.Volume = (int)Math.Round( newVolume * 100 );

        [RelayCommand]
        private void ToggleMute() => Settings.IsMute = !Settings.IsMute;

        private void SetCutOffTimer()
        {
            var now = DateTime.Now;

            var cutOffTime = new DateTime( now.Year, now.Month, now.Day, Settings.CutOffTime.Hours, Settings.CutOffTime.Minutes, Settings.CutOffTime.Seconds );
            if( cutOffTime < now ) cutOffTime = cutOffTime.AddDays( 1 );

            cutOffTimer.Interval = cutOffTime - now;
            cutOffTimer.Start();
        }

        private void SetMediaState( MediaStates state, MediaEntry? media = null )
        {
            media ??= LoadedMedia;

            if( media is MediaEntry entry )
            {
                Debug.WriteLine( $"Media state {entry.State} => {state} on {entry.Name}" );
                entry.State = state;

                if( state == MediaStates.Playing ) lastStartTime = DateTime.Now;
                else if( state == MediaStates.Stopped || state == MediaStates.Opening ) lastStartTime = null;

                PlayCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
            }
        }

        [RelayCommand]
        public void Play( MediaEntry media )
        {
            SetMediaState( MediaStates.Stopped );

            media.Media!.MetaChanged += OnMetadataChanged;
            mediaPlayer!.Play( media.Media );
        }

        private static bool CanStop( MediaEntry? entry ) => entry?.State == MediaStates.Playing;

        [RelayCommand( CanExecute = nameof( CanStop ))]
        public void Stop( MediaEntry? media )
        {
            if( media is not null )
            {
                media.Media!.MetaChanged -= OnMetadataChanged;
                SetMediaState( MediaStates.Stopped, media );
                NowPlayingChanged?.Invoke( this, null );
                mediaPlayer!.Stop();
            }
        }

        [RelayCommand]
        public static void SearchSong( string? text )
        {
            if( text is not null )
            {
                Process.Start( new ProcessStartInfo() {
                    FileName = new Uri( $"https://music.youtube.com/search?q={text}" ).AbsoluteUri.ToString(),
                    UseShellExecute = true
                } );
            }
        }

        public bool AddStation()
        {
            if( AddStationForm.Validate() )
            {
                string? url = AddStationForm.UrlOrFilePath;
                string? title = null;

                var uri = new Uri( AddStationForm.UrlOrFilePath );
                if( uri.IsFile )
                    (url, title) = AddStationForm.ParseFirstPlaylistEntry( uri );

                var entry = new MediaEntry {
                    Group = Settings.MediaEntries!.FirstOrDefault()?.Group ?? "New group",
                    Uri = url!,
                    Name = title ?? string.Empty
                };

                Settings.MediaEntries!.Add( entry );
                MediaEntries!.MoveCurrentTo( entry );

                Settings.Save();

                AddStationForm.UrlOrFilePath = "";

                return true;
            }

            return false;
        }

        public void RenameMedia( string oldValue, string newValue )
        {
            if( Settings.MediaEntries.FirstOrDefault( me => me.Name == oldValue ) is MediaEntry me )
                me.Name = newValue;

            Settings.Save();
        }

        [RelayCommand]
        public void DeleteMedia( MediaEntry entry )
        {
            Settings.MediaEntries.Remove( entry );
            Settings.Save();
        }

        [RelayCommand]
        private void AddGroup( string groupName )
        {
            var placeholder = new EmptyGroupPlaceholder( groupName );
            Settings.MediaEntries.Add( placeholder );
            RenameGroupInvoked?.Invoke( this, placeholder );
        }

        internal void RenameGroup( string oldName, string newName )
        {
            foreach( var entry in Settings.MediaEntries.Where( m => m.Group == oldName ) )
                entry.Group = newName;

            Settings.Save();
        }

        [RelayCommand]
        public void DeleteGroup( string groupName )
        {
            if( Settings.MediaEntries.OfType<EmptyGroupPlaceholder>().FirstOrDefault( p => p.Group == groupName ) is EmptyGroupPlaceholder placeholder )
                DeleteMedia( placeholder );
        }

        private void OnMetadataChanged( object? sender, LibVLCSharp.Shared.MediaMetaChangedEventArgs e ) => dispatcher.Invoke( async () => {
            if( LoadedMedia is not null )
            {
                var value = LoadedMedia.Media!.Meta( e.MetadataType );
                if( string.IsNullOrWhiteSpace( value ) ) return;

                Debug.WriteLine( $"Metadata changed: {e.MetadataType}: {value}" );

                switch( e.MetadataType )
                {
                    case LibVLCSharp.Shared.MetadataType.NowPlaying:
                        NowPlayingChanged?.Invoke( this, value );
                        if( LoadedMedia.CurrentSong?.Name != value! )
                            LoadedMedia.History.Add( new( value!, DateTime.Now ) );
                        if( LoadedMedia.CurrentSong!.SongData.Source == MetadataGrabber.Engines.None )
                        {
                            var song = LoadedMedia.CurrentSong!;
                            song.SongData = await MetadataGrabber.GetSongMetadata( song.Name,
                                MetadataGrabber.Engines.Deezer ) ?? song.SongData;
                        }
                        break;

                    case LibVLCSharp.Shared.MetadataType.Title:
                        LoadedMedia.Name = value!;
                        Settings.Save();
                        break;

                    case LibVLCSharp.Shared.MetadataType.ArtworkURL:
                        if( await MetadataGrabber.GetArtworkAsync( value!,
                            Path.Combine( Utils.DataPath.Value, LoadedMedia.Uri.GetHashCode().ToString() ) ) is string path )
                        {
                            LoadedMedia.ImageFilename = Path.GetFileName( path );
                            Settings.Save();
                        }
                        break;
                }
            }
        } );
    }
}