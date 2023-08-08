
using Com.Josh2112.StreamIt;
using CoreAudio;
using LibVLCSharp.Shared;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

var settings = Settings.Load();

var vlc = new LibVLC();
var mediaPlayer = new MediaPlayer( vlc ) {
    Volume = 0
};

//await SlurpSongTitles();

SlurpVLCVolume();

void SlurpVLCVolume()
{
    var device = new MMDeviceEnumerator( Guid.NewGuid() ).GetDefaultAudioEndpoint( DataFlow.Render, Role.Multimedia );
    var session = device.AudioSessionManager2?.Sessions?.FirstOrDefault( s => s.ProcessID == 7064 );
    session!.OnSimpleVolumeChanged += (s,vol,mute) => Console.WriteLine( vol );
    Console.ReadLine();
}

async Task TestGetSongMetadata( string title )
{
    var songData = await MetadataGrabber.GetSongMetadata( title, MetadataGrabber.Engines.Deezer );
    Console.WriteLine( $" - {(songData is not null ? "Got it!" : "no result")}" );
}

async Task SlurpSongTitles()
{
    var httpClient = new HttpClient();

    var path = Path.Combine( Utils.DataPath.Value, "songtitles.txt" );

    var titles = new HashSet<string>( File.Exists( path ) ? File.ReadAllLines( path ) : Array.Empty<string>() );

    foreach( var title in titles ) await TestGetSongMetadata( title );

    foreach( var entry in settings!.MediaEntries )
    {
        entry.Media = new Media( vlc!, new Uri( entry.Uri ), ":no-video" );
        entry.Media.MetaChanged += OnMetaChanged;
    }

    while( true )
    {
        foreach( var media in settings.MediaEntries )
        {
            mediaPlayer!.Play( media.Media! );
            await Task.Delay( TimeSpan.FromSeconds( 30 ) );
            mediaPlayer.Stop();
        }
    }

    async void OnMetaChanged( object? sender, MediaMetaChangedEventArgs e )
    {
        if( e.MetadataType == MetadataType.NowPlaying )
        {
            var value = mediaPlayer!.Media!.Meta( e.MetadataType );
            if( !string.IsNullOrWhiteSpace( value ) && titles.Add( value ) )
            {
                Console.WriteLine( value );
                File.AppendAllLines( path, new[] { value } );

                await TestGetSongMetadata( value );
            }
        }
    }
}
