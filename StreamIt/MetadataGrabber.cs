using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Com.Josh2112.StreamIt
{
    public class DeezerSearch
    {
        public record Artist( long Id, string Name );

        public record Album( long Id, string Title, string Cover_Medium );

        public record Track( long Id, string Title, Artist Artist, Album Album );

        public record SearchResponse( Track[] Data, int Total );

        public static async Task<(SongData?, string?)> Search( HttpClient client, SongData song )
        {
            var query = new[] {
                $"track:\"{song.Title}\"",
                song.Artist is not null ? $"artist:\"{song.Artist}\"" : null,
                song.Album is not null ? $"album:\"{song.Album}\"" : null
            }.OfType<string>();

            var uri = new Uri( $"https://api.deezer.com/search?q={string.Join( " ", query )}" ).AbsoluteUri;

            var response = await client.GetFromJsonAsync<SearchResponse>( new Uri( $"https://api.deezer.com/search?q={uri}" ).AbsoluteUri );
            return response?.Data.FirstOrDefault() is Track data
                ? (new( MetadataGrabber.Engines.Deezer, data.Id, data.Title, data.Artist.Name, data.Album.Title, song.Year ), data.Album.Cover_Medium)
                : (null, null);
        }
    }
    
    public static partial class MetadataGrabber
    {
        public enum Engines { None, Deezer };

        public static HttpClient HttpClient { get; } = new();

        // Delicious Agony: "ARTIST - TRACK, from the YEAR album `ALBUM`"
        [GeneratedRegex( @"(?'artist'.*) -{1,2} (?'track'.*), from the (?'year'\d+) album `(?'album'.*)`" )]
        private static partial Regex DelicousAgonyRegex();

        // Sleepbot: "ARTIST -- TRACK -- ALBUM (YEAR) -- /SONGID"
        [GeneratedRegex( @"(?'artist'.+) -{1,2} (?'track'.+) -{1,2} (?'album'.+) \((?'year'\d+)\) -{1,2} \/.*" )]
        private static partial Regex SleepbotRegex();

        // Standard: "ARTIST - TRACK[ - ALBUM][ (New)| (Live)]
        [GeneratedRegex( @"(?'artist'.*?) -{1,2} (?'track'.*?)(?: -{1,2} (?'album'.*?))?(?= \(New\)$| \(Live\)$|$)" )]
        private static partial Regex StandardRegex();

        public static SongData ParseSongTitle( string title )
        {
            var regexes = typeof( MetadataGrabber )
                .GetMethods( System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )
                .Where( m => m.ReturnType == typeof( Regex ) )
                .Select( m => m.Invoke( null, null ) )
                .OfType<Regex>();

            string? artist = null, track = null, album = null, year = null;

            foreach( Regex regex in regexes )
            {
                if( regex.Match( title ) is Match match && match.Success )
                {
                    if( match.Groups.TryGetValue( "artist", out Group? g1 ) ) artist = g1.Value;
                    if( match.Groups.TryGetValue( "track", out Group? g2 ) ) track = g2.Value;
                    if( match.Groups.TryGetValue( "album", out Group? g3 ) ) album = g3.Value;
                    if( match.Groups.TryGetValue( "year", out Group? g4 ) ) year = g4.Value;
                    break;
                }
            }

            return new SongData( Engines.None, 0, track ?? title, artist ?? string.Empty, album ?? string.Empty, year );
        }

        public static async Task<SongData?> GetSongMetadata( string title, Engines engine )
        {
            var parsedSongData = ParseSongTitle( title );

            var (songData,imageUri) = engine switch {
                Engines.Deezer => await DeezerSearch.Search( HttpClient, parsedSongData ),
                _ => throw new NotImplementedException( $"Unknown song metadata engine \"{engine}\"" )
            };

            songData ??= parsedSongData;

            if( imageUri is not null )
                songData.ImagePath = await GetArtworkAsync( imageUri, Path.GetTempFileName() );
            
            return songData;
        }

        public static async Task<string?> GetArtworkAsync( string uri, string filePathWithoutExtension )
        {
            var response = await HttpClient.GetAsync( uri );

            if( response.Content.Headers.ContentType?.MediaType is string mimeType &&
                Utils.GetFileExtensionForMimeType( mimeType ) is string ext )
            {
                Debug.WriteLine( $"Downloading artwork of type {mimeType}" );
                string path = $"{filePathWithoutExtension}{ext}";

                await File.WriteAllBytesAsync( path, await response.Content.ReadAsByteArrayAsync() );
                return path;
            }
            else return null;
        }
    }
}
