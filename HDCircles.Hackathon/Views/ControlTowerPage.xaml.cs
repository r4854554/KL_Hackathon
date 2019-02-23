namespace HDCircles.Hackathon.Views
{
    using DJI.WindowsSDK;
    using HDCircles.Hackathon.Services;
    using OpenCvSharp;
    using System;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
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
                Commander.MissionUpdated += Commander_MissionUpdated;
            }
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

        private async void Instance_PoseUpdated(ApriltagPoseEstimation pose)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (_frameLock)
                {
                    var frame = pose.Frame;
                    var bgraData = new byte[frame.Data.Length];

                    var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);
                    var bgraMat = new Mat();

                    Cv2.CvtColor(srcMat, bgraMat, ColorConversionCodes.RGBA2BGRA);

                    Marshal.Copy(bgraMat.Data, bgraData, 0, frame.Data.Length);

                    if (LiveFeedSource == null || LiveFeedSource.PixelWidth != frame.Width || LiveFeedSource.PixelHeight != frame.Height)
                    {
                        LiveFeedSource = new WriteableBitmap(frame.Width, frame.Height);
                        LiveFeedImage.Source = LiveFeedSource;
                    }

                    bgraData.AsBuffer().CopyTo(LiveFeedSource.PixelBuffer);

                    LiveFeedSource.Invalidate();
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

            if (relativeXValue < 1 || relativeXValue > 15)
            {
                relativeXValue = 13;
            }

            var args = new InputArgs(yawValue, altitudeValue, relativeXValue, relativeYValue, locationValue, isRightSide ?? true);
            
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

                Thread.Sleep(3500);

                //Commander.Instance.AddSetPointMission(35, 1.7f, 0, 0, 13);
                //Commander.Instance.AddSetPointMission(35, 1.1f, 0, 0, 12);
                //Commander.Instance.AddSetPointMission(35, 0.5f, 0, 0, 11);
                //Commander.Instance.AddSetPointMission(-145, 0.5f, 0, 0, 11);
                //Commander.Instance.AddSetPointMission(-145, 1.1f, 0, 0, 11);
                //Commander.Instance.AddSetPointMission(-145, 1.7f, 0, 0, 10);
                
                EnableInputs();
            }
        }
    }
}
