using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Catel.Data;
using DJI.WindowsSDK;
using DJI.WindowsSDK.Components;
using Windows.UI.Xaml.Controls;
using Catel.MVVM;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI.Core;

namespace HDCircles.Hackathon
{
    class Drone
    {
        #region Constants

        public const int       PRODUCT_ID = 0;
        public const int       PRODUCT_INDEX = 0;

        private const string    APP_KEY = "cb98b917674f98a483eb9228";
        #endregion Constants

        #region Fields
        public static bool _isRegistered { get; set; }
        public bool _isInitialized = false;
        #endregion Fields

        #region Property
        //public bool IsRegistered { get; set; }
        //public string SdkAppKey
        //{
        //    get => GetValue<String>(SdkAppKeyProperty);
        //    set => SetValue(SdkAppKeyProperty, value);

        //}
        //public static PropertyData SdkAppKeyProperty = RegisterProperty(nameof(SdkAppKey), typeof(string));
        //#endregion Property

        public Drone()
        {
            var sdkInstance = DJISDKManager.Instance;

            if (DJISDKManager.Instance.SDKRegistrationResultCode < 0)
            {
                System.Diagnostics.Debug.WriteLine("Info:Drone:Try to register app {0}", DJISDKManager.Instance.SDKRegistrationResultCode);
                sdkInstance.SDKRegistrationStateChanged += Init;
                
                DJISDKManager.Instance.RegisterApp(APP_KEY);
                 Task.Delay(100);

            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Info:Warning:Drone:App is regisstered suuccesfully");
            }
            _isRegistered = DJISDKManager.Instance.SDKRegistrationResultCode == SDKError.NO_ERROR;
            System.Diagnostics.Debug.WriteLine("Info:Drone:isRegistered: {0}", DJISDKManager.Instance.SDKRegistrationResultCode);

            if (_isRegistered)
            {
                //Init();


            }
        }


        private async void Init(SDKRegistrationState state, SDKError errorCode)
        {
            System.Diagnostics.Debug.WriteLine("Info:Init:Drone is initialised.");
            //_fcHandler.VelocityChanged += FlightControllerHandler_VelocityChanged;

            //stateTimer = new System.Timers.Timer(STATETIMER_UPDATE_FREQUENCE);
            //stateTimer.Elapsed += StateTimer_Elapsed;
            //stateTimer.AutoReset = true;
            //stateTimer.Enabled = true;

            //var connection = await wifiHandler.GetConnectionAsync();

            _isInitialized = true;

        }

        #region Components Handler


        public static WiFiHandler GetWifiHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetWiFiHandler(PRODUCT_ID, PRODUCT_INDEX);
        }


        public static FlightControllerHandler GetFlightControllerHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        public static CameraHandler GetCameraHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetCameraHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        public static GimbalHandler GetGimbalHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetGimbalHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        public static BatteryHandler GetBatteryHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetBatteryHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        #endregion Components Handler


        #region Callbacks
        //private async void StateTimer_Elapsed(object sender, ElapsedEventArgs e)
        //{
        //    //System.Diagnostics.Debug.WriteLine("Info:StateTimer_Elapsed:startime: {}", DateTime.Now);
        //    await UpdateAltitude();
        //    await UpdateAttitude();
        //    await UpdateVelocity();
        //    await UpdateGimbalAttitude();
        //    await UpdateVideoFeedFps();
        //    //await UpdateChargeRemaining();
        //}


        //async Task UpdateAttitude()
        //{
        //    var attitude = await GetFlightControllerHandler().GetAttitudeAsync();

        //    if (attitude.value.HasValue)
        //    {
        //        await CallOnUiThreadAsync(() =>
        //        {
        //            Attitude = attitude.value.Value;
        //        });
        //    }
        //}

        //async Task UpdateGimbalAttitude()
        //{
        //    var attitude = await GetGimbalHandler().GetGimbalAttitudeAsync();

        //    if (attitude.value.HasValue)
        //    {
        //        await CallOnUiThreadAsync(() =>
        //        {
        //            GimbalAttitude = attitude.value.Value;
        //        });
        //    }
        //}

        //async Task UpdateAltitude()
        //{
        //    var altitude = await GetFlightControllerHandler().GetAltitudeAsync();

        //    if (altitude.value.HasValue)
        //    {
        //        await CallOnUiThreadAsync(() =>
        //        {
        //            Altitude = altitude.value.Value.value;
        //        });
        //    }
        //}

        //async Task UpdateVelocity()
        //{
        //    var velocity = await GetFlightControllerHandler().GetVelocityAsync();

        //    if (velocity.value.HasValue)
        //    {
        //        await CallOnUiThreadAsync(() =>
        //        {
        //            Velocity = velocity.value.Value;
        //        });
        //    }
        //}

        //async Task UpdateVideoFeedFps()
        //{
        //    await CallOnUiThreadAsync(() =>
        //    {
        //        RaisePropertyChanged(nameof(ImageFpsText));
        //    });
        //}

        //async Task UpdateChargeRemaining()
        //{
        //    var chargeRemaining = await GetBatteryHandler().GetChargeRemainingInPercentAsync();

        //    if (chargeRemaining.value.HasValue)
        //    {
        //        await CallOnUiThreadAsync(() =>
        //        {
        //            ChargeRemainingInPercent = chargeRemaining.value.Value.value;
        //        });
        //    }
        //}
        #endregion
        #region Events
        //private async Task FlightControllerHandler_VelocityChanged(object sender, Velocity3D? value)
        //{
        //    if (value.HasValue)
        //    {
        //        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        //        {
        //            var unboxed = value.Value;

        //            VelocityX = unboxed.x;
        //            VelocityY = unboxed.y;
        //            VelocityZ = unboxed.z;
        //        });
        //    }
        //}

        #endregion Events

    }
}
#endregion