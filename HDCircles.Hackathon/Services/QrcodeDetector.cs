namespace HDCircles.Hackathon.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Runtime.InteropServices.WindowsRuntime;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using CustomVision;
    using DJI.WindowsSDK;
    using Dynamsoft.Barcode;
    using OpenCvSharp;
    using Windows.Graphics.Imaging;
    using Windows.Media;
    using Windows.Storage;

    public struct QrcodeDetection
    {
        public LiveFrame Frame { get; }

        public string Results { get; }

        public QrcodeDetection(string results, LiveFrame frame)
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

        private bool _isRunning;
        
        private ObjectDetection objectDetection;

        private BarcodeReader br;
        private Regex LocationTagReg = new Regex(@"^ [A-Z] [0-9]{2} [0-9]{2} [0-9] [0-9]{2}$");
        private Regex CartonTagReg = new Regex(@"^[0-9]{6}$");
        public List<string> ResultLocation = new List<string>();
        public List<List<string>> ResultCarton = new List<List<string>>();

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

                ScanFrame().Wait();

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(WorkFrequence - elapsed, 0);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task<int> ScanFrame()
        {
            var frame = Drone.Instance.GetLiveFrame();
            
            if (frame.Width == 0 || frame.Height == 0 || frame.Data == null || frame.Data.Length == 0)
                return -1;

            var data = frame.Data;
            var height = frame.Height;
            var width = frame.Width;
            // resize image to (416,416)
            var mat = new Mat(height, width, MatType.CV_8UC4, data);
            var gray = new Mat();
            Mat resizemat = new Mat();
            resizemat = mat.Clone();
            Cv2.CvtColor(mat, mat, ColorConversionCodes.RGBA2BGRA);
            Cv2.CvtColor(mat, gray, ColorConversionCodes.RGBA2GRAY);
            resizemat = resizemat.Resize(new Size(416, 416), 0, 0, InterpolationFlags.Linear);
            //Cv2.ImShow("hell0", mat);
            //Cv2.WaitKey(500);

            // Mat -> softwarebitmap
            var resizedLength = resizemat.Rows * resizemat.Cols * resizemat.ElemSize();
            var buffer = new byte[resizedLength];
            Marshal.Copy(resizemat.Data, buffer, 0, resizedLength);
            var bm = SoftwareBitmap.CreateCopyFromBuffer(buffer.AsBuffer(), BitmapPixelFormat.Rgba8, resizemat.Cols, resizemat.Rows);
            try
            {
                IList<PredictionModel> outputlist = await objectDetection.PredictImageAsync(VideoFrame.CreateWithSoftwareBitmap(bm));
                foreach (var output in outputlist)
                {
                    //chop origin image
                    //Mat chop = ChopOutData(gray, output.BoundingBox, height, width);
                    //var chop_image_length = chop.Rows * chop.Cols * chop.ElemSize();
                    //var chop_image_buffer = new byte[chop_image_length];
                    //Marshal.Copy(chop.Data, chop_image_buffer, 0, chop_image_length);

                    var gray_length = gray.Rows * gray.Cols * gray.ElemSize();
                    var gray_buffer = new byte[gray_length];
                    Marshal.Copy(gray.Data, gray_buffer, 0, gray_length);

                    br = new BarcodeReader();
                    br.LicenseKeys = DynamsoftAppKey;
                    TextResult[] result =
                        br.DecodeBuffer(gray_buffer, gray.Cols, gray.Rows, gray.Cols * gray.ElemSize(), EnumImagePixelFormat.IPF_GrayScaled, "");

                    //chop 
                    //TextResult[] result =
                    //      br.DecodeBuffer(chop_image_buffer, chop.Cols, chop.Rows, chop.Cols * chop.ElemSize(), EnumImagePixelFormat.IPF_GrayScaled, "");

                    int index = 0;
                    switch (output.TagName)
                    {

                        case "Box":
                            if (result.Length < 2)
                                return -1;
                            string LocationTag = "";
                            List<string> CartonTag = new List<string>();
                            foreach (var pick in result)
                            {
                                if (LocationTagReg.Match(pick.BarcodeText).Success)
                                {
                                    LocationTag = pick.BarcodeText;
                                }
                                else if (CartonTagReg.Match(pick.BarcodeText).Success)
                                {
                                    CartonTag.Add(pick.BarcodeText);
                                }
                            }
                            if (LocationTag == "" || CartonTag.Count < 1)
                                return -2;
                            index = ResultLocation.IndexOf(LocationTag);
                            if (index == -1)
                            {
                                ResultLocation.Add(LocationTag);
                                ResultCarton.Add(CartonTag);
                            }
                            else
                            {
                                ResultCarton[index] = CartonTag;
                            }
                            break;
                        case "Nobox":
                            if (result.Length < 1 || !LocationTagReg.Match(result[0].BarcodeText).Success)
                                return -2;

                            int i_dx = ResultLocation.IndexOf(result[0].BarcodeText);
                            if (i_dx == -1)
                            {
                                ResultLocation.Add(result[0].BarcodeText);
                                ResultCarton.Add(new List<string>());
                            }
                            else
                            {
                                ResultCarton[i_dx] = new List<string>();
                            }
                            break;
                    }
                    //UpdateDecodeText();

                    lock (updateLock)
                    {
                        string resultText = "";
                        index = ResultLocation.Count;
                        for (int i = 0; i < index; i++)
                        {
                            resultText += ResultLocation[i] + " -> ";
                            foreach (string carton in ResultCarton[i])
                            {
                                resultText += carton + " ";
                            }
                            resultText += "\n";
                        }

                        var qrcode = new QrcodeDetection(resultText, frame);

                        if (null != QrcodeDetected)
                        {
                            QrcodeDetected.Invoke(qrcode);
                        }
                    }

                    
                }
            }
            catch (Exception ex)
            {
                lock (updateLock)
                {
                    //string resultText = "";
                    //int index = ResultLocation.Count;
                    //for (int i = 0; i < index; i++)
                    //{
                    //    resultText += ResultLocation[i] + " -> ";
                    //    foreach (string carton in ResultCarton[i])
                    //    {
                    //        resultText += carton + " ";
                    //    }
                    //    resultText += "\n";
                    //}

                    var qrcode = new QrcodeDetection(ex.Message, frame);

                    if (null != QrcodeDetected)
                    {
                        QrcodeDetected.Invoke(qrcode);
                    }
                }
                return -1;
            }
            return 0;
        }
        
        private Mat ChopOutData(Mat image, BoundingBox box, int height, int width)
        {
            double x = (double)Math.Max(box.Left, 0);
            double y = (double)Math.Max(box.Top, 0);
            double w = (double)Math.Min(1 - x, box.Width);
            double h = (double)Math.Min(1 - y, box.Height);

            x = width * x;
            y = height * y;
            w = width * w;
            h = height * h;

            return new Mat(image, new Rect((int)x, (int)y, (int)w, (int)h)).Clone();
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
                    //var qrcode = new QrcodeDetection(results, frame);

                    //QrcodeDetected.Invoke(qrcode);
                }
            }
        }

        private async void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            var isReg = state == SDKRegistrationState.Succeeded && errorCode == SDKError.NO_ERROR;

            if (_isRunning)
                return;

            if (isReg)
            {
                //List<String> labels = new List<String> { "Box", "Nobox" };
                //objectDetection = new ObjectDetection(labels, 10, 0.6F, 0.45F);
                //await init_onnx();
                //_isRunning = true;

                //Thread.Sleep(2000);
                //workerThread.Start();
            }
        }
        
        private async Task init_onnx()
        {
            StorageFile file = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///AI/model.onnx"));
            await objectDetection.Init(file);
        }
    }
}
