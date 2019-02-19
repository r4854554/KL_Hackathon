using DJI.WindowsSDK;
using DJI.WindowsSDK.Components;
using System;
using System.Diagnostics;
using System.Threading;

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

    public sealed class DjiSdkManager
    {
        public event StateChangedHandler StateChanged;

        private static DjiSdkManager _instance;

        public static DjiSdkManager Instance
        {
            get
            {
                if (null == _instance)
                {
                    _instance = new DjiSdkManager();
                }

                return _instance;
            }
        }

        private long updateFrequence = 50L; // milliseconds

        private object _stateLock = new object();

        /// <summary>
        /// indicates whether the sdk instance is able to connect
        /// </summary>
        private bool _isSdkRegistered;

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
        private Thread _workerThread;

        /// <summary>
        /// current flight state of the drone.
        /// </summary>
        private FlightState currentState;

        private DjiSdkManager()
        {
            _workerThread = new Thread(CollectData);

            DJISDKManager.Instance.SDKRegistrationStateChanged += OnSdkRegistrationStateChanged;

            _workerThread.Start();
        }

        private async void CollectData()
        {
            var watch = Stopwatch.StartNew();

            for (; ; )
            {
                var elapsed = 0L;
                var sleepTime = 0;

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

                var attitude = await fcHandler.GetAttitudeAsync();
                var altitude = await fcHandler.GetAltitudeAsync();

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
                    currentState = new FlightState(altitudeValue, yaw, pitch, roll, flightStateError);

                    if (null != StateChanged)
                    {
                        // dispatch the state
                        StateChanged.Invoke(currentState);
                    }
                }                

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int) Math.Max(updateFrequence - elapsed, 0L);
                
                Thread.Sleep(sleepTime);
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
        }
    }
}
