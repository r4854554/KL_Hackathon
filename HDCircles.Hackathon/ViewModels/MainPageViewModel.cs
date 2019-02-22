namespace HDCircles.Hackathon.ViewModels
{
    using Catel.Data;
    using Catel.MVVM;
    using DJI.WindowsSDK;
    using DJI.WindowsSDK.Components;
    using DJIVideoParser;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Timers;
    using System.Windows.Input;
    using Windows.ApplicationModel.Core;
    using Windows.System;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;
    using Dynamsoft.Barcode;
    using System.ComponentModel;
    // for LiveCharts
    using LiveCharts;
    using LiveCharts.Uwp;
    using System.Drawing;
    using System.Text.RegularExpressions;
    using System.Collections.Generic;

    using Timer = System.Timers.Timer;
    using OpenCvSharp;
    using System.Runtime.InteropServices;
    using HDCircles.Hackathon.Services;
    using Windows.UI.Xaml.Media.Imaging;
    using System.Linq;

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

        private const int PRODUCT_ID    = 0;
        private const int PRODUCT_INDEX = 0;
        private const string APP_KEY = "cb98b917674f98a483eb9228";
        private const string Dynamsoft_App_Key = "t0068NQAAALjRYgQPyFU9w77kwoOtA6C+n34MIhvItkLV0+LcUVEef9fN3hiwyNTlUB8Lg+2XYci3vEYVCc4mdcuhAs7mVMg=";

        private bool _enableQrcodeDetection;

        private long qrcodeDetectFrequence = 250L;

        private object frameLock = new object();

        public WriteableBitmap LiveFrameSource
        {
            get => GetValue<WriteableBitmap>(LiveFrameSourceProperty);
            set => SetValue(LiveFrameSourceProperty, value);
        }
        public static readonly PropertyData LiveFrameSourceProperty = RegisterProperty(nameof(LiveFrameSource), typeof(WriteableBitmap));
        
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
        private System.Timers.Timer stateTimer;

        private bool _isInitialized;        

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

        public static CoreDispatcher Dispatcher { get; set; }

        public string SdkAppKey
        {
            get => GetValue<string>(SdkAppKeyProperty);
            set => SetValue(SdkAppKeyProperty, value);
        }
        public static PropertyData SdkAppKeyProperty = RegisterProperty(nameof(SdkAppKey), typeof(string));

        public bool IsRegistered { get; set; }

        //public double ImageFrameCount
        //{
        //    get => GetValue<double>(ImageFrameCountProperty);
        //    set
        //    {
        //        SetValue(ImageFrameCountProperty, value);
        //    }
        //}
        public int ImageFrameCount
        {
            get => GetValue<int>(ImageFrameCountProperty);
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
        public string DecodeText {
            get => GetValue<string>(DecodeTextProperty);
            set
            {
                SetValue(DecodeTextProperty, value);
                RaisePropertyChanged(nameof(DecodeText));
            }
        }
        
        public static PropertyData DecodeTextProperty = RegisterProperty(nameof(DecodeText),typeof(string));

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
            Debug.WriteLine("Thread id in attitude: " + Thread.CurrentThread.ManagedThreadId);
            var attitude = await GetGimbalHandler().GetGimbalAttitudeAsync();

            if (attitude.value.HasValue)
            {
                await CallOnUiThreadAsync(() =>
                {
                    Debug.WriteLine("Thread id in attitude ui: " + Thread.CurrentThread.ManagedThreadId);
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

        #region Components Handler


        WiFiHandler GetWifiHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetWiFiHandler(PRODUCT_ID, PRODUCT_INDEX);
        }


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

            if (sdkManager.SDKRegistrationResultCode == SDKError.NO_ERROR)
                DJKSDKManager_SDKRegistrationStateChanged(SDKRegistrationState.Succeeded, SDKError.NO_ERROR);

            await Task.Delay(100);
        }

        private async void DJKSDKManager_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            Debug.WriteLine("Info:DJKSDKManager_SDKRegistrationStateChanged:DJKSDKManager_SDKRegistrationStateChanged");
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

                QrcodeDetector.Instance.QrcodeDetected += Instance_QrcodeDetected;

                var fcHandler = GetFlightControllerHandler();
                var cameraHandler = GetCameraHandler();
                var gimbalHandler = GetGimbalHandler();
                var wifiHandler = GetWifiHandler();

                System.Diagnostics.Debug.WriteLine("WifiHandler debug");
                var connection = await wifiHandler.GetConnectionAsync();
                System.Diagnostics.Debug.WriteLine("WifiHandler debug done {0}", connection.value.HasValue);
                System.Diagnostics.Debug.WriteLine("WifiHandler debug done {0}", connection.error.ToString());
                if (connection.value.HasValue)
                {
                    System.Diagnostics.Debug.WriteLine("Connection status:{0}", connection.value.Value); 
                }

                _enableQrcodeDetection = true;
                _isInitialized = true;

                Thread.Sleep(300);
            });
        }

        private async void Instance_QrcodeDetected(QrcodeDetection qrcode)
        {
            if (qrcode.Results == null || qrcode.Results.Length == 0)
                return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (frameLock)
                {
                    var frame = qrcode.Frame;
                    var bgraData = new byte[frame.Data.Length];

                    var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);
                    var bgraMat = new Mat();

                    Cv2.CvtColor(srcMat, bgraMat, ColorConversionCodes.RGBA2BGRA);

                    Marshal.Copy(bgraMat.Data, bgraData, 0, frame.Data.Length);

                    if (LiveFrameSource == null || LiveFrameSource.PixelWidth != frame.Width || LiveFrameSource.PixelHeight != frame.Height)
                    {
                        LiveFrameSource = new WriteableBitmap(frame.Width, frame.Height);
                    }

                    bgraData.AsBuffer().CopyTo(LiveFrameSource.PixelBuffer);

                    LiveFrameSource.Invalidate();

                    DecodeText = string.Join("\n", qrcode.Results.Select(x => x.BarcodeText).ToList());
                }
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
            
            await ResetJoystickExecute();
            await fcHandler.StartAutoLandingAsync();
            var  isFlyingResult = await fcHandler.GetIsFlyingAsync();
            if (isFlyingResult.value.HasValue)
            {
                var isFlying = isFlyingResult.value.Value.value;
                while (isFlying)
                {
                    await fcHandler.StartAutoLandingAsync();
                    var confirmationNeeded = await fcHandler.GetIsLandingConfirmationNeededAsync();
                    if (confirmationNeeded.value.HasValue)
                    {
                        await fcHandler.ConfirmLandingAsync();
                    }
                    isFlyingResult = await fcHandler.GetIsFlyingAsync();
                    if (isFlyingResult.value.HasValue) { isFlying = isFlyingResult.value.Value.value; }

                }
            }
            

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
                    
                    //var result = await GetGimbalHandler().RotateByAngleAsync(new GimbalAngleRotation
                    //{
                    //    mode = GimbalAngleRotationMode.RELATIVE_ANGLE,
                    //    pitch = gimbalPitch,
                    //    roll = gimbalRoll,
                    //    yaw = gimbalYaw,
                    //    pitchIgnored = false,
                    //    rollIgnored = true,
                    //    yawIgnored = true,
                    //    duration = 1
                    //});

                    await UpdateCurrentState("Virtual Joystick Update! " );
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

        private async Task Decode_QRcode(byte[] data,int width,int height) {

            if (null == data || data.Length == 0)
            {
                return;
            }

            try
            {
                var mat = new Mat(height, width, MatType.CV_8UC4, data);
                var gray = new Mat();

                Cv2.CvtColor(mat, gray, ColorConversionCodes.RGBA2GRAY);

                var stride = gray.Cols * gray.ElemSize();
                var length = gray.Rows * stride;
                var buffer = new byte[length];

                Marshal.Copy(gray.Data,  buffer, 0, length);

                //byte[] image = new byte[width * height * 4];
                //int nWidth = width, nHeight = height;
                //int count = 0;
                //int size = data.Length / 4;
                //for (int j = 0; j < size; j++)
                //{
                //    for (int i = 3; i >= 0; i--)
                //    {
                //        image[count + i] = data[count + (3 - i)];
                //    }
                //    count += 4;
                //}
                // ToFix: it crashes sometime, saying that 
                var br = new BarcodeReader();
                br.LicenseKeys = Dynamsoft_App_Key;
                TextResult[] result = br.DecodeBuffer(
                    buffer, 
                    width, 
                    height, 
                    stride, 
                    EnumImagePixelFormat.IPF_GrayScaled,
                    "");
                
                await CallOnUiThreadAsync(() =>
                {
                    if (result.Length > 0)
                    {
                        DecodeText = "";
                        for (int i = 0; i < result.Length; i++)
                        {
                            DecodeText += result[i].BarcodeText + "\n";
                        }
                    }
                    result = null;
                });
            }
            catch (System.AccessViolationException)
            {

            }
            data = null;
        }

        private void FurtherProcess(TextResult[] data, LocalizationResult[] pos) {
            Regex LocationTagReg= new Regex(@"^[A-Z]{2}[0-9]{6}$");
            Regex CartonTagReg = new Regex(@"^[0-9]{6}$");
            List<int> LocationTagPos = new List<int>();
            List<int> CartonTagPos = new List<int>();
            int DataSize = data.Length;
            // Classify Tag
            for(int i =0;i<DataSize;i++) {
                string temp = data[i].BarcodeText;
                if (LocationTagReg.Match(temp).Success)
                {
                    LocationTagPos.Add(i);
                }
                else if (CartonTagReg.Match(temp).Success)
                {
                    CartonTagPos.Add(i);
                }
            }
        }

        private async void StateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Debug.WriteLine("Thread id in timer: " + Thread.CurrentThread.ManagedThreadId);

            await UpdateAltitude();
            await UpdateAttitude();
            await UpdateVelocity();
            await UpdateGimbalAttitude();
            await UpdateVideoFeedFps();
            //await UpdateChargeRemaining();
        }

        private void MainPage_KeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var key = e.Key;

            KeyUpCommand.Execute(key);
        }

        private void MainPage_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            var key = e.Key;

            KeyDownCommand.Execute(key);
        }

        #endregion Events
    }
}
