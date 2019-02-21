namespace HDCircles.Hackathon.Services
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using DJI.WindowsSDK;
    using Dynamsoft.Barcode;
    using OpenCvSharp;

    public struct QrcodeDetection
    {
        public LiveFrame Frame { get; }

        public TextResult[] Results { get; }

        public QrcodeDetection(TextResult[] results, LiveFrame frame)
        {
            Frame = frame;
            Results = results;
        }
    }

    public delegate void QrcodeDetectHandler(QrcodeDetection qrcode);

    public class QrcodeDetector
    {
        public event QrcodeDetectHandler QrcodeDetected;

        private const string DynamsoftAppKey = "t0068NQAAALjRYgQPyFU9w77kwoOtA6C+n34MIhvItkLV0+LcUVEef9fN3hiwyNTlUB8Lg+2XYci3vEYVCc4mdcuhAs7mVMg=";

        private object updateLock = new object();

        private long WorkFrequence = 250L;

        private Thread workerThread;

        private static QrcodeDetector _instance;

        public static QrcodeDetector Instance
        {
            get
            {
                if (null == _instance)
                    _instance = new QrcodeDetector();

                return _instance;
            }
        }

        private QrcodeDetector()
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            workerThread = new Thread(DoWork);
        }

        private void DoWork()
        {
            var watch = Stopwatch.StartNew();
            var elapsed = 0L;
            var sleepTime = 0;


            for (; ; )
            {
                watch.Restart();

                DetectQrcode().Wait();

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(WorkFrequence - elapsed, 0);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task DetectQrcode()
        {
            var frame = Drone.Instance.GetLiveFrame();

            if (frame.Width == 0 || frame.Height == 0 || frame.Data == null || frame.Data.Length == 0)
                return;

            var srcMat = new Mat(frame.Height, frame.Width, MatType.CV_8UC4, frame.Data);
            var grayMat = new Mat();

            Cv2.CvtColor(srcMat, grayMat, ColorConversionCodes.RGBA2GRAY);

            var stride = grayMat.Cols * grayMat.ElemSize();
            var size = stride * grayMat.Rows;
            var data = new byte[size];

            Marshal.Copy(grayMat.Data, data, 0, size);

            lock (updateLock)
            {
                var results = default(TextResult[]);

                {
                    var br = new BarcodeReader();

                    br.LicenseKeys = DynamsoftAppKey;
                    results = br.DecodeBuffer(data, frame.Width, frame.Height, stride, EnumImagePixelFormat.IPF_GrayScaled, "");
                }

                if (null != QrcodeDetected)
                {
                    var qrcode = new QrcodeDetection(results, frame);

                    QrcodeDetected.Invoke(qrcode);
                }
            }
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            var isReg = state == SDKRegistrationState.Succeeded && errorCode == SDKError.NO_ERROR;

            if (isReg)
            {
                workerThread.Start();
            }
        }
    }
}
