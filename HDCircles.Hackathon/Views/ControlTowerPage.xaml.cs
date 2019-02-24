namespace HDCircles.Hackathon.Views
{
    using DJI.WindowsSDK;
    using HDCircles.Hackathon.Services;
    using OpenCvSharp;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.Storage;
    using Windows.UI.Core;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;

    public sealed partial class ControlTowerPage : Page
    {
        struct InputArgs
        {
            public float Yaw { get; }
            public float Altitude { get; }
            public float RelativeX { get; }
            public float RelativeY { get; }
            public int PositionId { get; }
            public bool RightSide { get; }

            public InputArgs(float yaw, float altitude, float relativeX, float relativeY, int positionId, bool rightSide)
            {
                Yaw = yaw;
                Altitude = altitude;
                RelativeX = relativeX;
                RelativeY = relativeY;
                PositionId = positionId;
                RightSide = rightSide;
            }
        }

        private object _emergencyLock = new object();

        private object _cmdLock = new object();

        private object _frameLock = new object();

        private bool _isLanding;

        private bool _isTakingOff;

        private bool _isAutoPilot;

        private WriteableBitmap LiveFeedSource { get; set; }

        private Commander Commander => Commander.Instance;

        public ControlTowerPage()
        {
            this.InitializeComponent();

            Loaded += ControlTowerPage_Loaded;
            Unloaded += ControlTowerPage_Unloaded;
        }

        private void ControlTowerPage_Unloaded(object sender, RoutedEventArgs e)
        {

        }

        private void ControlTowerPage_Loaded(object sender, RoutedEventArgs e)
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            if (DJISDKManager.Instance.SDKRegistrationResultCode == SDKError.NO_ERROR)
            {
                Instance_SDKRegistrationStateChanged(SDKRegistrationState.Succeeded, SDKError.NO_ERROR);
            }
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            var isRegistered = state == SDKRegistrationState.Succeeded && errorCode == SDKError.NO_ERROR;

            if (isRegistered)
            {
                PosController.Instance.PoseUpdated += Instance_PoseUpdated;
                QrcodeDetector.Instance.QrcodeDetected += Instance_QrcodeDetected;
                Commander.MissionUpdated += Commander_MissionUpdated;
            }
        }

        private async void Instance_QrcodeDetected(QrcodeDetection qrcode)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (_frameLock)
                {
                    var frame = qrcode.Frame;
                    var bgraData = new byte[frame.Data.Length];

                    var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);
                    var bgraMat = new Mat();

                    Cv2.CvtColor(srcMat, bgraMat, ColorConversionCodes.RGBA2BGRA);

                    Marshal.Copy(srcMat.Data, bgraData, 0, frame.Data.Length);

                    if (LiveFeedSource == null || LiveFeedSource.PixelWidth != frame.Width || LiveFeedSource.PixelHeight != frame.Height)
                    {
                        LiveFeedSource = new WriteableBitmap(frame.Width, frame.Height);
                        LiveFeedImage.Source = LiveFeedSource;
                    }

                    bgraData.AsBuffer().CopyTo(LiveFeedSource.PixelBuffer);

                    LiveFeedSource.Invalidate();

                    // Display Decode Result
                    DecodeText.Text = qrcode.Results;
                }
            });
        }

            private async void Commander_MissionUpdated(CommanderState state)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var currentMission = state.CurrentMission;
                var nextMission = state.NextMission;

                CurrentTaskText.Text = null != currentMission ? $"{currentMission.Id} - {currentMission.Type}" : "None";
                NextTaskText.Text = null != nextMission ? $"{nextMission.Id} - {nextMission.Type}" : "None";
            });
        }

        private object indexLock = new object();

        public double AverrageQrCount { get; set; } = 0;

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
                    //if (value != 0.0) { _averrageQrIndex = value; }
                    _averrageQrIndex = value;

                }
            }
        }

        private async void Instance_PoseUpdated(ApriltagPoseEstimation pose)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (_frameLock)
                {
                    //var frame = pose.Frame;
                    //var bgraData = new byte[frame.Data.Length];

                    //var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);
                    //var bgraMat = new Mat();

                    //Cv2.CvtColor(srcMat, bgraMat, ColorConversionCodes.RGBA2BGRA);

                    //Marshal.Copy(bgraMat.Data, bgraData, 0, frame.Data.Length);

                    //if (LiveFeedSource == null || LiveFeedSource.PixelWidth != frame.Width || LiveFeedSource.PixelHeight != frame.Height)
                    //{
                    //    LiveFeedSource = new WriteableBitmap(frame.Width, frame.Height);
                    //    LiveFeedImage.Source = LiveFeedSource;
                    //}

                    //bgraData.AsBuffer().CopyTo(LiveFeedSource.PixelBuffer);

                    //LiveFeedSource.Invalidate();

                    var tempAverrageQrIndex = 0;
                    var tempAverageQrCount = 0;

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

                            tempAverrageQrIndex = tempAverrageQrIndex + num;
                            //AverrageQrIndex = AverrageQrIndex + num;
                            tempAverageQrCount += 1;

                        }
                    }

                    AverrageQrCount = tempAverageQrCount;
                    AverrageQrIndex = tempAverrageQrIndex;

                    //Debug.Print($"LocationTag: {ImageFrameCount} - { AverrageQrIndex}, {AverrageQrCount}, {AverrageQrIndex / AverrageQrCount} \n");
                    if (AverrageQrCount > 0) { FlightStacks.Instance._positionController.CurrentIndex = AverrageQrIndex / AverrageQrCount; }

                    //PoseText.Text = $"tag id: {pose.TagId} yaw: {pose.Yaw} pitch: {pose.Pitch} roll: {pose.Roll} tx: {pose.Tx} ty: {pose.Ty} tz: {pose.Tz}";
                }
            });
        }

        private void DisableControls(bool isEmergency)
        {
            DisableInputs();
        }

        private void EnableControls(bool isEmergency)
        {
            EnableInputs();
        }

        private void DisableInputs()
        { }

        private void EnableInputs()
        { }

        private InputArgs GetInputArgs()
        {
            var yawText = YawSetpointBox.Text;
            var altitudeText = AltitudeSetpointBox.Text;
            var relativeXText = RelativeXSetpointBox.Text;
            var relativeYText = RelativeYSetpointBox.Text;
            var locationIdText = LocationIdBox.Text;
            var isRightSide = !IsLeftSide.IsChecked;

            var yawValue = float.Parse(yawText);
            var altitudeValue = float.Parse(altitudeText);
            var relativeXValue = float.Parse(relativeXText);
            var relativeYValue = float.Parse(relativeYText);
            var locationValue = int.Parse(locationIdText);

            // TODO: Check input bound.

            if (yawValue < -180 || yawValue > 180)
            {
                yawValue = 35; // 0
            }

            if (altitudeValue > 1.7 || altitudeValue < 0.5)
            {
                altitudeValue = 1.2f;
            }

            if (relativeXValue < 2 || relativeXValue > 14)
            {
                relativeXValue = 13;
            }

            var args = new InputArgs(yawValue, altitudeValue, relativeXValue, relativeYValue, locationValue, isRightSide ?? false);

            return args;
        }

        private void EmergencyButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: disable all ui element
            // TODO: stop autonomous flight

            try
            {
                lock (_emergencyLock)
                {
                    if (_isLanding) return;

                    DisableControls(true);

                    _isLanding = true;

                    Drone.Instance.EmergencyLanding();

                    _isLanding = false;

                    EnableControls(true);
                }
            }
            catch (Exception ex)
            {
                if (_isLanding)
                {
                    // exception when landing command executing
                    // TODO: set the ui to error state, disable all ui control.
                }

                EnableControls(true);

                Logger.Instance.Log(ex.ToString());
            }

        }

        private void TakeOffButton_Click(object sender, RoutedEventArgs e)
        {
            // return if it is landing or taking off
            if (_isLanding || _isTakingOff) return;

            // return if it is flying
            // TODO: 

            try
            {
                _isTakingOff = true;
                DisableControls(false);
                Commander.AddTakeOffMission();
                _isTakingOff = false;
                EnableControls(false);
            }
            catch (Exception ex)
            {
                var oldValue = _isTakingOff;

                _isTakingOff = false;
                EnableControls(false);

                Logger.Instance.Log(ex.ToString());
            }

        }

        private void LandingButton_Click(object sender, RoutedEventArgs e)
        {
            // return if it is landing or taking off
            if (_isLanding || _isTakingOff) return;

            // return if it is flying
            // TODO: 
            try
            {
                _isLanding = true;
                DisableControls(false);
                Commander.AddLandingMission();
                _isLanding = false;
                EnableControls(false);
            }
            catch (Exception ex)
            {
                var oldValue = _isLanding;

                _isLanding = false;
                EnableControls(false);

                Logger.Instance.Log(ex.ToString());
            }
        }

        private void ManualAutoToggle_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void SaveFileClick(object sender, RoutedEventArgs e) {
            try
            {
                var gimbal = await DJISDKManager.Instance.ComponentManager.GetGimbalHandler(0, 0).GetGimbalAttitudeAsync();
                var tmpName = Path.GetTempFileName().Replace(".tmp", "").Split("\\").Last();
                var fileName = tmpName + $"_{gimbal.value.Value.pitch}" + ".csv";
                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(fileName);
                //var file = await StorageFile.CreateStreamedFileFromUriAsync(fileName); /*await ApplicationData.Current.LocalFolder.CreateFileAsync(ranName)*/;

                var stream = await file.OpenStreamForWriteAsync();
                var writer = new BinaryWriter(stream);
                var text = DecodeText.Text;

                writer.Write(text);
                //var charArray = text.ToCharArray();
                //var buffer = new byte[charArray.Length];

                //Buffer.BlockCopy(charArray, 0, buffer, 0, charArray.Length);

                writer.Dispose();
                stream.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        private void SetPointButton_Click(object sender, RoutedEventArgs e)
        {
            lock (_cmdLock)
            {
                DisableInputs();

                var args = GetInputArgs();

                Commander.AddSetPointMission(args.Yaw, args.Altitude, args.RelativeX, args.RelativeY, args.PositionId, args.RightSide);

                EnableInputs();
            }
        }

        private void StartMissionButton_Click(object sender, RoutedEventArgs e)
        {
            lock (_cmdLock)
            {
                DisableInputs();

                // TODO: design the mission stack
                Commander.Instance.AddTakeOffMission();

                //Thread.Sleep(3500);

                //Commander.Instance.AddSetPointMission(35, 1.5f, 0, 0, 13, true);
                //Commander.Instance.AddSetPointMission(35, 0.9f, 0, 0, 12, true);
                //Commander.Instance.AddSetPointMission(35, 0.5f, 0, 0, 11, true);
                //Commander.Instance.AddSetPointMission(-145, 0.5f, 0, 0, 11);
                //Commander.Instance.AddSetPointMission(-145, 1.1f, 0, 0, 11);
                //Commander.Instance.AddSetPointMission(-145, 1.7f, 0, 0, 10);

                //DoHackPath();
                //DoJasonPath();

                float frontYaw = 30;
                float backYaw = -150;
                
                Commander.Instance.AddSetPointMission(frontYaw, 1.4f, 0, 0, 14, false);
                Commander.Instance.AddSetPointMission(frontYaw, 1.4f, 0, 0, 8, false);
                Commander.Instance.AddSetPointMission(frontYaw, 1.4f, 0, 0, 2, false);

                Commander.Instance.AddSetPointMission(frontYaw, 0.9f, 0, 0, 2, false);
                Commander.Instance.AddSetPointMission(frontYaw, 0.9f, 0, 0, 8, false);
                Commander.Instance.AddSetPointMission(frontYaw, 0.9f, 0, 0, 14, false);

                Commander.Instance.AddSetPointMission(frontYaw, 0.5f, 0, 0, 14, false);
                Commander.Instance.AddSetPointMission(frontYaw, 0.5f, 0, 0, 8, false);
                Commander.Instance.AddSetPointMission(frontYaw, 0.5f, 0, 0, 2, false);

                EnableInputs();
            }
        }

        private void DoJasonPath()
        {
            // left side top row
            //Commander.Instance.AddSetPointMission(30, 1.4f, 0, 0, 14, false);
            //Commander.Instance.AddSetPointMission(30, 1.4f, 0, 0, 13, false);
            //Commander.Instance.AddSetPointMission(30, 1.4f, 0, 0, 12, false);
            //Commander.Instance.AddSetPointMission(30, 1.4f, 0, 0, 11, false);
            //Commander.Instance.AddSetPointMission(30, 1.4f, 0, 0, 10, false);

            // left side middle row

            //Commander.Instance.AddSetPointMission(30, 1.4f, 0, 0, 14, false);
            //Commander.Instance.AddSetPointMission(30, 0.9f, 0, 0, 10, false);
            //Commander.Instance.AddSetPointMission(30, 0.9f, 0, 0, 11, false);
            //Commander.Instance.AddSetPointMission(30, 0.9f, 0, 0, 12, false);
            //Commander.Instance.AddSetPointMission(30, 0.9f, 0, 0, 13, false);
            //Commander.Instance.AddSetPointMission(30, 0.9f, 0, 0, 14, false);

            // left side bottle row
            Commander.Instance.AddSetPointMission(30, 0.5f, 0, 0, 10, false);
            Commander.Instance.AddSetPointMission(30, 0.5f, 0, 0, 11, false);
            Commander.Instance.AddSetPointMission(30, 0.5f, 0, 0, 12, false);
            Commander.Instance.AddSetPointMission(30, 0.5f, 0, 0, 13, false);
            Commander.Instance.AddSetPointMission(30, 0.5f, 0, 0, 14, false);


            // Ture around
            Commander.Instance.AddSetPointMission(-150, 0.5f, 0, 0, 14, true);

            // move forward 
            // right side bottle row
            Commander.Instance.AddSetPointMission(-150, 0.5f, 5, 0, 14, true);
            Commander.Instance.AddSetPointMission(-150, 0.5f, 0, 0, 13, true);
            Commander.Instance.AddSetPointMission(-150, 0.5f, 0, 0, 12, true);
            Commander.Instance.AddSetPointMission(-150, 0.5f, 0, 0, 11, true);
            Commander.Instance.AddSetPointMission(-150, 0.5f, 0, 0, 10, true);

            // right side middle row
            Commander.Instance.AddSetPointMission(-150, 0.9f, 0, 0, 10, true);
            Commander.Instance.AddSetPointMission(-150, 0.9f, 0, 0, 11, true);
            Commander.Instance.AddSetPointMission(-150, 0.9f, 0, 0, 12, true);
            Commander.Instance.AddSetPointMission(-150, 0.9f, 0, 0, 13, true);
            Commander.Instance.AddSetPointMission(-150, 0.9f, 0, 0, 14, true);

            // right side top row
            Commander.Instance.AddSetPointMission(-150, 1.4f, 0, 0, 14, true);
            Commander.Instance.AddSetPointMission(-150, 1.4f, 0, 0, 13, true);
            Commander.Instance.AddSetPointMission(-150, 1.4f, 0, 0, 12, true);
            Commander.Instance.AddSetPointMission(-150, 1.4f, 0, 0, 11, true);
            Commander.Instance.AddSetPointMission(-150, 1.4f, 0, 0, 10, true);


        }

        private void DoHackPath()
        {
            // left side top row
            Commander.Instance.AddSetPointMission(20, 1.4f, 0, 0, 14, false);
            Commander.Instance.AddSetPointMission(20, 1.4f, 0, 0, 13, false);
            Commander.Instance.AddSetPointMission(20, 1.4f, 0, 0, 12, false);
            Commander.Instance.AddSetPointMission(20, 1.4f, 0, 0, 11, false);
            Commander.Instance.AddSetPointMission(20, 1.4f, 0, 0, 10, false);

            // left side middle row
            Commander.Instance.AddSetPointMission(20, 0.9f, 0, 0, 10, false);
            Commander.Instance.AddSetPointMission(20, 0.9f, 0, 0, 11, false);
            Commander.Instance.AddSetPointMission(20, 0.9f, 0, 0, 12, false);
            Commander.Instance.AddSetPointMission(20, 0.9f, 0, 0, 13, false);
            Commander.Instance.AddSetPointMission(20, 0.9f, 0, 0, 14, false);
            
            // right side top row
            Commander.Instance.AddSetPointMission(-160, 1.4f, 0, 0, 14, true);
            Commander.Instance.AddSetPointMission(-160, 1.4f, 0, 0, 13, true);
            Commander.Instance.AddSetPointMission(-160, 1.4f, 0, 0, 12, true);
            Commander.Instance.AddSetPointMission(-160, 1.4f, 0, 0, 11, true);
            Commander.Instance.AddSetPointMission(-160, 1.4f, 0, 0, 10, true);

            // right side middle row
            Commander.Instance.AddSetPointMission(-160, 0.9f, 0, 0, 10, true);
            Commander.Instance.AddSetPointMission(-160, 0.9f, 0, 0, 11, true);
            Commander.Instance.AddSetPointMission(-160, 0.9f, 0, 0, 12, true);
            Commander.Instance.AddSetPointMission(-160, 0.9f, 0, 0, 13, true);
            Commander.Instance.AddSetPointMission(-160, 0.9f, 0, 0, 14, true);
        }
    }
}
