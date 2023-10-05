using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Com.Josh2112.StreamIt
{
    public enum MediaStates { Stopped, Opening, Playing }

    public record SongData( MetadataGrabber.Engines Source, long ID, string Title, string? Artist = null, string? Album = null, string? Year = null )
    {
        public string? ImagePath { get; set; } = null;
    }

    public partial class Song : ObservableObject
    {
        public string Name { get; }
        public DateTime? Start { get; }

        [ObservableProperty]
        private SongData _songData = new( MetadataGrabber.Engines.None, 0, "" );

        public Song( string name, DateTime? start ) => (Name, Start) = (name, start);
    }

    public partial class MediaEntry : ObservableObject
    {
        public ObservableCollection<string> Tags { get; set; } = new();

        [ObservableProperty]
        private string _name = "";

        public string DisplayName => !string.IsNullOrWhiteSpace( Name ) ? Name : Uri;

        public string Uri { get; set; } = "";

        [ObservableProperty]
        private string _imageFilename = "";

        [ObservableProperty]
        private string _lastSongImagePath = "";

        [ObservableProperty]
        [property: JsonIgnore]
        private MediaStates _state;

        [JsonIgnore]
        public ObservableCollection<Song> History { get; } = new();

        [JsonIgnore]
        public Song? CurrentSong => History.LastOrDefault();

        [JsonIgnore]
        public string? ImagePath => !string.IsNullOrEmpty( ImageFilename ) ? Path.Combine( Utils.DataPath.Value, ImageFilename ) :
            !string.IsNullOrEmpty( LastSongImagePath ) ? LastSongImagePath : null;

        [JsonIgnore]
        public LibVLCSharp.Shared.Media? Media { get; set; }

        partial void OnImageFilenameChanged( string value ) => OnPropertyChanged( nameof( ImagePath ) );
        partial void OnLastSongImagePathChanged( string value ) => OnPropertyChanged( nameof( ImagePath ) );

        public MediaEntry() => History.CollectionChanged += ( s, e ) => OnPropertyChanged( nameof( CurrentSong ) );

        partial void OnNameChanged( string value ) => OnPropertyChanged( nameof( DisplayName ) );
    }


    public partial class Settings : ObservableObject
    {
        private static readonly string SETTINGS_PATH = Path.Combine( Utils.DataPath.Value, "settings.json" );

        private static readonly JsonSerializerOptions JSON_OPTS = new() {
            WriteIndented = true,
            Converters = {
                new ColorJsonConverter()
            }
        };

        public static readonly Settings DEFAULT = new( new() {
            new MediaEntry() {
                Name = "Progulus",
                Uri = "http://149.56.234.136:9992/stream",
            } } ) {
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

        public ObservableCollection<MediaEntry> MediaEntries { get; } = new();

        public Settings( ObservableCollection<MediaEntry>? mediaEntries = null ) =>
            MediaEntries = mediaEntries ?? MediaEntries;
    }

    public class ColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options ) =>
            (Color)ColorConverter.ConvertFromString( reader.GetString() );

        public override void Write( Utf8JsonWriter writer, Color value, JsonSerializerOptions options ) =>
            writer.WriteStringValue( value.ToString() );
    }
}