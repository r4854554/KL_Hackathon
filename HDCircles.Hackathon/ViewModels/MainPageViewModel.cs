namespace HDCircles.Hackathon.ViewModels
{
    using Catel.Data;
    using Catel.MVVM;
    using DJI.WindowsSDK;
    using DJI.WindowsSDK.Components;
    using DJIVideoParser;
    using System;
    using System.Threading.Tasks;
    using System.Windows.Input;
    using Windows.ApplicationModel.Core;
    using Windows.System;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public class MainPageViewModel : ViewModelBase
    {
        private const float MAX_JOYSTICK_VALUE = 0.5f;
        private const float MIN_JOYSTICK_VALUE = -0.5f;
        private const float JOYSTICK_THROTTLE_STEP = 0.02f;
        private const float JOYSTICK_ATTITUDE_STEP = 0.05f;

        private const int PRODUCT_ID = 0;
        private const int PRODUCT_INDEX = 0;
        private const string APP_KEY = "cb98b917674f98a483eb9228";

        private readonly ICommandManager _commandManager;
        private DJISDKManager _instance;

        /// <summary>
        /// the instance of DJIVideoParser
        /// </summary>
        private Parser _videoParser;

        /// <summary>
        /// for joystick parameters
        /// </summary>
        private float throttle = 0.0f;
        private float pitch = 0.0f;
        private float roll = 0.0f;
        private float yaw = 0.0f;

        #region static Methods

        static async Task CallOnUiThreadAsync(CoreDispatcher dispatcher, DispatchedHandler handler)
        {
            if (dispatcher.HasThreadAccess)
            {
                handler.Invoke();
            }
            else
            {
                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, handler);
            }
        }

        static async Task CallOnUiThreadAsync(DispatchedHandler handler) =>
            await CallOnUiThreadAsync(Dispatcher, handler);

        #endregion static Methods

        #region Properties

        /// <remark>
        /// will be set at loaded event of the page
        /// </remark>
        public SwapChainPanel SwapChainPanel { get; set; }

        public static CoreDispatcher Dispatcher { get; set; }

        public bool IsRegistered { get; set; }

        public string RegistrationStateText
        {
            get => GetValue<string>(RegistrationStateTextProperty);
            set => SetValue(RegistrationStateTextProperty, value);
        }
        public static PropertyData RegistrationStateTextProperty = RegisterProperty(nameof(RegistrationStateText), typeof(string));

        public string CurrentStateText
        {
            get => GetValue<string>(CurrentStateTextProperty);
            set => SetValue(CurrentStateTextProperty, value);
        }

        public static PropertyData CurrentStateTextProperty = RegisterProperty(nameof(CurrentStateText), typeof(string));

        public float Throttle
        {
            get => GetValue<float>(ThrottleProperty);
            set => SetValue(ThrottleProperty, value);
        }
        public static PropertyData ThrottleProperty = RegisterProperty(nameof(Throttle), typeof(float));

        public double Pitch
        {
            get => GetValue<double>(PitchProperty);
            set => SetValue(PitchProperty, value);
        }
        public static PropertyData PitchProperty = RegisterProperty(nameof(Pitch), typeof(double));

        public double Roll
        {
            get => GetValue<double>(RollProperty);
            set => SetValue(RollProperty, value);
        }
        public static PropertyData RollProperty = RegisterProperty(nameof(Roll), typeof(double));

        public double Yaw
        {
            get => GetValue<double>(YawProperty);
            set => SetValue(YawProperty, value);
        }
        public static PropertyData YawProperty = RegisterProperty(nameof(Yaw), typeof(double));

        public double VelocityX
        {
            get => GetValue<double>(VelocityXProperty);
            set => SetValue(VelocityXProperty, value);
        }
        public static PropertyData VelocityXProperty = RegisterProperty(nameof(VelocityX), typeof(double));

        public double VelocityY
        {
            get => GetValue<double>(VelocityYProperty);
            set => SetValue(VelocityYProperty, value);
        }
        public static PropertyData VelocityYProperty = RegisterProperty(nameof(VelocityY), typeof(double));

        public double VelocityZ
        {
            get => GetValue<double>(VelocityZProperty);
            set => SetValue(VelocityZProperty, value);
        }
        public static PropertyData VelocityZProperty = RegisterProperty(nameof(VelocityZ), typeof(double));

        public CameraType CameraType
        {
            get => GetValue<CameraType>(CameraTypeProperty);
            set => SetValue(CameraTypeProperty, value);
        }
        public static PropertyData CameraTypeProperty = RegisterProperty(nameof(CameraType), typeof(CameraType));

        #endregion Properties

        public MainPageViewModel(ICommandManager commandManager)
        {
            _commandManager = commandManager;

            MainPageLoadedCommand = new TaskCommand(MainPageLoadedExecute);
            TakeOffCommand = new TaskCommand(TakeOffExecute);
            LandingCommand = new TaskCommand(LandingExecute);
            KeyDownCommand = new TaskCommand<VirtualKey>(KeyDownExecute);
            KeyUpCommand = new TaskCommand<VirtualKey>(KeyUpExecute);

            commandManager.RegisterCommand(Commands.MainPageLoaded, MainPageLoadedCommand, this);
            commandManager.RegisterCommand(Commands.KeyDown, KeyDownCommand, this);
            commandManager.RegisterCommand(Commands.KeyUp, KeyUpCommand, this);
        }

        async Task UpdateCurrentState(string message)
        {
            await CallOnUiThreadAsync(() =>
            {
                CurrentStateText = message;
            });
        }

        async Task StartRecording()
        {
            var result = await GetCameraHandler().StartRecordAsync();

            if (result != SDKError.NO_ERROR)
            {

            }
        }

        int[] VideoParserVideoAssitantInfoParserHandle(byte[] data)
        {
            return _instance.VideoFeeder.ParseAssitantDecodingInfo(PRODUCT_INDEX, data);
        }

        #region Components Handler

        FlightControllerHandler GetFlightControllerHandler()
        {
            return _instance.ComponentManager.GetFlightControllerHandler(0, 0);
        }

        CameraHandler GetCameraHandler()
        {
            return _instance.ComponentManager.GetCameraHandler(0, 0);
        }

        GimbalHandler GetGimbalHandler()
        {
            return _instance.ComponentManager.GetGimbalHandler(0, 0);
        }

        #endregion Components Handler

        #region Commands

        #region MainPageLoaded Command

        public ICommand MainPageLoadedCommand { get; set; }

        private async Task MainPageLoadedExecute()
        {
            _instance = DJISDKManager.Instance;

            _instance.SDKRegistrationStateChanged += DJKSDKManager_SDKRegistrationStateChanged;
            _instance.RegisterApp(APP_KEY);

            await Task.Delay(1000);
        }

        private async void DJKSDKManager_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            IsRegistered = errorCode == SDKError.NO_ERROR;

            await CallOnUiThreadAsync(() =>
            {
                RegistrationStateText = IsRegistered ? "Registered" : $"Not Registered - {state},{errorCode}";
            });

            if (!IsRegistered) return;

            await UpdateCurrentState("sdk registered!");

            await CallOnUiThreadAsync(async () =>
            {
                var fcHandler = GetFlightControllerHandler();
                var cameraHandler = GetCameraHandler();

                fcHandler.AttitudeChanged += FlightControllerHandler_AttitudeChanged;
                fcHandler.VelocityChanged += FlightControllerHandler_VelocityChanged;

                _videoParser = new Parser();
                _videoParser.Initialize(VideoParserVideoAssitantInfoParserHandle);
                _videoParser.SetSurfaceAndVideoCallback(PRODUCT_ID, PRODUCT_INDEX, SwapChainPanel, VideoParserVideoDataCallback);

                _instance.VideoFeeder.GetPrimaryVideoFeed(PRODUCT_INDEX).VideoDataUpdated += VideoFeed_VideoDataUpdated;

                cameraHandler.CameraTypeChanged += CameraHandler_CameraTypeChanged;

                var cameraType = await cameraHandler.GetCameraTypeAsync();

                CameraHandler_CameraTypeChanged(null, cameraType.value);
            });
        }

        #endregion MainPageLoaded Command

        #region TakeOffCommand

        public ICommand TakeOffCommand { get; set; }
        private async Task TakeOffExecute()
        {
            var fcHandler = GetFlightControllerHandler();

            await fcHandler.StartTakeoffAsync();
        }

        #endregion TakeOffCommand

        #region Landing Command

        public ICommand LandingCommand { get; set; }
        private async Task LandingExecute()
        {
            var fcHandler = GetFlightControllerHandler();

            await fcHandler.StartAutoLandingAsync();
        }

        #endregion Landing Command

        #region KeyDown Command

        public ICommand KeyDownCommand { get; set; }
        private async Task KeyDownExecute(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.W:
                    throttle += JOYSTICK_THROTTLE_STEP;

                    if (throttle >= MAX_JOYSTICK_VALUE)
                    {
                        throttle = MAX_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.S:
                    throttle -= JOYSTICK_THROTTLE_STEP;

                    if (throttle <= MIN_JOYSTICK_VALUE)
                    {
                        throttle = MIN_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.A:
                    yaw -= JOYSTICK_ATTITUDE_STEP;

                    if (yaw <= MIN_JOYSTICK_VALUE)
                    {
                        yaw = MIN_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.D:
                    yaw += JOYSTICK_ATTITUDE_STEP;

                    if (yaw >= MAX_JOYSTICK_VALUE)
                    {
                        yaw = MAX_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.I:
                    pitch += JOYSTICK_ATTITUDE_STEP;

                    if (pitch >= MAX_JOYSTICK_VALUE)
                    {
                        pitch = MAX_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.K:
                    pitch -= JOYSTICK_ATTITUDE_STEP;

                    if (pitch <= MIN_JOYSTICK_VALUE)
                    {
                        pitch = MIN_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.J:
                    roll -= JOYSTICK_ATTITUDE_STEP;

                    if (roll <= MIN_JOYSTICK_VALUE)
                    {
                        roll = MIN_JOYSTICK_VALUE;
                    }
                    break;
                case VirtualKey.L:
                    roll += JOYSTICK_ATTITUDE_STEP;

                    if (roll >= MAX_JOYSTICK_VALUE)
                    {
                        roll = MAX_JOYSTICK_VALUE;
                    }
                    break;
            }

            try
            {
                if (null != _instance)
                {
                    _instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                }
            }
            catch (Exception)
            { }
        }

        #endregion KeyDown Command

        #region KeyUp Command

        public ICommand KeyUpCommand { get; set; }
        private async Task KeyUpExecute(VirtualKey key)
        {
            switch (key)
            {
                case VirtualKey.W:
                case VirtualKey.S:
                    throttle = 0;
                    break;
                case VirtualKey.A:
                case VirtualKey.D:
                    yaw = 0;
                    break;
                case VirtualKey.I:
                case VirtualKey.K:
                    pitch = 0;
                    break;
                case VirtualKey.J:
                case VirtualKey.L:
                    roll = 0;
                    break;
            }

            try
            {
                if (null != _instance)
                {
                    _instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);
                }
            }
            catch (Exception)
            { }
        }

        #endregion KeyUp Command

        public ICommand ResetGimbalCommand { get; set; }
        private async Task ResetGimbalExecute()
        {

        }

        #endregion Commands

        #region Events

        private async void FlightControllerHandler_VelocityChanged(object sender, Velocity3D? value)
        {
            if (value.HasValue)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var unboxed = value.Value;

                    VelocityX = unboxed.x;
                    VelocityY = unboxed.y;
                    VelocityZ = unboxed.z;
                });
            }
        }

        private async void FlightControllerHandler_AttitudeChanged(object sender, Attitude? value)
        {
            if (value.HasValue)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var unboxed = value.Value;

                    Pitch = unboxed.pitch;
                    Roll = unboxed.roll;
                    Yaw = unboxed.yaw;
                });
            }
        }

        private void VideoFeed_VideoDataUpdated(VideoFeed sender, byte[] bytes)
        {
            _videoParser.PushVideoData(PRODUCT_ID, PRODUCT_INDEX, bytes, bytes.Length);
        }

        private void VideoParserVideoDataCallback(byte[] data, int width, int height)
        {
            // intended to be empty
        }

        private void CameraHandler_CameraTypeChanged(object sender, CameraTypeMsg? value)
        {
            var unboxed = value ?? new CameraTypeMsg { };

            if (null == _videoParser)
                return;

            switch (unboxed.value)
            {
                case CameraType.MAVIC_2_ZOOM:
                    _videoParser.SetCameraSensor(AircraftCameraType.Mavic2Zoom);
                    break;
                case CameraType.MAVIC_2_PRO:
                    _videoParser.SetCameraSensor(AircraftCameraType.Mavic2Pro);
                    break;
                default:
                    _videoParser.SetCameraSensor(AircraftCameraType.Others);
                    break;
            }
        }

        #endregion Events
    }
}
