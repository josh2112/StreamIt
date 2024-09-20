using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Data;

namespace Com.Josh2112.StreamIt
{
    public enum MediaStates { Stopped, Opening, Playing }

    public record SongData( MetadataGrabber.Engines Source, long ID, string Title,
        string? Artist = null, string? Album = null, string? Year = null )
    {
        public string? ImagePath { get; set; } = null;
    }

    public partial class Song : ObservableObject
    {
        public string Name { get; }

        public DateTime Start { get; }

        [ObservableProperty]
        private SongData _songData = new( MetadataGrabber.Engines.None, 0, "" );

        public Song( string name, DateTime start ) => (Name, Start) = (name, start);
    }

    public partial class MediaEntry : ObservableObject
    {
        public ObservableCollection<string> Tags { get; set; } = [];

        [ObservableProperty]
        private string _name = "";

        public string DisplayName => !string.IsNullOrWhiteSpace( Name ) ? Name : Uri;

        public string Uri { get; set; } = "";

        [ObservableProperty]
        [NotifyPropertyChangedFor( nameof( ImagePath ) )]
        private string _imageFilename = "";

        [ObservableProperty]
        [property: JsonIgnore]
        private MediaStates _state;

        public ObservableCollection<Song> History { get; set; } = [];

        public ListCollectionView HistoryCollection { get; private set; } = null!;

        [JsonIgnore]
        public Song? CurrentSong => History.FirstOrDefault();

        [JsonIgnore]
        public string? ImagePath
        {
            get
            {
                if( Path.Combine( Utils.DataPath.Value, ImageFilename ) is string p && File.Exists( p ) )
                    return p;
                else if( File.Exists( CurrentSong?.SongData?.ImagePath ) )
                    return CurrentSong.SongData.ImagePath;
                else
                    return null;
            }
        }

        [JsonIgnore]
        public LibVLCSharp.Shared.Media? Media { get; set; }

        partial void OnImageFilenameChanged( string value ) => OnPropertyChanged( nameof( ImagePath ) );

        public MediaEntry()
        {
            History.CollectionChanged += ( s, e ) => OnPropertyChanged( nameof( CurrentSong ) );
        }

        public void CreateHistoryCollection()
        {
            HistoryCollection = new( History );
            HistoryCollection.GroupDescriptions.Add( new PropertyGroupDescription( "Start.Date" ) );

        }

        partial void OnNameChanged( string value ) => OnPropertyChanged( nameof( DisplayName ) );
    }


    public partial class Settings : ObservableObject
    {
        private static readonly string SETTINGS_PATH = Path.Combine( Utils.DataPath.Value, "settings.json" );

        private static readonly JsonSerializerOptions JSON_OPTS = new() {
            WriteIndented = true,
        };

        public static readonly Settings DEFAULT = new() {
            Volume = 50,
            CutOffTime = new TimeSpan( 18, 0, 0 )
        };

        public static Settings Load()
        {
            try
            {
                return JsonSerializer.Deserialize<Settings>( File.ReadAllText( SETTINGS_PATH ), JSON_OPTS ) ?? DEFAULT;
            }
            catch( FileNotFoundException )
            {
                DEFAULT.Save();
                return DEFAULT;
            }
            catch( Exception )
            {
                return DEFAULT;
            }
        }

        public void Save() => File.WriteAllText( SETTINGS_PATH, JsonSerializer.Serialize( this, JSON_OPTS ) );

        [ObservableProperty]
        private int _volume;

        [ObservableProperty]
        [property: JsonIgnore]
        private bool _isMute;

        [ObservableProperty]
        private TimeSpan _cutOffTime;

        public ObservableCollection<MediaEntry> MediaEntries { get; } = [];

        public Settings( ObservableCollection<MediaEntry>? mediaEntries = null ) =>
            MediaEntries = mediaEntries ?? MediaEntries;
    }
}