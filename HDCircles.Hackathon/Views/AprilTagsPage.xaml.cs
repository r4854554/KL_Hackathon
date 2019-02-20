namespace HDCircles.Hackathon.Views
{
    using AprilTagsSharp;
    using DJI.WindowsSDK;
    using DJIVideoParser;
    using OpenCvSharp;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Threading;
    using System.Threading.Tasks;
    using Windows.UI.Core;
    using Windows.UI.Xaml.Controls;

    public sealed partial class AprilTagsPage : Page
    {
        private long parseFreqeunce = 200L; // milliseconds

        private object bufferLock = new object();

        private byte[] frameBuffer;

        private int frameWidth;

        private int frameHeight;

        private Parser videoParser;

        private Thread workerThread;

        private bool _isWorkerEnabled;

        public AprilTagsPage()
        {
            InitializeComponent();

            Loaded += AprilTagsPage_Loaded;
            Unloaded += AprilTagsPage_Unloaded;

            workerThread = new Thread(WorkerTask);
        }

        private void WorkerTask()
        {
            var watch = Stopwatch.StartNew();

            for (; ; )
            {
                if (!_isWorkerEnabled)
                    break;

                watch.Reset();
                watch.Start();

                EstimatePose();

                watch.Stop();

                var elapsed = watch.ElapsedMilliseconds;
                var sleepTime = Math.Max(parseFreqeunce - elapsed, 0);

                Thread.Sleep((int)sleepTime);
            }
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
                var videoFeeder = DJISDKManager.Instance.VideoFeeder;
                var cameraHandler = DJISDKManager.Instance.ComponentManager.GetCameraHandler(0 ,0);

                if (null != videoFeeder)
                    videoFeeder.GetPrimaryVideoFeed(0).VideoDataUpdated -= VideoFeeder_VideoDataUpdated;

                if (null != videoParser)
                {
                    videoParser.SetSurfaceAndVideoCallback(0, 0, null, null);
                    //videoParser.Uninitialize();
                    videoParser = null;
                }

                if (null != cameraHandler)
                {
                    cameraHandler.CameraTypeChanged -= CameraHandler_CameraTypeChanged;
                }

                _isWorkerEnabled = false;

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

                    videoFeeder.VideoDataUpdated += VideoFeeder_VideoDataUpdated; ;
                }

                if (null != cameraHandler)
                {
                    var res = await cameraHandler.GetCameraTypeAsync();

                    cameraHandler.CameraTypeChanged += CameraHandler_CameraTypeChanged; ;

                    if (res.error == SDKError.NO_ERROR)
                    {
                        CameraHandler_CameraTypeChanged(null, res.value);
                    }
                }

                // wait until action done
                Thread.Sleep(300);

                workerThread.Start();
                _isWorkerEnabled = true;
            }
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

        private void VideoFeeder_VideoDataUpdated(VideoFeed sender, byte[] bytes)
        {
            if (null != videoParser)
            {
                videoParser.PushVideoData(0, 0, bytes, bytes.Length);
            }
        }

        private void CameraHandler_CameraTypeChanged(object sender, CameraTypeMsg? value)
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

        private async Task<Mat> GetTransformFromDetection(Detector det, float tagSize)
        {
            var objPoints = new List<Point3f>();
            var imgPoints = new List<Point2f>();
            var tagRadius = tagSize / 2;

            objPoints.Add(new Point3f { X = -tagRadius, Y = -tagRadius, Z = 0 });
            objPoints.Add(new Point3f { X = tagRadius, Y = -tagRadius, Z = 0 });
            objPoints.Add(new Point3f { X = tagRadius, Y = tagRadius, Z = 0 });
            objPoints.Add(new Point3f { X = -tagRadius, Y = tagRadius, Z = 0 });

            switch (det.rotation)
            {
                case 1:                    
                    // first quadrant
                    imgPoints.Add(det.points[2]);
                    imgPoints.Add(det.points[1]);
                    imgPoints.Add(det.points[0]);
                    imgPoints.Add(det.points[3]);
                    break;
                case 0:
                    // fourth quadrant
                    imgPoints.Add(det.points[3]);
                    imgPoints.Add(det.points[2]);
                    imgPoints.Add(det.points[1]);
                    imgPoints.Add(det.points[0]);
                    break;
                case 3:
                    // third quadrant
                    imgPoints.Add(det.points[0]);
                    imgPoints.Add(det.points[3]);
                    imgPoints.Add(det.points[2]);
                    imgPoints.Add(det.points[1]);
                    break;
                case 2:
                    // second quadrant
                    imgPoints.Add(det.points[1]);
                    imgPoints.Add(det.points[0]);
                    imgPoints.Add(det.points[3]);
                    imgPoints.Add(det.points[2]);
                    break;
            }            

            var intrinsics = new double[3, 3]
            {
                { 446.362, 0, 631.601 },
                { 0, 448.676, 350.1 },
                { 0, 0, 1 },
            };
            var distortionCoeff = new double[4]
            {
                0, 0, 0, 0
            };

            var rVec = new double[9];
            var tVec = new double[3];

            Cv2.SolvePnP(objPoints.ToArray(), imgPoints.ToArray(), intrinsics, distortionCoeff, out rVec, out tVec, false, SolvePnPFlags.Iterative);

            var r = new double[3, 3];

            Cv2.Rodrigues(rVec, out r);

            var mat = new Mat(4, 4, MatType.CV_64FC1);
            var indexer = mat.GetGenericIndexer<double>();

            indexer[0, 0] = r[0, 0];
            indexer[0, 1] = r[0, 1];
            indexer[0, 2] = r[0, 2];
            indexer[1, 0] = r[1, 0];
            indexer[1, 1] = r[1, 1];
            indexer[1, 2] = r[1, 2];
            indexer[2, 0] = r[2, 0];
            indexer[2, 1] = r[2, 1];
            indexer[2, 2] = r[2, 2];

            indexer[0, 3] = tVec[0];
            indexer[1, 3] = tVec[1];
            indexer[2, 3] = tVec[2];

            indexer[3, 0] = 0;
            indexer[3, 1] = 0;
            indexer[3, 2] = 0;
            indexer[3, 3] = 1;

            return mat;
        }

        private async Task<ArrayList> DetectTag(Mat src)
        {
            var ap = new AprilTag("canny", false, "tag36h11", 0.8, 1, 400);

            return ap.detect(src);
        }

        private async void EstimatePose()
        {
            int width, height;
            byte[] buffer;

            lock (bufferLock)
            {
                if (frameWidth == 0 || frameHeight == 0)
                    return;

                buffer = new byte[frameWidth * frameHeight * 4];
                width = frameWidth;
                height = frameHeight;

                frameBuffer.CopyTo(buffer.AsBuffer());
            }

            var srcMat = new Mat(height, width, MatType.CV_8UC4, buffer);
            var detections = await DetectTag(srcMat);

            foreach (var o in detections)
            {
                var det = o as Detector;
                var detIndexer = det.homography.GetGenericIndexer<double>();
                
                var transform = await GetTransformFromDetection(det, 100f);
                var indexer = transform.GetGenericIndexer<double>();

                var tx = indexer[0, 3];
                var ty = indexer[1, 3];
                var tz = indexer[2, 3];
                var yawRa = Math.Atan2(indexer[1, 0], indexer[0, 0]);
                var pitchRa = Math.Atan2(-indexer[2, 0], Math.Sqrt(Math.Pow(indexer[2, 1], 2) + Math.Pow(indexer[2, 2], 2)));
                var rollRa = Math.Atan2(indexer[2, 1], indexer[2, 2]);

                var yaw = yawRa * (180 / Math.PI);
                var pitch = pitchRa * (180 / Math.PI);
                var roll = rollRa * (180 / Math.PI);
                var d = Math.Sqrt(Math.Pow(tx, 2) + Math.Pow(ty, 2) + Math.Pow(tz, 2));

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {

                    RotationText.Text = $"Rotation: {det.rotation} Yaw: {yaw:0.000} Pitch: {pitch:0.000} Roll: {roll:0.000}";
                    DistanceText.Text = $"Distance: {d:0.000} tx: {tx:0.000} ty: {ty:0.000} tz: {tz:0.000}";
                });
            }
        }

        private async void EstimatePoseButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            //EstimatePose();
        }
    }
}
