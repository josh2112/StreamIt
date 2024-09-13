using Com.Josh2112.StreamIt.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CoreAudio;
using MaterialDesignThemes.Wpf;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Threading;

namespace Com.Josh2112.StreamIt
{
    public partial class ViewModel : ObservableObject
    {
        private readonly Dispatcher dispatcher;

        private LibVLCSharp.Shared.LibVLC? vlc;
        private LibVLCSharp.Shared.MediaPlayer? mediaPlayer;

        private readonly DispatcherTimer cutOffTimer = new();
        private readonly DispatcherTimer elapsedTimeTimer = new( DispatcherPriority.Render ) { Interval = TimeSpan.FromSeconds( 0.5 ) };

        public SnackbarMessageQueue SnackbarMessages { get; } = new();

        [ObservableProperty]
        private Settings _settings = Settings.DEFAULT;

        [ObservableProperty]
        private ListCollectionView? _mediaEntries;

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

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private string _searchText = "";

        private string searchTextLowercase = "";

        public event EventHandler<string?>? NowPlayingChanged;

        public async Task InitializeAsync()
        {
            elapsedTimeTimer.Tick += ( s, e ) => OnPropertyChanged( nameof( ElapsedTime ) );
            elapsedTimeTimer.Start();

            Settings = Settings.Load();

            MediaEntries = new( Settings.MediaEntries );

            MediaEntries.CurrentChanged += ( s, e ) => OnPropertyChanged( nameof( SelectedMedia ) );

            MediaEntries.Filter = ( obj ) => obj is MediaEntry me && PassesFilter( me );

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

            foreach( var entry in Settings.MediaEntries )
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

        partial void OnSearchTextChanged( string value )
        {
            searchTextLowercase = SearchText?.ToLower() ?? "";
            MediaEntries?.Refresh();
        }

        private bool PassesFilter( MediaEntry entry ) =>
            string.IsNullOrWhiteSpace( searchTextLowercase ) ||
            entry.Name.ToLower().Contains( searchTextLowercase ) ||
            entry.Tags.Any( t => t.Contains( searchTextLowercase ) );

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
            MediaEntries!.MoveCurrentTo( media );

            SetMediaState( MediaStates.Stopped );
            NowPlayingChanged?.Invoke( this, null );

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
        public void SearchSong( string? text )
        {
            if( text is not null )
            {
                Process.Start( new ProcessStartInfo() {
                    FileName = new Uri( $"https://music.youtube.com/search?q={text}" ).AbsoluteUri.ToString(),
                    UseShellExecute = true
                } );
            }
        }

        public async Task<MediaEntry?> AddStationAsync( AddStationModel station )
        {
            string? url = station.UrlOrFilePath;
            string? title = null;

            var uri = new Uri( station.UrlOrFilePath );

            if( url.ToLower() is string lower && lower.StartsWith( "http" ) && lower.EndsWith( ".pls" ) )
            {
                var response = await MetadataGrabber.HttpClient.GetAsync( uri );

                if( response.Content.Headers.ContentType?.MediaType == "audio/x-scpls" )
                {
                    var tmpfile = Path.GetTempFileName();
                    File.WriteAllText( tmpfile, await response.Content.ReadAsStringAsync() );
                    uri = new Uri( tmpfile );
                }
            }

            if( uri.IsFile )
            {
                (url, title) = AddStationModel.ParseFirstPlaylistEntry( uri );
            }

            if( url != null )
            {
                var entry = new MediaEntry {
                    Uri = url!,
                    Name = title ?? string.Empty
                };

                entry.Media = new LibVLCSharp.Shared.Media( vlc!, new Uri( entry.Uri ), ":no-video" );

                Settings.MediaEntries!.Add( entry );
                Settings.Save();

                return entry;
            }

            return null;
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
                            if( song.SongData.ImagePath != null )
                            {
                                LoadedMedia.LastSongImagePath = song.SongData.ImagePath!;
                                Settings.Save();
                            }
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

        public void DeleteMedia( MediaEntry entry )
        {
            if( LoadedMedia == entry )
                Stop( entry );

            Settings.MediaEntries.Remove( entry );
            Settings.Save();
        }
    }
}