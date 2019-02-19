namespace HDCircles.Hackathon.Views
{    
    using DJI.WindowsSDK;
    using DJIVideoParser;
    using OpenCvSharp;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public sealed partial class CalibrationPage : Page
    {
        struct CalibrateImage
        {
            public byte[] Buffer { get; }

            public int Width { get; }

            public int Height { get; }

            public CalibrateImage(byte[] buffer, int width, int height)
            {
                Buffer = buffer;
                Width = width;
                Height = height;
            }
        }

        private object bufferLock = new object();

        private byte[] frameBuffer;

        private int frameWidth;

        private int frameHeight;

        private Parser videoParser;

        private List<CalibrateImage> calibrateImages = new List<CalibrateImage>();

        public CalibrationPage()
        {
            this.InitializeComponent();

            Loaded += CalibrationPage_Loaded;
            Unloaded += CalibrationPage_Unloaded;
        }

        private void CalibrationPage_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            if (DJISDKManager.Instance.SDKRegistrationResultCode == SDKError.NO_ERROR)
            {
                // manually dispatch the state changed event 
                Instance_SDKRegistrationStateChanged(SDKRegistrationState.Succeeded, SDKError.NO_ERROR);
            }

            Loaded -= CalibrationPage_Loaded;
        }

        private void CalibrationPage_Unloaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var videoFeeder = DJISDKManager.Instance.VideoFeeder;

                if (null != videoFeeder)
                    videoFeeder.GetPrimaryVideoFeed(0).VideoDataUpdated -= VideoFeeder_VideoDataUpdated;

                if (null != videoParser)
                {
                    videoParser.Uninitialize();
                }

                Unloaded -= CalibrationPage_Unloaded;
            }
            catch (Exception ex)
            {
                // TODO: handle exception
            }
        }

        private async void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            if (SDKError.NO_ERROR == errorCode && SDKRegistrationState.Succeeded == state)
            {
                var videoFeeder = DJISDKManager.Instance.VideoFeeder.GetPrimaryVideoFeed(0);
                var cameraHandler = DJISDKManager.Instance.ComponentManager.GetCameraHandler(0, 0);

                if (null != videoFeeder)
                {
                    videoParser = new Parser();
                    videoParser.Initialize((byte[] data) =>
                    {
                        return DJISDKManager.Instance.VideoFeeder.ParseAssitantDecodingInfo(0, data);
                    });
                    videoParser.SetSurfaceAndVideoCallback(0, 0, LiveFeedPanel, OnFrameParsed);

                    videoFeeder.VideoDataUpdated += VideoFeeder_VideoDataUpdated;                    
                }

                if (null != cameraHandler)
                {
                    var res = await cameraHandler.GetCameraTypeAsync();

                    cameraHandler.CameraTypeChanged += CalibrationPage_CameraTypeChanged;

                    if (res.error == SDKError.NO_ERROR)
                    {
                        CalibrationPage_CameraTypeChanged(null, res.value);
                    }
                }

                // wait until action done
                Thread.Sleep(300);
            }
        }

        private void CalibrationPage_CameraTypeChanged(object sender, CameraTypeMsg? value)
        {
            if (null != value && null != videoParser)
            {
                switch (value.Value.value)
                {
                    case CameraType.MAVIC_2_PRO:
                        videoParser.SetCameraSensor(AircraftCameraType.Mavic2Pro);
                        break;
                    case CameraType.MAVIC_2_ZOOM:
                        videoParser.SetCameraSensor(AircraftCameraType.Mavic2Zoom);
                        break;
                    default:
                        videoParser.SetCameraSensor(AircraftCameraType.Others);
                        break;
                }
            }
        }

        private void VideoFeeder_VideoDataUpdated(VideoFeed sender, byte[] bytes)
        {
            if (null != videoParser)
                videoParser.PushVideoData(0, 0, bytes, bytes.Length);
        }

        private void OnFrameParsed(byte[] data, int width, int height)
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
        }

        private async void CaptureImageButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            lock (bufferLock)
            {
                if (frameWidth == 0 || frameHeight == 0)
                    return;

                var buffer = new byte[frameWidth * frameHeight * 4];

                frameBuffer.CopyTo(buffer.AsBuffer());

                var img = new CalibrateImage(buffer, frameWidth, frameHeight);

                calibrateImages.Add(img);
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ImageCount.Text = "Calibration Image Count: " + calibrateImages.Count;
            });
        }

        private void DoCalibrationButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var objPoints = new List<Point3f>();
            var imgPoints = new List<Point2f>();
            var chessboardSize = new Size(7, 5);
            var terminationCriteria = new TermCriteria(CriteriaType.Eps | CriteriaType.MaxIter, 30, 0.001);

            for (int x = 0; x < chessboardSize.Height; x++)
            {
                for (int y = 0; y < chessboardSize.Width; y++)
                {

                }
            }

            foreach (var ci in calibrateImages)
            {
                var img = new Mat(ci.Height, ci.Width, MatType.CV_8UC4, ci.Buffer);
                Mat grayImg = new Mat();
                Point2f[] corners;

                Cv2.CvtColor(img, grayImg, ColorConversionCodes.RGBA2GRAY);
                               
                Cv2.ImShow("gray", grayImg);
                Cv2.WaitKey(5000);

                var result = Cv2.FindChessboardCorners(grayImg, chessboardSize, out corners, ChessboardFlags.None);

                if (result)
                {
                    var winSize = new Size(11, 11);
                    var zeroZone = new Size(-1, -1);
                    var refinedCorners = Cv2.CornerSubPix(grayImg, corners, winSize, zeroZone, terminationCriteria);

                    Cv2.DrawChessboardCorners(img, chessboardSize, refinedCorners, result);
                    Cv2.ImShow("img", img);
                    Cv2.WaitKey(500);

                    //Cv2.CalibrateCamera(objPoints, imgPoints, grayImg.Size(), );
                }
            }
            //Cv2.DestroyAllWindows();
        }
    }
}
