using AprilTagsSharp;
using DJI.WindowsSDK;
using Dynamsoft.Barcode;
using HDCircles.Hackathon.Services;
using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace HDCircles.Hackathon
{
    public delegate void PoseEstimationHandler(ApriltagPoseEstimation pose);

    /// <summary>
    /// Pose estimation based on Apriltags.
    /// </summary>
    public struct ApriltagPoseEstimation
    {
        public LiveFrame Frame { get; }

        public TextResult[] DetectResults { get; }

        //public int TagId { get; }

        //public double Yaw { get; }

        //public double Pitch { get; }

        //public double Roll { get; }

        //public double Tx { get; }

        //public double Ty { get; }

        //public double Tz { get; }
        

        public ApriltagPoseEstimation(TextResult[] results, LiveFrame frame)
        {
            //TagId = tagId;

            //Yaw = yaw;
            //Pitch = pitch;
            //Roll = roll;

            //Tx = tx;
            //Ty = ty;
            //Tz = tz;

            DetectResults = results;
            Frame = frame;
        }
    }

    public class PosController
    {
        private const string DynamsoftAppKey = "t0068NQAAALjRYgQPyFU9w77kwoOtA6C+n34MIhvItkLV0+LcUVEef9fN3hiwyNTlUB8Lg+2XYci3vEYVCc4mdcuhAs7mVMg=";

        public event PoseEstimationHandler PoseUpdated;

        private Thread _thread;

        private ApriltagPoseEstimation PoseEstimation { get; set; }

        private object updateLock = new object();

        private long WorkFrequence = 250L;

        private bool _isRunning;

        private static PosController _instance;

        public static PosController Instance
        {
            get
            {
                if (null == _instance)
                {
                    _instance = new PosController();
                }

                return _instance;
            }
        }

        private PosController()
        {
            _thread = new Thread(Thread_Run);
        }

        public void Start()
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            if (DJISDKManager.Instance.SDKRegistrationResultCode == SDKError.NO_ERROR)
            {
                Instance_SDKRegistrationStateChanged(SDKRegistrationState.Succeeded, SDKError.NO_ERROR);
            }
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            var isReg = errorCode == SDKError.NO_ERROR;

            if (_isRunning)
                return;

            _thread.Start();
            _isRunning = true;
        }

        private void Thread_Run()
        {
            var watch = Stopwatch.StartNew();
            var elapsed = 0L;
            var sleepTime = 0;

            for (; ; )
            {
                watch.Restart();

                var liveFrame = Drone.Instance.GetLiveFrame();

                if (liveFrame.Width == 0 || liveFrame.Height == 0)
                {
                    watch.Stop();
                    elapsed = watch.ElapsedMilliseconds;
                    sleepTime = (int)Math.Max(WorkFrequence - elapsed, 0);

                    Thread.Sleep(sleepTime);
                    continue;
                }

                DetectApriltag(liveFrame);

                //detections.Wait();

                //var result = detections.Result;

                //if (null != result && result.Count > 0)
                //{
                //    var det = result[0] as Detector;

                //    PoseEstimate(liveFrame, det).Wait();
                //}

                //Debug.WriteLine($"yaw: {PoseEstimation.Yaw}, tz: {PoseEstimation.Tz}");

                watch.Stop();
                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(WorkFrequence - elapsed, 0);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task DetectApriltag(LiveFrame frame)
        {
            var br = new BarcodeReader(DynamsoftAppKey);
            var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);
            var grayMat = new Mat();

            Cv2.CvtColor(srcMat, grayMat, ColorConversionCodes.RGBA2GRAY);

            var stride = grayMat.Cols * grayMat.ElemSize();
            var length = stride * grayMat.Rows;
            var buffer = new byte[length];

            Marshal.Copy(grayMat.Data, buffer, 0, length);

            var results = br.DecodeBuffer(buffer, grayMat.Cols, grayMat.Rows, stride, EnumImagePixelFormat.IPF_GrayScaled, "");
            var pose = new ApriltagPoseEstimation(results, frame);
            //var aprilTag = new AprilTag("canny", false, "tag36h11", 0.8, 1, 400);
            //var detections = aprilTag.Detect(frame.Height, frame.Width, frame.Data);
            //var structedResults = results.Select(x => new ApriltagPoseEstimation(x.BarcodeText, x.LocalizationResult, frame)).ToList();

            if (null != PoseUpdated)
            {
                PoseUpdated.Invoke(pose);
            }
        }

        private async Task PoseEstimate(LiveFrame frame, Detector det)
        {
            var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);            
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
            //var d = Math.Sqrt(Math.Pow(tx, 2) + Math.Pow(ty, 2) + Math.Pow(tz, 2));

            lock (updateLock) {
                //var poseEstimation = new ApriltagPoseEstimation(det.id, yaw, pitch, roll, tx, ty, tz, frame);

                //PoseEstimation = poseEstimation;

                //if (null != PoseUpdated)
                //{
                //    PoseUpdated.Invoke(poseEstimation);
                //}
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
    }
}
