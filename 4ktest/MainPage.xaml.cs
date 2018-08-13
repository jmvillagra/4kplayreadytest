using SDKTemplate.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Protection.PlayReady;
using Windows.Media.Streaming.Adaptive;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace _4ktest
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        MediaPlayer _player;
        PlayReadyHelper _playReadyHelper;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            btnPlay.Tapped += BtnPlay_Tapped;
        }

        private async void BtnPlay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(txtStreamUrl.Text))
            {
                var adaptiveMediaSourceResult = await AdaptiveMediaSource.CreateFromUriAsync(new Uri(txtStreamUrl.Text.Trim()));

                if (adaptiveMediaSourceResult.Status == AdaptiveMediaSourceCreationStatus.Success)
                {
                    mediaPlayerElement.Source = MediaSource.CreateFromAdaptiveMediaSource(adaptiveMediaSourceResult.MediaSource);
                    txtSL.Text = PlayReadyStatics.PlayReadyCertificateSecurityLevel.ToString();
                    txtHasHardwareDRM.Text = PlayReadyStatics.CheckSupportedHardware(PlayReadyHardwareDRMFeatures.HardwareDRM).ToString();
                    txtHasHEVCSupport.Text = PlayReadyStatics.CheckSupportedHardware(PlayReadyHardwareDRMFeatures.HEVC).ToString();
                }
                else
                {
                    Debug.WriteLine("Error opening the stream");
                }
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            var codecQuery = new CodecQuery();
            IReadOnlyList<CodecInfo> result;
            try
            {
                result = await codecQuery.FindAllAsync(CodecKind.Video, CodecCategory.Decoder, CodecSubtypes.VideoFormatHevc);
            }
            catch (Exception ex)
            {
                throw;
            }            

            StringBuilder sb = new StringBuilder();

            foreach (var codecInfo in result)
            {
                sb.Append("============================================================\n");
                sb.Append(string.Format("Codec: {0}\n", codecInfo.DisplayName));
                sb.Append(string.Format("Kind: {0}\n", codecInfo.Kind.ToString()));
                sb.Append(string.Format("Category: {0}\n", codecInfo.Category.ToString()));
                sb.Append(string.Format("Trusted: {0}\n", codecInfo.IsTrusted.ToString()));

                foreach (string subType in codecInfo.Subtypes)
                {
                    sb.Append(string.Format("   Subtype: {0}\n", subType));
                }
            }

            Debug.WriteLine(sb.ToString());

            _player = new MediaPlayer();
            _player.MediaFailed += Player_MediaFailed;
            _player.AutoPlay = true;
            _player.PlaybackSession.BufferingStarted += PlaybackSession_BufferingStarted;
            _player.BufferingStarted += Player_BufferingStarted;
            _player.PlaybackSession.PlaybackStateChanged += PlaybackSession_PlaybackStateChanged;

            _playReadyHelper = new PlayReadyHelper(); // From SDKTemplate.Helpers.PlayReadyHelper
            _playReadyHelper.SetUpProtectionManager(_player);

            mediaPlayerElement.SetMediaPlayer(_player);
            mediaPlayerElement.AutoPlay = true;          
        }

        private void PlaybackSession_PlaybackStateChanged(MediaPlaybackSession sender, object args)
        {
            Log($"PlaybackSession_PlaybackStateChanged: {sender.PlaybackState}");
        }

        private void Player_BufferingStarted(MediaPlayer sender, object args)
        {
            Log("Player_BufferingStarted");
        }

        private void PlaybackSession_BufferingStarted(MediaPlaybackSession sender, object args)
        {
            Log("PlaybackSession_BufferingStarted");
        }

        private void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            //
            Log("Player_MediaFailed");
        }

        private void Log(String msg)
        {
            Debug.WriteLine(msg);
        }
    }
}
