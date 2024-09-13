using CoreAudio;
using NAudio.Wave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Printing.IndexedProperties;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Com.Josh2112.StreamIt
{
    public class TESTING_StreamWithNAudio
    {
        string url;
        DispatcherTimer timer1;

        enum StreamingPlaybackState
        {
            Stopped,
            Playing,
            Buffering,
            Paused
        }

        private BufferedWaveProvider? bufferedWaveProvider;
        private IWavePlayer? directSoundOut;
        private VolumeWaveProvider16 volumeProvider;
        private volatile StreamingPlaybackState playbackState;
        private volatile bool fullyDownloaded;
        
        private static HttpClient httpClient = new();

        delegate void ShowErrorDelegate( string message );

        public TESTING_StreamWithNAudio( string url )
        {
            this.url = url;
            //volumeSlider1.VolumeChanged += OnVolumeSliderChanged;
            //Disposed += MP3StreamingPanel_Disposing;

            timer1 = new DispatcherTimer { Interval = TimeSpan.FromSeconds( 0.25 ) };
            timer1.Tick += timer1_Tick;
        }

        private void Thread_StreamMp3( object? state )
        {
            fullyDownloaded = false;
            var url = (string)state;

            int metadataInterval = 0;
            
            Stream? stream;
            try
            {
                //stream = httpClient.GetStreamAsync( url ).Result;

                var request = new HttpRequestMessage
                {
                    RequestUri = new Uri( url! ),
                    Method = HttpMethod.Get,
                    Headers =
                    {
                        { "icy-metadata", "1" }
                    }
                };

                var response = httpClient.SendAsync( request, HttpCompletionOption.ResponseHeadersRead ).Result;

                if( response.Headers.TryGetValues( "icy-metaint", out IEnumerable<string>? values ) )
                    metadataInterval = int.Parse( values.First() );

                stream = response.Content.ReadAsStreamAsync().Result;
            }
            catch( Exception e )
            {
                MessageBox.Show( e.Message );
                return;
            }

            var buffer = new byte[16384 * 4]; // needs to be big enough to hold a decompressed frame

            IMp3FrameDecompressor? decompressor = null;
            try
            {
                using( stream )
                {
                    var readFullyStream = new ReadFullyStream( stream );

                    do
                    {
                        if( IsBufferNearlyFull )
                        {
                            Debug.WriteLine( "Buffer getting full, taking a break" );
                            Thread.Sleep( 500 );
                        }
                        else
                        {
                            Mp3Frame frame;
                            try
                            {
                                frame = Mp3Frame.LoadFromStream( readFullyStream );
                            }
                            catch( EndOfStreamException )
                            {
                                fullyDownloaded = true;
                                // reached the end of the MP3 file / stream
                                break;
                            }
                            if( frame == null ) break;
                            if( decompressor == null )
                            {
                                // don't think these details matter too much - just help ACM select the right codec
                                // however, the buffered provider doesn't know what sample rate it is working at
                                // until we have a frame
                                decompressor = CreateFrameDecompressor( frame );
                                bufferedWaveProvider = new BufferedWaveProvider( decompressor.OutputFormat )
                                {
                                    BufferDuration = TimeSpan.FromSeconds( 20 ) // allow us to get well ahead of ourselves
                                };
                            }
                            int decompressed = decompressor.DecompressFrame( frame, buffer, 0 );
                            
                            bufferedWaveProvider.AddSamples( buffer, 0, decompressed );
                        }

                    } while( playbackState != StreamingPlaybackState.Stopped );

                    Debug.WriteLine( "Exiting" );
                    // was doing this in a finally block, but for some reason
                    // we are hanging on response stream .Dispose so never get there
                    decompressor.Dispose();
                }
            }
            finally
            {
                decompressor?.Dispose();
                decompressor = null;
            }
        }

        private static IMp3FrameDecompressor CreateFrameDecompressor( Mp3Frame frame )
        {
            WaveFormat waveFormat = new Mp3WaveFormat( frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2,
                frame.FrameLength, frame.BitRate );
            return new AcmMp3FrameDecompressor( waveFormat );
        }

        private bool IsBufferNearlyFull
        {
            get
            {
                return bufferedWaveProvider != null &&
                       bufferedWaveProvider.BufferLength - bufferedWaveProvider.BufferedBytes
                       < bufferedWaveProvider.WaveFormat.AverageBytesPerSecond / 4;
            }
        }

        public void Start()
        {
            if( playbackState == StreamingPlaybackState.Stopped )
            {
                playbackState = StreamingPlaybackState.Buffering;
                bufferedWaveProvider = null;
                ThreadPool.QueueUserWorkItem( Thread_StreamMp3, url );
                timer1.IsEnabled = true;
            }
            else if( playbackState == StreamingPlaybackState.Paused )
            {
                playbackState = StreamingPlaybackState.Buffering;
            }
        }

        public void StopPlayback()
        {
            if( playbackState != StreamingPlaybackState.Stopped )
            {
                if( !fullyDownloaded )
                {
                    //webRequest.Abort();
                }

                playbackState = StreamingPlaybackState.Stopped;
                if( directSoundOut != null )
                {
                    directSoundOut.Stop();
                    directSoundOut.Dispose();
                    directSoundOut = null;
                }
                timer1.IsEnabled = false;
                // n.b. streaming thread may not yet have exited
                Thread.Sleep( 500 );
                ShowBufferState( 0 );
            }
        }

        private void ShowBufferState( double totalSeconds )
        {
            System.Diagnostics.Debug.WriteLine(  $"{totalSeconds:0.0}s" );
        }

        private void timer1_Tick( object? sender, EventArgs e )
        {
            if( playbackState != StreamingPlaybackState.Stopped )
            {
                if( directSoundOut == null && bufferedWaveProvider != null )
                {
                    Debug.WriteLine( "Creating WaveOut Device" );
                    directSoundOut = new DirectSoundOut();
                    directSoundOut.PlaybackStopped += OnPlaybackStopped;
                    volumeProvider = new VolumeWaveProvider16( bufferedWaveProvider );
                    directSoundOut.Init( volumeProvider );
                    //progressBarBuffer.Maximum = (int)bufferedWaveProvider.BufferDuration.TotalMilliseconds;
                }
                else if( bufferedWaveProvider != null )
                {
                    var bufferedSeconds = bufferedWaveProvider.BufferedDuration.TotalSeconds;
                    ShowBufferState( bufferedSeconds );
                    // make it stutter less if we buffer up a decent amount before playing
                    if( bufferedSeconds < 0.5 && playbackState == StreamingPlaybackState.Playing && !fullyDownloaded )
                    {
                        Pause();
                    }
                    else if( bufferedSeconds > 4 && playbackState == StreamingPlaybackState.Buffering )
                    {
                        Play();
                    }
                    else if( fullyDownloaded && bufferedSeconds == 0 )
                    {
                        Debug.WriteLine( "Reached end of stream" );
                        StopPlayback();
                    }
                }

            }
        }

        private void Play()
        {
            directSoundOut.Play();
            Debug.WriteLine( String.Format( "Started playing, directSoundOut.PlaybackState={0}", directSoundOut.PlaybackState ) );
            playbackState = StreamingPlaybackState.Playing;
        }

        private void Pause()
        {
            playbackState = StreamingPlaybackState.Buffering;
            directSoundOut.Pause();
            Debug.WriteLine( String.Format( "Paused to buffer, directSoundOut.PlaybackState={0}", directSoundOut.PlaybackState ) );
        }

        private void MP3StreamingPanel_Disposing( object sender, EventArgs e )
        {
            StopPlayback();
        }

        private void buttonPause_Click( object sender, EventArgs e )
        {
            if( playbackState == StreamingPlaybackState.Playing || playbackState == StreamingPlaybackState.Buffering )
            {
                directSoundOut.Pause();
                Debug.WriteLine( String.Format( "User requested Pause, directSoundOut.PlaybackState={0}", directSoundOut.PlaybackState ) );
                playbackState = StreamingPlaybackState.Paused;
            }
        }

        private void OnPlaybackStopped( object sender, StoppedEventArgs e )
        {
            Debug.WriteLine( "Playback Stopped" );
            if( e.Exception != null )
            {
                MessageBox.Show( $"Playback Error {e.Exception.Message}" );
            }
        }
    }

    public class ReadFullyStream( Stream sourceStream ) : Stream
    {
        private long pos; // psuedo-position
        private readonly byte[] readAheadBuffer = new byte[4096];
        private int readAheadLength;
        private int readAheadOffset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush() =>
            throw new InvalidOperationException();

        public override long Length => pos;

        public override long Position
        {
            get => pos;
            set => throw new InvalidOperationException();
        }

        long nextMetadata = 16384;

        public override int Read( byte[] buffer, int offset, int count )
        {
            int bytesRead = 0;
            while( bytesRead < count )
            {
                int readAheadAvailableBytes = readAheadLength - readAheadOffset;
                int bytesRequired = count - bytesRead;
                if( readAheadAvailableBytes > 0 )
                {
                    int toCopy = Math.Min( readAheadAvailableBytes, bytesRequired );
                    Array.Copy( readAheadBuffer, readAheadOffset, buffer, offset + bytesRead, toCopy );
                    bytesRead += toCopy;
                    readAheadOffset += toCopy;
                }
                else
                {
                    readAheadOffset = 0;
                    readAheadLength = sourceStream.Read( readAheadBuffer, 0, readAheadBuffer.Length );
                    
                    if( readAheadLength == 0 )
                    {
                        break;
                    }
                }
            }

            pos += bytesRead;

            // "Str" = 83, 116, 114
            var start = Array.IndexOf( buffer, 83, offset, count );
            if( start >= 0 && start + 2 < count && buffer[start + 1] == 116 && buffer[start + 2] == 114 )
            {
                var len = buffer[start-1] * 16;
                var str = Encoding.UTF8.GetString( buffer, start, len );
                Debug.WriteLine( "METADATA:" + str );
            }

            return bytesRead;
        }

        public override long Seek( long offset, SeekOrigin origin ) =>
            throw new InvalidOperationException();

        public override void SetLength( long value ) =>
            throw new InvalidOperationException();

        public override void Write( byte[] buffer, int offset, int count ) =>
            throw new InvalidOperationException();
    }

}
