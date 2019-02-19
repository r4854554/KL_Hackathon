namespace HDCircles.Hackathon.ViewModels
{
    using AprilTagsSharp;
    using Catel.Data;
    using Catel.MVVM;
    using DJI.WindowsSDK;
    using DJI.WindowsSDK.Components;
    using DJIVideoParser;
    using OpenCvSharp;
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows.Input;
    using Windows.ApplicationModel.Core;
    using Windows.Storage;
    using Windows.Storage.Streams;
    using Windows.System;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public class MainPageViewModel : ViewModelBase
    {

        #region Constants
        // Configuration constans
        private const double STATETIMER_UPDATE_FREQUENCE = 100; // 10Hz

        private const float MAX_JOYSTICK_VALUE = 0.5f;
        private const float MIN_JOYSTICK_VALUE = -0.5f;
        private const float JOYSTICK_THROTTLE_STEP = 0.02f;
        private const float JOYSTICK_ATTITUDE_STEP = 0.05f;

        private const float GIMBAL_ROTATE_STEP = 5f;

        private const int PRODUCT_ID = 0;
        private const int PRODUCT_INDEX = 0;
        private const string APP_KEY = "cb98b917674f98a483eb9228";

        private object bufferLock = new object();

        private byte[] frameBuffer;

        private int frameWidth;

        private int frameHeight;

        private Task aprilTagDetectionWorker;
        
        /// <summary>
        /// for joystick parameters
        /// </summary>
        private float throttle = 0.0f;
        private float pitch = 0.0f;
        private float roll = 0.0f;
        private float yaw = 0.0f;

        private float gimbalPitch = 0.0f;
        private float gimbalRoll = 0.0f;
        private float gimbalYaw = 0.0f;
        #endregion Constants

        #region Fields
        private readonly ICommandManager _commandManager;
        private Timer stateTimer;

        private bool _isInitialized;

        /// <summary>
        /// the instance of DJIVideoParser
        /// </summary>
        private Parser _videoParser;

        private DateTime processStart = DateTime.Now;
        private DateTime imageFpsStart = DateTime.Now;

        #endregion Fields


        #region Static Methods
        /// <summary>
        /// Async methods to run on UI thread.
        /// </summary>
        /// <param name="dispatcher"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
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

        /// <summary>
        /// the ui component
        /// </summary>
        public MainPage MainPage { get; set; }

        /// <remark>
        /// will be set at loaded event of the page
        /// </remark>
        public SwapChainPanel SwapChainPanel { get; set; }

        public static CoreDispatcher Dispatcher { get; set; }

        public bool EnableAprilTagDetection { get; set; }

        public string SdkAppKey
        {
            get => GetValue<string>(SdkAppKeyProperty);
            set => SetValue(SdkAppKeyProperty, value);
        }
        public static PropertyData SdkAppKeyProperty = RegisterProperty(nameof(SdkAppKey), typeof(string));

        public bool IsRegistered { get; set; }

        public double ImageFrameCount
        {
            get => GetValue<double>(ImageFrameCountProperty);
            set
            {
                SetValue(ImageFrameCountProperty, value);
            }
        }
        public static PropertyData ImageFrameCountProperty = RegisterProperty(nameof(ImageFrameCount), typeof(double));

        public double ImageFps
        {
            get => GetValue<double>(ImageFpsProperty);
            set
            {
                SetValue(ImageFpsProperty, value);
                RaisePropertyChanged(nameof(ImageFpsText));
            }
        }
        public static PropertyData ImageFpsProperty = RegisterProperty(nameof(ImageFps), typeof(double));

        public string ImageFpsText => $"fps: {ImageFps:0.0}";

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

        public int ChargeRemainingInPercent
        {
            get => GetValue<int>(ChargeRemainingInPercentProperty);
            set
            {
                SetValue(ChargeRemainingInPercentProperty, value);
                RaisePropertyChanged(nameof(ChargeRemainingInPercentText));
            }
        }
        public static PropertyData ChargeRemainingInPercentProperty = RegisterProperty(nameof(ChargeRemainingInPercent), typeof(int));

        public string ChargeRemainingInPercentText => $"{ChargeRemainingInPercent}%";

        public float Throttle
        {
            get => GetValue<float>(ThrottleProperty);
            set => SetValue(ThrottleProperty, value);
        }
        public static PropertyData ThrottleProperty = RegisterProperty(nameof(Throttle), typeof(float));

        public Attitude Attitude
        {
            get => GetValue<Attitude>(AttitudeProperty);
            set
            {
                SetValue(AttitudeProperty, value);
                RaisePropertyChanged(nameof(PitchText));
                RaisePropertyChanged(nameof(RollText));
                RaisePropertyChanged(nameof(YawText));
            }
        }
        public static PropertyData AttitudeProperty = RegisterProperty(nameof(Attitude), typeof(Attitude));

        public string PitchText => $"pitch: {Attitude.pitch}";
        public string RollText => $"roll: {Attitude.roll}";
        public string YawText => $"yaw: {Attitude.yaw}";

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

        public Attitude GimbalAttitude
        {
            get => GetValue<Attitude>(GimbalAttitudeProperty);
            set
            {
                SetValue(GimbalAttitudeProperty, value);
                RaisePropertyChanged(nameof(GimbalPitchText));
                RaisePropertyChanged(nameof(GimbalRollText));
                RaisePropertyChanged(nameof(GimbalYawText));
            }
        }

        public static PropertyData GimbalAttitudeProperty = RegisterProperty(nameof(GimbalAttitude), typeof(Attitude));

        public string GimbalPitchText => $"pitch: {GimbalAttitude.pitch}";

        public string GimbalRollText => $"roll: {GimbalAttitude.roll}";

        public string GimbalYawText => $"yaw: {GimbalAttitude.yaw}";

        public double Altitude
        {
            get => GetValue<double>(AltitudeProperty);
            set
            {
                SetValue(AltitudeProperty, value);
                RaisePropertyChanged(nameof(AltitudeText));
            }
        }
        public static PropertyData AltitudeProperty = RegisterProperty(nameof(Altitude), typeof(double));

        public string AltitudeText => $"height: {Altitude}";

        public Velocity3D Velocity
        {
            get => GetValue<Velocity3D>(VelocityProperty);
            set
            {
                SetValue(VelocityProperty, value);
                RaisePropertyChanged(nameof(VelocityXText));
                RaisePropertyChanged(nameof(VelocityYText));
                RaisePropertyChanged(nameof(VelocityZText));
            }
        }
        public static PropertyData VelocityProperty = RegisterProperty(nameof(Velocity), typeof(Velocity3D));

        public string VelocityXText => $"x: {Velocity.x}";
        public string VelocityYText => $"y: {Velocity.y}";
        public string VelocityZText => $"z: {Velocity.z}";

        public bool IsTimerEnabled
        {
            get => GetValue<bool>(IsTimerEnabledProperty);
            set => SetValue(IsTimerEnabledProperty, value);
        }
        public static PropertyData IsTimerEnabledProperty = RegisterProperty(nameof(IsTimerEnabled), typeof(bool));

        #endregion Properties

        #region Class Methods
        /// <summary>
        /// Constructor MainPageViewModel
        /// </summary>
        /// <param name="commandManager"></param>
        public MainPageViewModel(ICommandManager commandManager)
        {
            _commandManager = commandManager;
            ImageFrameCount = 0;
            SdkAppKey = APP_KEY;

            MainPageLoadedCommand = new TaskCommand(MainPageLoadedExecute);
            TakeOffCommand = new TaskCommand(TakeOffExecute);
            LandingCommand = new TaskCommand(LandingExecute);
            KeyDownCommand = new TaskCommand<VirtualKey>(KeyDownExecute);
            KeyUpCommand = new TaskCommand<VirtualKey>(KeyUpExecute);
            ResetGimbalCommand = new TaskCommand(ResetGimbalExecute);
            ResetJoystickCommand = new TaskCommand(ResetJoystickExecute);
            TestGimbalCommand = new TaskCommand(TestGimbalExecute);
            ToggleAprilTagDetectionCommand = new TaskCommand(ToggleAprilTagDetectionExecute);

            commandManager.RegisterCommand(Commands.MainPageLoaded, MainPageLoadedCommand, this);
            commandManager.RegisterCommand(Commands.KeyDown, KeyDownCommand, this);
            commandManager.RegisterCommand(Commands.KeyUp, KeyUpCommand, this);
        }

        #endregion Class Methods
        #region Auxlliary functions

        private bool IsEqual(float a, float b)
        {
            return Math.Abs(a - b) < float.Epsilon;
        }

        #endregion Auxlliary functions

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

        async Task UpdateAttitude()
        {
            var attitude = await GetFlightControllerHandler().GetAttitudeAsync();

            if (attitude.value.HasValue)
            {
                await CallOnUiThreadAsync(() =>
                {
                    Attitude = attitude.value.Value;
                });
            }
        }

        async Task UpdateGimbalAttitude()
        {
            var attitude = await GetGimbalHandler().GetGimbalAttitudeAsync();

            if (attitude.value.HasValue)
            {
                await CallOnUiThreadAsync(() =>
                {
                    GimbalAttitude = attitude.value.Value;
                });
            }
        }

        async Task UpdateAltitude()
        {
            var altitude = await GetFlightControllerHandler().GetAltitudeAsync();

            if (altitude.value.HasValue)
            {
                await CallOnUiThreadAsync(() =>
                {
                    Altitude = altitude.value.Value.value;
                });
            }
        }

        async Task UpdateVelocity()
        {
            var velocity = await GetFlightControllerHandler().GetVelocityAsync();

            if (velocity.value.HasValue)
            {
                await CallOnUiThreadAsync(() =>
                {
                    Velocity = velocity.value.Value;
                });
            }
        }

        async Task UpdateVideoFeedFps()
        {
            await CallOnUiThreadAsync(() =>
            {
                RaisePropertyChanged(nameof(ImageFpsText));
            });
        }

        async Task UpdateChargeRemaining()
        {
            var chargeRemaining = await GetBatteryHandler().GetChargeRemainingInPercentAsync();

            if (chargeRemaining.value.HasValue)
            {
                await CallOnUiThreadAsync(() =>
                {
                    ChargeRemainingInPercent = chargeRemaining.value.Value.value;
                });
            }
        }

        int[] VideoParserVideoAssitantInfoParserHandler(byte[] data)
        {
            return DJISDKManager.Instance.VideoFeeder.ParseAssitantDecodingInfo(PRODUCT_INDEX, data);
        }

        void CreateAprilTagDetectionWorker()
        {
            if (null == aprilTagDetectionWorker)
            {
                aprilTagDetectionWorker = new Task(DetectAprilTag);
            }
        }

        async void DetectAprilTag()
        {

            
        }

        #region Components Handler

        FlightControllerHandler GetFlightControllerHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        CameraHandler GetCameraHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetCameraHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        GimbalHandler GetGimbalHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetGimbalHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        BatteryHandler GetBatteryHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetBatteryHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        #endregion Components Handler

        #region Commands

        #region MainPageLoaded Command

        public ICommand MainPageLoadedCommand { get; set; }

        private async Task MainPageLoadedExecute()
        {
            var sdkManager = DJISDKManager.Instance;

            sdkManager.SDKRegistrationStateChanged += DJKSDKManager_SDKRegistrationStateChanged;
            
            if(sdkManager.appActivationState != AppActivationState.ACTIVATED)
                sdkManager.RegisterApp(SdkAppKey);

            await Task.Delay(100);
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

        #endregion MainPageLoaded Command

        #region TakeOffCommand

        public ICommand TakeOffCommand { get; set; }
        private async Task TakeOffExecute()
        {
            var fcHandler = GetFlightControllerHandler();

            var result = await fcHandler.StartTakeoffAsync();

            await UpdateCurrentState("take off executed: " + result);
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
                case VirtualKey.T:
                    gimbalPitch = GIMBAL_ROTATE_STEP;
                    break;
                case VirtualKey.G:
                    gimbalPitch = -GIMBAL_ROTATE_STEP;
                    break;
                case VirtualKey.F:
                    gimbalRoll = GIMBAL_ROTATE_STEP;
                    break;
                case VirtualKey.H:
                    gimbalRoll = -GIMBAL_ROTATE_STEP;
                    break;
                case VirtualKey.R:
                    gimbalYaw = GIMBAL_ROTATE_STEP;
                    break;
                case VirtualKey.Y:
                    gimbalYaw = -GIMBAL_ROTATE_STEP;
                    break;
            }

            try
            {
                if (null != DJISDKManager.Instance)
                {
                    DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

                    var result = await GetGimbalHandler().RotateByAngleAsync(new GimbalAngleRotation
                    {
                        mode = GimbalAngleRotationMode.RELATIVE_ANGLE,
                        pitch = gimbalPitch,
                        roll = gimbalRoll,
                        yaw = gimbalYaw,
                        pitchIgnored = false,
                        rollIgnored = true,
                        yawIgnored = true,
                        duration = 1
                    });

                    await UpdateCurrentState("RotateGimbalByAngle: " + result.ToString());
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
            }
        }

        #endregion KeyDown Command

        #region KeyUp Command

        public ICommand KeyUpCommand { get; set; }
        private async Task KeyUpExecute(VirtualKey key)
        {
            var updateGimbal = false;

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
                case VirtualKey.T:
                case VirtualKey.G:
                    gimbalPitch = 0f;
                    break;
                case VirtualKey.F:
                case VirtualKey.H:
                    gimbalRoll = 0f;
                    break;
                case VirtualKey.R:
                case VirtualKey.Y:
                    gimbalYaw = 0f;
                    break;
            }

            try
            {
                if (null != DJISDKManager.Instance)
                {
                    DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

                    await GetGimbalHandler().RotateByAngleAsync(new GimbalAngleRotation
                    {
                        mode = GimbalAngleRotationMode.RELATIVE_ANGLE,
                        pitch = gimbalPitch,
                        roll = gimbalRoll,
                        yaw = gimbalYaw,
                        pitchIgnored = false,
                        rollIgnored = false,
                        yawIgnored = false,
                        duration = 0.3
                    });
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(ex.ToString());
#endif
            }
        }

        #endregion KeyUp Command

        public ICommand ResetGimbalCommand { get; set; }
        private async Task ResetGimbalExecute()
        {
            var gimbalHandler = GetGimbalHandler();
            var resetMsg = new GimbalResetCommandMsg
            {
                value = GimbalResetCommand.TOGGLE_PITCH
            };

            await gimbalHandler.ResetGimbalAsync(resetMsg);
        }

        public ICommand ResetJoystickCommand { get; set; }
        private async Task ResetJoystickExecute()
        {
            if (null != DJISDKManager.Instance)
                DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(0f, 0f, 0f, 0f);

            await Task.Delay(20);
        }

        public ICommand TestGimbalCommand { get; set; }
        private async Task TestGimbalExecute()
        {
            var gimbalHandler = GetGimbalHandler();
            var param = new GimbalSpeedRotation
            {
                pitch = -5f,
                roll = 0,
                yaw = 0,
            };

            var result = await gimbalHandler.RotateBySpeedAsync(param);
        }

        public ICommand ToggleAprilTagDetectionCommand { get; set; }
        private async Task ToggleAprilTagDetectionExecute()
        {
            //EnableAprilTagDetection = !EnableAprilTagDetection;

            frameWidth = 1280;
            frameHeight = 960;

            byte[] buffer = new byte[frameWidth * frameHeight * 3];

            try
            {
                //lock (bufferLock)
                //{
                //    if (buffer.Length != frameBuffer.Length)
                //    {
                //        Array.Resize(ref buffer, frameBuffer.Length);

                //        frameBuffer.CopyTo(buffer.AsBuffer());
                //    }                    
                //}

                //var rgba = new Mat(frameHeight, frameWidth, MatType.CV_8UC4, buffer);
                //var rgb = rgba.CvtColor(ColorConversionCodes.RGBA2RGB);
                //var bytes = rgb.Width * 3 * rgb.Height;
                //var rgbBytes = new byte[bytes];

                //Marshal.Copy(rgb.Data, rgbBytes, 0, bytes);

                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.GetFileAsync("sample.bin");
                var stream = await file.OpenAsync(FileAccessMode.Read);
                var buf = buffer.AsBuffer();

                await stream.ReadAsync(buf, (uint)(frameWidth * frameHeight * 3), InputStreamOptions.None);

                var mat = new Mat(frameHeight, frameWidth, MatType.CV_8UC3, buffer);
                var ap = new AprilTag("canny", false, "tag25h9", 0.8, 1, 400);

                var result = ap.detect(mat);

                //var detections = _videoParser.DetectAprilTag(buffer, frameWidth, frameHeight, AprilTagFamily.Tag25h9);
            }
            catch (Exception e)
            {
                
            }            
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
                    Attitude = value.Value;
                });
            }
        }

        private void VideoFeed_VideoDataUpdated(VideoFeed sender, byte[] bytes)
        {
            _videoParser.PushVideoData(PRODUCT_ID, PRODUCT_INDEX, bytes, bytes.Length);
        }

        private async void VideoParserVideoDataCallback(byte[] data, int width, int height)
        {
            lock (bufferLock)
            {
                if (null == frameBuffer)
                {
                    frameBuffer = data;
                }
                else
                {
                    if (data.Length != frameBuffer.Length)
                    {
                        Array.Resize(ref frameBuffer, data.Length);
                    }

                    data.CopyTo(frameBuffer.AsBuffer());
                    frameWidth = width;
                    frameHeight = height;
                }
            }

            await CallOnUiThreadAsync(async () =>
            {
                var now = DateTime.Now;
                var elapsed = now - imageFpsStart;

                if (elapsed >= TimeSpan.FromSeconds(1))
                {
                    var n = (imageFpsStart - processStart).TotalSeconds;

                    ImageFps = (ImageFps * n / (n + 1)) + (ImageFrameCount / (n + 1));
                    imageFpsStart = now;
                    ImageFrameCount = 0;
                }

                ImageFrameCount += 1;
            });
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

        private async void GimbalHandler_GimbalAttitudeChanged(object sender, Attitude? value)
        {
            if (value.HasValue)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    GimbalAttitude = value.Value;
                });
            }
        }

        private async void StateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            await UpdateAltitude();
            await UpdateAttitude();
            await UpdateVelocity();
            await UpdateGimbalAttitude();
            await UpdateVideoFeedFps();
            await UpdateChargeRemaining();
        }

        private void MainPage_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var key = e.Key;

            KeyUpCommand.Execute(key);
        }

        private void MainPage_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var key = e.Key;
             
        }

        #endregion Events
    }
}
