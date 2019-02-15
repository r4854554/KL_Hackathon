using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DJI.WindowsSDK;
using DJI.WindowsSDK.Components;
using DJIVideoParser;


namespace HDCircles.Hackathon
{
    class DjiSdk
    {
        #region Constants

        private const int PRODUCT_ID = 0;
        private const int PRODUCT_INDEX = 0;
        private const string APP_KEY = "cb98b917674f98a483eb9228";


        #endregion Constants

        #region Fields
        public DJISDKManager _sdkManager;
        #endregion

        #region Property
        public bool IsRegistered { get; set; }
        #endregion

        DjiSdk(MainPageViewModel mainPageViewModel)
        {
            _sdkManager = DJISDKManager.Instance;

            _sdkManager.SDKRegistrationStateChanged += DJKSDKManager_SDKRegistrationStateChanged;
            _sdkManager.RegisterApp(APP_KEY);

        }

        private async void DJKSDKManager_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            IsRegistered = errorCode == SDKError.NO_ERROR;
            
            RegistrationStateText = IsRegistered ? "Registered" : $"Not Registered - {state},{errorCode}";
            
            if (!IsRegistered) return;

            await UpdateCurrentState("sdk registered!");

            if (_isInitialized) return;

            await CallOnUiThreadAsync(async () =>
            {
                var rootGrid = MainPage.FindName("RootGrid") as Grid;

                if (rootGrid != null)
                {
                    rootGrid.KeyDown += MainPage_KeyDown;
                    rootGrid.KeyUp += MainPage_KeyUp;
                }

                var fcHandler = GetFlightControllerHandler();
                var cameraHandler = GetCameraHandler();
                var gimbalHandler = GetGimbalHandler();

                fcHandler.VelocityChanged += FlightControllerHandler_VelocityChanged;

                _videoParser = new Parser();
                _videoParser.Initialize(VideoParserVideoAssitantInfoParserHandler);
                _videoParser.SetSurfaceAndVideoCallback(PRODUCT_ID, PRODUCT_INDEX, SwapChainPanel, VideoParserVideoDataCallback);

                DJISDKManager.Instance.VideoFeeder.GetPrimaryVideoFeed(PRODUCT_INDEX).VideoDataUpdated += VideoFeed_VideoDataUpdated;

                cameraHandler.CameraTypeChanged += CameraHandler_CameraTypeChanged;

                var cameraType = await cameraHandler.GetCameraTypeAsync();

                CameraHandler_CameraTypeChanged(null, cameraType.value);

                stateTimer = new Timer(STATETIMER_UPDATE_FREQUENCE);
                stateTimer.Elapsed += StateTimer_Elapsed;
                stateTimer.AutoReset = true;
                stateTimer.Enabled = true;

                _isInitialized = true;
            });
        }


    }
}
