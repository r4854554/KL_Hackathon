using DJI.WindowsSDK;
using DJI.WindowsSDK.Components;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace HDCircles.Hackathon.Services
{
    public struct FlightState
    {
        public double Altitude { get; }
        public double Yaw { get; }
        public double Pitch { get; }
        public double Roll { get; }
        public SDKError Error { get; }

        public FlightState(double altitude, double yaw, double pitch, double roll, SDKError error)
        {
            Altitude = altitude;
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
            Error = error;
        }
    }

    public delegate void StateChangedHandler(FlightState state);

    public sealed class Drone
    {
        public event StateChangedHandler StateChanged;

        private static DJISDKManager _sdkManager;

        public static DJISDKManager SdkManager
        {
            get
            {
                if (null == _sdkManager)
                {
                    _sdkManager = DJISDKManager.Instance;
                }

                return _sdkManager;
            }
        }

        private long updateFrequence = 100L; // milliseconds

        private object _stateLock = new object();

        /// <summary>
        /// indicates whether the sdk instance is able to connect
        /// </summary>
        public bool _isSdkRegistered;

        /// <summary>
        /// indicates the background worker is running.
        /// </summary>
        private bool _isWorkerEnabled;

        /// <summary>
        /// the flight controller handler from DJI SDK.
        /// </summary>
        private FlightControllerHandler fcHandler;

        /// <summary>
        /// 
        /// </summary>
        //private Thread _workerThread;

        private BackgroundWorker backgroundWorker;

        /// <summary>
        /// current flight state of the drone.
        /// </summary>
        private FlightState _currentState;
        public FlightState CurrentState { get=> _currentState; }

        public Drone()
        {
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += BackgroundWorker_DoWork;

            //_workerThread = new Thread(BackgroundWorker_DoWork);

            DJISDKManager.Instance.SDKRegistrationStateChanged += OnSdkRegistrationStateChanged;

            //_workerThread.Start();
            backgroundWorker.RunWorkerAsync();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var watch = Stopwatch.StartNew();

            var elapsed = 0L;
            var sleepTime = 0;

            for (; ; )
            {
                watch.Reset();
                watch.Start();

                if (!_isWorkerEnabled || !_isSdkRegistered || null == fcHandler)
                {
                    watch.Stop();

                    elapsed = watch.ElapsedMilliseconds;
                    sleepTime = sleepTime = (int)Math.Max(updateFrequence - elapsed, 0L);

                    Thread.Sleep(sleepTime);
                    continue;
                }

                CollectData().Wait();

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(updateFrequence - elapsed, 0L);

                //Debug.WriteLine($"Background thread id: {Thread.CurrentThread.ManagedThreadId}");
                //Debug.WriteLine("elapsed: " + watch.Elapsed.TotalMilliseconds);
                
                Thread.Sleep(sleepTime);
            }
        }

        private async Task CollectData()
        {
            //Debug.WriteLine($"Collect Data thread id: {Thread.CurrentThread.ManagedThreadId}");

            var attitude = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAttitudeAsync();
            var altitude = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();

            var flightStateError = attitude.error;
            var yaw = 0.0;
            var pitch = 0.0;
            var roll = 0.0;
            var altitudeValue = 0.0;

            if (attitude.error == SDKError.NO_ERROR)
            {
                yaw = attitude.value.Value.yaw; ;
                pitch = attitude.value.Value.pitch;
                roll = attitude.value.Value.roll;

                altitudeValue = altitude.value.Value.value;
            }

            lock (_stateLock)
            {
                _currentState = new FlightState(altitudeValue, yaw, pitch, roll, flightStateError);

                if (null != StateChanged)
                {
                    // dispatch the state
                    StateChanged.Invoke(_currentState);
                }
                DateTime localDate = DateTime.Now;
                //Debug.WriteLine($"Info:CollectData:{localDate.Millisecond:G} - yaw: {yaw} pitch - {pitch} roll - {roll} altitude - {altitudeValue}");
                //Debug.WriteLine($"yaw: {_currentState.Yaw} pitch: {_currentState.Pitch} roll: {_currentState.Roll} altitude: {_currentState.Altitude}");
            }
        }

        private void OnSdkRegistrationStateChanged(SDKRegistrationState state, SDKError error)
        {
            _isSdkRegistered = SDKError.NO_ERROR == error && state == SDKRegistrationState.Succeeded;

            if (_isSdkRegistered)
            {
                _isWorkerEnabled = true;
                fcHandler = DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0);
            }
            else
            {
                _isWorkerEnabled = false;
                fcHandler = null;
            }

            Thread.Sleep(300);
        }
    }
}
