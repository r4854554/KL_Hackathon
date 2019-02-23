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
    using Windows.Media;

    using CustomVision;
    using Windows.Storage;
    using Windows.UI.Xaml.Media;
    using Windows.UI.Xaml;
    using Windows.UI.Text;
    using HDCircles.Hackathon.Services;
    using Windows.UI.Xaml.Media.Imaging;
    using System.Linq;

    using Windows.Graphics.Imaging;
    using HDCircles.Hackathon.Views;

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
        private BarcodeReader br;
        private Regex CartonTagReg = new Regex(@"^[0-9]{6}$");
        public List<string> ResultLocation = new List<string>();
        public List<List<string> > ResultCarton = new List<List<string> >();
        private BackgroundWorker backgroundWorker;

        private bool _enableQrcodeDetection;

        private long qrcodeDetectFrequence = 250L;


        private object frameLock = new object();

        private object indexLock = new object();

        private double _averrageQrIndex;
        public double AverrageQrIndex
        {
            get
            {
                lock (indexLock)
                {
                    return _averrageQrIndex;
                }
            }
            set
            {
                lock (indexLock)
                {
                    if (value != 0) { _averrageQrIndex = value; }
                    
                }
            }
        }


        //private double _averrageQrIndexLateral;
        //public double AverrageQrIndexLateral
        //{
        //    get
        //    {
        //        lock (indexLock)
        //        {
        //            return _averrageQrIndexLateral;
        //        }
        //    }
        //    set
        //    {
        //        lock (indexLock)
        //        {
        //            _averrageQrIndexLateral = value;
        //        }
        //    }
        //}

        public double AverrageQrCount { get; set; } = 0;

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
        //private ObjectDetection objectDetection;
        #endregion Constants

        #region Fields
        private readonly ICommandManager _commandManager;
        private System.Timers.Timer stateTimer;

        private bool _isInitialized;        

        private DateTime processStart = DateTime.Now;
        private DateTime imageFpsStart = DateTime.Now;
        private Regex LocationTagReg = new Regex(@"^ [A-Z] [0-9]{2} [0-9]{2} [0-9] [0-9]{2}$");

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

            //label here
            //List<String> labels = new List<String> { "Box", "Nobox" };
            //objectDetection = new ObjectDetection(labels, 20, 0.8F, 0.45F);
            //init_onnx();
            Thread.Sleep(100);
        }
        
        //private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        //{
        //    var watch = Stopwatch.StartNew();
        //    var elapsed = 0L;
        //    var sleepTime = 0;

        //    for (; ; )
        //    {
        //        watch.Restart();

        //        if (!_enableQrcodeDetection || !IsRegistered || !_isInitialized)
        //        {
        //            watch.Stop();

        //            elapsed = watch.ElapsedMilliseconds;
        //            sleepTime = (int)Math.Max(qrcodeDetectFrequence - elapsed, 0);

        //            Thread.Sleep(sleepTime);
        //            continue;
        //        }

        //        DoDetection().Wait();

        //        watch.Stop();

        //        elapsed = watch.ElapsedMilliseconds;
        //        sleepTime = (int)Math.Max(qrcodeDetectFrequence - elapsed, 0);

        //        Thread.Sleep(sleepTime);
        //    }
        //}

        //private async Task DoDetection()
        //{
        //    var frame = Drone.Instance.GetLiveFrame();
        //    int check = await ScanFrame(frame.Data, frame.Width, frame.Height);
        //}

        //private async Task<int> ScanFrame(byte[] data,int height, int width) {
        //    // resize image to (416,416)
        //    var mat = new Mat(height, width, MatType.CV_8UC4, data);
        //    Mat resizemat = new Mat();
        //    resizemat = mat.Clone();
        //    Cv2.CvtColor(mat, mat, ColorConversionCodes.RGBA2GRAY);
        //    resizemat.Resize(416, 416);
        //    // Mat -> softwarebitmap
        //    var length = resizemat.Rows * resizemat.Cols * resizemat.ElemSize();
        //    var buffer = new byte[length];
        //    Marshal.Copy(mat.Data, buffer, 0, length);
        //    var bm = SoftwareBitmap.CreateCopyFromBuffer(buffer.AsBuffer(), BitmapPixelFormat.Rgba8, width, height);
        //    try
        //    {
        //        IList<PredictionModel> outputlist = await objectDetection.PredictImageAsync(VideoFrame.CreateWithSoftwareBitmap(bm));

        //        foreach (var output in outputlist) {
        //            // chop origin image  
        //            Mat chop = ChopOutData(mat, output.BoundingBox, height, width);
        //            var chop_image_length = chop.Rows * chop.Cols * chop.ElemSize();
        //            var chop_image_buffer = new byte[length];
        //            Marshal.Copy(chop.Data, buffer, 0, length);
        //            br = new BarcodeReader();
        //            br.LicenseKeys = Dynamsoft_App_Key;
        //            TextResult[] result =
        //                br.DecodeBuffer(buffer, width, height, chop.Cols * chop.ElemSize(), EnumImagePixelFormat.IPF_GrayScaled, "");
                    
        //            switch (output.TagName) {
  
        //                case "Box":
        //                    if (result.Length < 2)
        //                        return -1;
        //                    string LocationTag = "";
        //                    List<string> CartonTag = new List<string>();
        //                    foreach (var pick in result)
        //                    {
        //                        if (LocationTagReg.Match(pick.BarcodeText).Success) {
        //                            LocationTag = pick.BarcodeText;
        //                        }else if (CartonTagReg.Match(pick.BarcodeText).Success)
        //                        {
        //                            CartonTag.Add(pick.BarcodeText);
        //                        }
        //                        int index = ResultLocation.IndexOf(LocationTag);
        //                        if ( index == -1)
        //                        {
        //                            ResultLocation.Add(LocationTag);
        //                            ResultCarton.Add(CartonTag);
        //                        }
        //                        else
        //                        {
        //                            ResultCarton[index] = CartonTag;
        //                        }
        //                    }
        //                    break;
        //                case "NoBox":
        //                    if (result.Length !=  1)
        //                        return -1;
        //                    int i_dx = ResultLocation.IndexOf(result[0].BarcodeText);
        //                    if (i_dx == -1) {
        //                        ResultLocation.Add(result[0].BarcodeText);
        //                        ResultCarton.Add(new List<string>());
        //                    }
        //                    else
        //                    {
        //                        ResultCarton[i_dx] = new List<string>();
        //                    }
        //                    break;                           
        //            }
        //            UpdateDecodeText();
        //            //LocalizationResult[] pos = br.GetAllLocalizationResults();
        //            // drawing result 
        //            // width & height should be the actualwidth and actualheight of the canvas 
        //            //UpdateResult(output, width, height);
        //            //switch (output.TagName)
        //            //{
        //            //    case "LocationTag":                            
        //            //        //do something
        //            //        break;
        //            //    case "box":
        //            //        //do something 
        //            //        break;
        //            //    case "nobox":
        //            //        //do something
        //            //        break;
        //            //}
        //            // Proccess the result

        //        }
        //    }
        //    catch
        //    {

        //    }
        //    return 0;
        //}

        private Mat ChopOutData(Mat image, BoundingBox box, int height, int width) {
            double x = (double)Math.Max(box.Left, 0);
            double y = (double)Math.Max(box.Top, 0);
            double w = (double)Math.Min(1 - x, box.Width);
            double h = (double)Math.Min(1 - y, box.Height);

            x = width * x;
            y = height * y;
            w = width * w;
            h = height * h;

            return new Mat(image,new Rect((int)x,(int)y,(int)w,(int)h)).Clone();
        }

        private async void UpdateDecodeText()
        {
            await CallOnUiThreadAsync(() =>
            {
                DecodeText = "";
                int index = ResultLocation.Count;
                for(int i = 0; i < index; i++)
                {
                    DecodeText += ResultLocation[i] + " -> ";
                    foreach(string carton in ResultCarton[i])
                    {
                        DecodeText += carton + " ";
                    }
                    DecodeText += "\n";
                }
            });
        }

        //private async void init_onnx()
        //{

        //    StorageFile file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///AI/model.onnx"));
        //    await objectDetection.Init(file);
        //}

        private void UpdateResult(PredictionModel output,int width, int height)
        {
            //Canvas Drawable = MainPage.GetCanvas();
            //Drawable.Children.Clear();
            //SolidColorBrush _fillBrush = new SolidColorBrush(Windows.UI.Colors.Transparent);
            //SolidColorBrush _lineBrushRed = new SolidColorBrush(Windows.UI.Colors.Red);
            //SolidColorBrush _lineBrushGreen = new SolidColorBrush(Windows.UI.Colors.Green);
            //SolidColorBrush _lineBrushBlue = new SolidColorBrush(Windows.UI.Colors.Blue);
            //SolidColorBrush color = new SolidColorBrush(Windows.UI.Colors.Blue);

            //var box = output.BoundingBox;

            //double x = (double)Math.Max(box.Left, 0);
            //double y = (double)Math.Max(box.Top, 0);
            //double w = (double)Math.Min(1 - x, box.Width);
            //double h = (double)Math.Min(1 - y, box.Height);

            //x = width * x;
            //y = height * y;
            //w = width * w;
            //h = height * h;

            //switch (output.TagName)
            //{
            //    case "LocationTag":
            //        //do something
            //        color = _lineBrushBlue;
            //        break;
            //    case "box":
            //        //do something 
            //        color = _lineBrushGreen;
            //        break;
            //    case "nobox":
            //        //do something
            //        color = _lineBrushRed;
            //        break;
            //}
            //var r = new Windows.UI.Xaml.Shapes.Rectangle
            //{
            //    Tag = box,
            //    Width = w,
            //    Height = h,
            //    Fill = _fillBrush,
            //    Stroke = color,
            //    StrokeThickness = 2.0,
            //    Margin = new Thickness(x, y, 0, 0)
            //};

            //var tb = new TextBlock
            //{
            //    Margin = new Thickness(x + 4, y + 4, 0, 0),
            //    Text = $"{output.TagName} ({Math.Round(output.Probability, 4)})",
            //    FontWeight = FontWeights.Bold,
            //    Width = 126,
            //    Height = 21,
            //    HorizontalTextAlignment = TextAlignment.Center
            //};

            //var textBack = new Windows.UI.Xaml.Shapes.Rectangle
            //{
            //    Width = 134,
            //    Height = 29,
            //    Fill = _fillBrush,
            //    Margin = new Thickness(x, y, 0, 0)
            //};

            //Drawable.Children.Add(textBack);
            //Drawable.Children.Add(tb);
            //Drawable.Children.Add(r);
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

                //QrcodeDetector.Instance.QrcodeDetected += Instance_QrcodeDetected;
                PosController.Instance.PoseUpdated += Instance_PoseUpdated;

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

        private async void Instance_PoseUpdated(ApriltagPoseEstimation pose)
        {
            if (pose.DetectResults == null || pose.DetectResults.Length == 0)
                return;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (frameLock)
                {
                    var frame = pose.Frame;
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
                    AverrageQrIndex = 0;
                    AverrageQrCount = 0;
                    foreach (var result in pose.DetectResults)
                    {
                        //Debug.Print($"{ result.BarcodeText}\n");

                        var locReg = new Regex(@"^[A-Z]{2}[0-9]{1}[0-9]{2}[0-9][0-9]{2}$");
                        
                        
                        // only care about location tag
                        if (locReg.Match(result.BarcodeText).Success)
                        {
                            var LocationTag = result.BarcodeText;
                            var pos = Regex.Match(result.BarcodeText, @"(.{2})\s*$"); ;
                            var num = Int32.Parse(pos.Value);
                            

                            AverrageQrIndex = AverrageQrIndex + num;
                            AverrageQrCount += 1;
                            
                        }
                        Debug.Print($"LocationTag: {ImageFrameCount} - { AverrageQrIndex}, {AverrageQrCount}, {AverrageQrIndex / AverrageQrCount} \n");
                        
                    }

                    //#region by chriss. for debugging and tracing the current qrcode coordination
                    //var h = new Heuristic();
                    //if (results.Length >= 3)
                    //{
                    //    h.LRHeuristic(results[0].BarcodeText, results[1].BarcodeText, results[2].BarcodeText);
                    //}
                    //#endregion

                    //DecodeText = qrcode.Results;
                }
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

                    Marshal.Copy(srcMat.Data, bgraData, 0, frame.Data.Length);

                    if (LiveFrameSource == null || LiveFrameSource.PixelWidth != frame.Width || LiveFrameSource.PixelHeight != frame.Height)
                    {
                        LiveFrameSource = new WriteableBitmap(frame.Width, frame.Height);
                    }

                    bgraData.AsBuffer().CopyTo(LiveFrameSource.PixelBuffer);

                    LiveFrameSource.Invalidate();

                    DecodeText = qrcode.Results;
                }
            });
        }

        #endregion MainPageLoaded Command

        #region TakeOffCommand

        public ICommand TakeOffCommand { get; set; }
        private async Task TakeOffExecute()
        {
            

            
            var result = await Drone.Instance.TakeOff(); 
            await UpdateCurrentState("take off executed: " + result);
        }

        #endregion TakeOffCommand

        #region Landing Command

        public ICommand LandingCommand { get; set; }
        private async Task LandingExecute()
        {
            Drone.Instance.EmergencyLanding();
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
                case VirtualKey.Up:
                    Debug.WriteLine("Info:KeyDownExecute:Up");
                    //FlightStacks.Instance._positionController.SetAltitudeStepCommand(0.05);
                    FlightStacks.Instance._positionController.AltitudeSetpoint = 1.5;
                    break;
                case VirtualKey.Down:
                    Debug.WriteLine("Info:KeyDownExecute:Up");
                    //FlightStacks.Instance._positionController.SetAltitudeStepCommand(-0.05);
                    FlightStacks.Instance._positionController.AltitudeSetpoint = 0.4;
                    break;
                case VirtualKey.PageDown:
                    //Debug.WriteLine("Info:KeyDownExecute:Up");
                    //FlightStacks.Instance._positionController.SetYawStepCommand(0.5);
                    FlightStacks.Instance._positionController.YawSetpoint = 10;
                    break;
                case VirtualKey.PageUp:
                    //Debug.WriteLine("Info:KeyDownExecute:Up");
                    //FlightStacks.Instance._positionController.SetYawStepCommand(-0.5);
                    FlightStacks.Instance._positionController.YawSetpoint = -170;
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
