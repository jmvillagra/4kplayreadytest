using SDKTemplate.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.Protection;
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
        MediaProtectionManager _protectionManager;

        String _licenseOverride;

        public MainPage()
        {
            this.InitializeComponent();
            this.Loaded += MainPage_Loaded;
            btnPlay.Tapped += BtnPlay_Tapped;
            cbHardwareDRM.Checked += CbHardwareDRM_Checked;
            cbHardwareDRM.Unchecked += CbHardwareDRM_Unchecked;
        }

        private void CbHardwareDRM_Unchecked(object sender, RoutedEventArgs e)
        {
            ConfigureSoftwareDRM();
        }

        private void CbHardwareDRM_Checked(object sender, RoutedEventArgs e)
        {
            ConfigureHardwareDRM();
        }

        private async void BtnPlay_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _licenseOverride = txtLicenseOverride.Text;

            if (!String.IsNullOrWhiteSpace(txtStreamUrl.Text))
            {                
                var adaptiveMediaSourceResult = await AdaptiveMediaSource.CreateFromUriAsync(new Uri(txtStreamUrl.Text.Trim()));

                if (adaptiveMediaSourceResult.Status == AdaptiveMediaSourceCreationStatus.Success)
                {
                    mediaPlayerElement.Source = MediaSource.CreateFromAdaptiveMediaSource(adaptiveMediaSourceResult.MediaSource);
                }
                else
                {
                    Debug.WriteLine("Error opening the stream");
                }
            }
        }

        private void ConfigureHardwareDRM()
        {
            Debug.WriteLine("ConfigureHardwareDRM");
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.CreateContainer("PlayReady", Windows.Storage.ApplicationDataCreateDisposition.Always);
            localSettings.Containers["PlayReady"].Values["SoftwareOverride"] = 0;
            localSettings.Containers["PlayReady"].Values["HardwareOverride"] = 1;
            _player.ProtectionManager.Properties.Remove("Windows.Media.Protection.UseSoftwareProtectionLayer");
            _player.ProtectionManager.Properties["Windows.Media.Protection.UseHardwareProtectionLayer"] = true;
        }

        /// <summary>
        /// This method will configure the properties used by Media Foundation and PlayReady
        /// to specify Software DRM. The existing ProtectionManager assigned to the MediaElement
        /// is altered with updated/removed properties. Lastly, proactive individualiation is called.
        /// </summary>
        private void ConfigureSoftwareDRM()
        {
            Debug.WriteLine("ConfigureSoftwareDRM");
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            localSettings.CreateContainer("PlayReady", Windows.Storage.ApplicationDataCreateDisposition.Always);
            localSettings.CreateContainer("PlayReady", Windows.Storage.ApplicationDataCreateDisposition.Always);
            localSettings.Containers["PlayReady"].Values["SoftwareOverride"] = 1;
            localSettings.Containers["PlayReady"].Values["HardwareOverride"] = 0;
            _player.ProtectionManager.Properties.Remove("Windows.Media.Protection.UseHardwareProtectionLayer");
            _player.ProtectionManager.Properties["Windows.Media.Protection.UseSoftwareProtectionLayer"] = true;
        }

        private async Task RefreshPlayreadyStats()
        {
            await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                txtSL.Text = PlayReadyStatics.PlayReadyCertificateSecurityLevel.ToString();
                txtHasHardwareDRM.Text = PlayReadyStatics.CheckSupportedHardware(PlayReadyHardwareDRMFeatures.HardwareDRM).ToString();
                txtHasHEVCSupport.Text = PlayReadyStatics.CheckSupportedHardware(PlayReadyHardwareDRMFeatures.HEVC).ToString();
            });
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

            _protectionManager = PlayReadyHelpers.InitializeProtectionManager(ServiceRequested);
            _player.ProtectionManager = _protectionManager;

            //_playReadyHelper = new PlayReadyHelper(); // From SDKTemplate.Helpers.PlayReadyHelper
            //_playReadyHelper.SetUpProtectionManager(_player);

            mediaPlayerElement.SetMediaPlayer(_player);
            mediaPlayerElement.AutoPlay = true;          
        }

        /// <summary>
        /// The ProtectionManager defers the service call to the ServiceReqested handler.
        /// This handler will enable the application to customize the communication (custom data, http headers, manual request)
        /// The ServiceCompletion instance will notify the ProtectionManager in the case of queued requests.
        /// </summary>
        MediaProtectionServiceCompletion serviceCompletionNotifier = null;
        void ServiceRequested(MediaProtectionManager sender, ServiceRequestedEventArgs srEvent)
        {
            serviceCompletionNotifier = srEvent.Completion;
            IPlayReadyServiceRequest serviceRequest = (IPlayReadyServiceRequest)srEvent.Request;
            Debug.WriteLine(serviceRequest.GetType().Name);
            ProcessServiceRequest(serviceRequest);
        }

        /// <summary>
        /// The helper class will determine the exact type of ServiceRequest in order to customize and send
        /// the service request. ServiceRequests (except for Individualization and Revocation) also support the
        /// GenerateManualEnablingChallenge method. This can be used to read and customize the SOAP challenge
        /// and manually send the challenge.
        /// </summary>
        async Task ProcessServiceRequest(IMediaProtectionServiceRequest serviceRequest)
        {
            //Alternatively the serviceRequest can be determined by the Guid serviceRequest.Type
            if (serviceRequest is PlayReadyIndividualizationServiceRequest)
            {
                
                PlayReadyHelpers.ReactiveIndividualization(serviceRequest as PlayReadyIndividualizationServiceRequest, serviceCompletionNotifier, () => RefreshPlayreadyStats());
                RefreshPlayreadyStats();
            }
            else if (serviceRequest is PlayReadyLicenseAcquisitionServiceRequest)
            {
                var licenseRequest = serviceRequest as PlayReadyLicenseAcquisitionServiceRequest;
                // The initial service request url was taken from the playready header from the dash manifest.
                // This can overridden to a different license service prior to sending the request (staging, production,...). 

                if (!String.IsNullOrWhiteSpace(_licenseOverride))
                {
                    licenseRequest.Uri = new Uri(_licenseOverride);
                }

                PlayReadyHelpers.ReactiveLicenseAcquisition(licenseRequest, serviceCompletionNotifier);
            }

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
