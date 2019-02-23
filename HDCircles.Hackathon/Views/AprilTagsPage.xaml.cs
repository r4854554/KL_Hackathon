namespace HDCircles.Hackathon.Views
{
    using DJI.WindowsSDK;
    using HDCircles.Hackathon.Services;
    using OpenCvSharp;
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media.Imaging;

    public sealed partial class AprilTagsPage : Page
    {
        private object _frameLock = new object();

        private WriteableBitmap LiveFrameSource { get; set; }

        public AprilTagsPage()
        {
            InitializeComponent();

            Loaded += AprilTagsPage_Loaded;
            Unloaded += AprilTagsPage_Unloaded;
        }

        private void AprilTagsPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged; ;

            if (DJISDKManager.Instance.SDKRegistrationResultCode == SDKError.NO_ERROR)
            {
                // manually dispatch the state changed event 
                Instance_SDKRegistrationStateChanged(SDKRegistrationState.Succeeded, SDKError.NO_ERROR);
            }

            Loaded -= AprilTagsPage_Loaded;
        }

        private void AprilTagsPage_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                PosController.Instance.PoseUpdated -= PosController_PoseUpdated;

                // sleep until the worker thread exit.
                Thread.Sleep(300);

                Unloaded -= AprilTagsPage_Unloaded;
            }
            catch (Exception ex)
            {
                // TODO: handle exception
                Console.Write(ex.ToString());
            }
        }

        private async void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            if (SDKError.NO_ERROR == errorCode && SDKRegistrationState.Succeeded == state)
            {
                PosController.Instance.PoseUpdated += PosController_PoseUpdated;

                // wait until action done
                Thread.Sleep(300);
            }
        }

        private async void PosController_PoseUpdated(ApriltagPoseEstimation pose)
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

                    if (LiveFrameSource == null || LiveFrameSource.PixelWidth != frame.Width || LiveFrameSource.PixelHeight != frame.Height)
                    {
                        LiveFrameSource = new WriteableBitmap(frame.Width, frame.Height);
                        LiveFeedImage.Source = LiveFrameSource;
                    }

                    bgraData.AsBuffer().CopyTo(LiveFrameSource.PixelBuffer);

                    LiveFrameSource.Invalidate();
                    //PoseText.Text = $"tag id: {pose.TagId} yaw: {pose.Yaw} pitch: {pose.Pitch} roll: {pose.Roll} tx: {pose.Tx} ty: {pose.Ty} tz: {pose.Tz}";
                }
            });
        }
    }
}
