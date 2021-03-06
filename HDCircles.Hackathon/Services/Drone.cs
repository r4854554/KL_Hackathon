﻿using DJI.WindowsSDK;
using DJI.WindowsSDK.Components;
using DJIVideoParser;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
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
        public double Vx { get; }
        public double Vy { get; }
        public double Vz { get; }
        public bool IsFlying { get; }
        public SDKError Error { get; }

        public FlightState(double altitude, double yaw, double pitch, double roll, 
            double vx, double vy, double vz, bool isFlyging, SDKError error)
        {
            Altitude = altitude;
            Yaw = yaw;
            Pitch = pitch;
            Roll = roll;
            Vx = vx;
            Vy = vy;
            Vz = vz;
            IsFlying = isFlyging;


            Error = error;
        }
    }

    public struct LiveFrame
    {
        public byte[] Data { get; }
        public int Width { get; }
        public int Height { get; }

        public LiveFrame(byte[] data, int width, int height)
        {
            Data = data;
            Width = width;
            Height = height;
        }
    }

    public delegate void StateChangedHandler(FlightState state);

    public sealed class Drone
    {
        private uint PRODUCT_ID = 0;
        private uint PRODUCT_INDEX = 0;
        public event StateChangedHandler StateChanged;

        private static DJISDKManager _sdkManager;

        private object takeofflock = new object();
        private object landinglock = new object();
        private bool _isLanding; 
        // public state that is not update from the SDK
        public bool IsLanding {
            get
            {
                lock (landinglock)
                {
                    return _isLanding;
                }
            }
            set
            {
                lock (landinglock)
                {
                    _isLanding = value;
                }
            }
        }
        private bool _isTakeOffFinish;
        public bool IsTakeOffFinish {
            get
            {
                lock (takeofflock)
                {
                    return _isTakeOffFinish;
                }
            }
            set
            {
                lock (takeofflock)
                {
                    _isTakeOffFinish = value;
                }
            }
        }


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

        /// <summary>
        /// State Update Frequence
        /// </summary>
        private long updateFrequence = 100L; // milliseconds
                
        private object _stateLock = new object();

        private object _frameLock = new object();

        private Parser VideoParser;

        private byte[] frameBuffer;

        private int frameWidth;

        private int frameHeight;

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
        public FlightState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }

        private static Drone _instance;
        public static Drone Instance
        {
            get
            {
                if (null == _instance)
                    _instance = new Drone();

                return _instance;
            }
        }

        private Drone()
        {
            IsLanding = false;
            //IsTakingOff = false;
            backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += BackgroundWorker_DoWork;

            //_workerThread = new Thread(BackgroundWorker_DoWork);
            VideoParser = new Parser();
            DJISDKManager.Instance.SDKRegistrationStateChanged += OnSdkRegistrationStateChanged;

            //_workerThread.Start();
            backgroundWorker.RunWorkerAsync();
        }

        public LiveFrame GetLiveFrame()
        {
            byte[] buffer;
            int width, height;


            lock (_frameLock)
            {
                if (frameWidth == 0 || frameHeight == 0)
                    return default(LiveFrame);

                buffer = new byte[frameWidth * frameHeight * 4];

                frameBuffer.CopyTo(buffer.AsBuffer());

                width = frameWidth;
                height = frameHeight;
            }

            var liveFrame = new LiveFrame(buffer, width, height);

            return liveFrame;
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

                Thread.Sleep(sleepTime);
            }
        }

        private async Task CollectData()
        {
            // Call the methods to get the values
            var attitude = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAttitudeAsync();
            var altitude = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetAltitudeAsync();
            var velocity = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetVelocityAsync();
            var isFlying = await DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).GetIsFlyingAsync();

            var flightStateError = attitude.error;
            var yaw = 0.0;
            var pitch = 0.0;
            var roll = 0.0;
            var altitudeValue = 0.0;
            var vx = 0.0;
            var vy = 0.0;
            var vz = 0.0;
            var isFlyingState = false;

            if (attitude.error == SDKError.NO_ERROR)
            {
                yaw = attitude.value.Value.yaw; 
                pitch = attitude.value.Value.pitch;
                roll = attitude.value.Value.roll;
                
            }


            if (altitude.error == SDKError.NO_ERROR)
            {
                altitudeValue = altitude.value.Value.value;
            }


            if (velocity.error == SDKError.NO_ERROR)
            {
                vx = velocity.value.Value.x; 
                vy = velocity.value.Value.y;
                vz = velocity.value.Value.z;
            }

            if (isFlying.error == SDKError.NO_ERROR)
            {
                isFlyingState = isFlying.value.Value.value;
            }

            lock (_stateLock)
            {
                _currentState = new FlightState(altitudeValue, yaw, pitch, roll,vx, vy, vz, isFlyingState, flightStateError);
            
                if (null != StateChanged)
                {
                    // dispatch the state
                    StateChanged.Invoke(_currentState);
                }

                Logger.Instance.Log($"yaw: {yaw} pitch: {pitch} roll: {roll} altitude: {altitudeValue}");
            }
        }

        private void OnSdkRegistrationStateChanged(SDKRegistrationState state, SDKError error)
        {
            _isSdkRegistered = SDKError.NO_ERROR == error && state == SDKRegistrationState.Succeeded;

            if (_isSdkRegistered)
            {
                _isWorkerEnabled = true;
                ConfigDroneAsync();
                fcHandler = DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0);
                
                var videoFeeder = DJISDKManager.Instance.VideoFeeder.GetPrimaryVideoFeed(0);
                var cameraHandler = DJISDKManager.Instance.ComponentManager.GetCameraHandler(0, 0);

                if (null != videoFeeder)
                {
                    VideoParser = new Parser();
                    VideoParser.Initialize((byte[] data) =>
                    {
                        return DJISDKManager.Instance.VideoFeeder.ParseAssitantDecodingInfo(0, data);
                    });
                    VideoParser.SetSurfaceAndVideoCallback(0, 0, null, OnFrameParsed);

                    videoFeeder.VideoDataUpdated += Drone_VideoDataUpdated; ;
                }

                if (null != cameraHandler)
                {
                    var res = cameraHandler.GetCameraTypeAsync();

                    res.Wait();

                    if (res.Result.error == SDKError.NO_ERROR)
                    {
                        Drone_CameraTypeChanged(null, res.Result.value);
                    }

                    cameraHandler.CameraTypeChanged += Drone_CameraTypeChanged; ;
                }
            }
            else
            {
                _isWorkerEnabled = false;
                fcHandler = null;
            }

            Thread.Sleep(300);
        }

        private void Drone_CameraTypeChanged(object sender, CameraTypeMsg? value)
        {
            if (value.HasValue)
            {
                var acCamType = AircraftCameraType.Others;

                switch (value.Value.value)
                {
                    case CameraType.MAVIC_2_PRO:
                        acCamType = AircraftCameraType.Mavic2Pro;
                        break;
                    case CameraType.MAVIC_2_ZOOM:
                        acCamType = AircraftCameraType.Mavic2Zoom;
                        break;
                }

                if (null != VideoParser)
                {
                    VideoParser.SetCameraSensor(acCamType);
                }
            }
        }

        private void Drone_VideoDataUpdated(VideoFeed sender, byte[] bytes)
        {
            if (null != VideoParser)
            {
                VideoParser.PushVideoData(0, 0, bytes, bytes.Length);
            }
        }

        private void OnFrameParsed(byte[] data, int width, int height)
        {
            lock (_frameLock)
            {
                if (null == frameBuffer)
                {
                    frameBuffer = data;
                }
                else
                {
                    if (frameBuffer.Length != data.Length)
                    {
                        Array.Resize(ref frameBuffer, data.Length);
                    }

                    data.CopyTo(frameBuffer.AsBuffer());
                }

                frameWidth = width;
                frameHeight = height;
            }
        }

        #region Methods
        public void ResetJoystick()
        {
            if (null != DJISDKManager.Instance)
                DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(0f, 0f, 0f, 0f);

            Task.Delay(20);
        }

        public void SetJoystick(float throttle, float yaw, float pitch, float roll)
        {
            if (null != DJISDKManager.Instance)
                DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(throttle, yaw, pitch, roll);

            
        }
        public async void EmergencyLanding()
        {
            if (null != DJISDKManager.Instance)
            {
                ResetJoystick();

                var SDKErrorCode = await fcHandler.StartAutoLandingAsync();
                if (SDKErrorCode == SDKError.NO_ERROR)
                {

                    Drone.Instance.IsLanding = true;
                    //Drone.Instance.IsTakingOff = false;

                }
                var isFlyingResult = await fcHandler.GetIsFlyingAsync();
                if (isFlyingResult.value.HasValue)
                {
                    var isFlying = isFlyingResult.value.Value.value;
                    while (isFlying)
                    {
                        await fcHandler.StartAutoLandingAsync();
                        var confirmationNeeded = await fcHandler.GetIsLandingConfirmationNeededAsync();
                        if (confirmationNeeded.value.HasValue)
                        {
                            await fcHandler.ConfirmLandingAsync();
                        }
                        isFlyingResult = await fcHandler.GetIsFlyingAsync();
                        if (isFlyingResult.value.HasValue) { isFlying = isFlyingResult.value.Value.value; }

                    }
                }
            }
            
        }

        public async Task<SDKError> TakeOff()
        {
            SDKError result = SDKError.UNKNOWN;
            if (null != DJISDKManager.Instance)
            {
                // start take off
                result = await fcHandler.StartTakeoffAsync();
                // check 
                if (result == SDKError.NO_ERROR)
                {
                    var TakeoffAlt = 1.18; // [m]
                    // take off command send
                    bool achieveTakeOffHeight = Drone.Instance.CurrentState.Altitude > TakeoffAlt;
                    while (!achieveTakeOffHeight)
                    {
                        Thread.Sleep(10);
                        achieveTakeOffHeight = Drone.Instance.CurrentState.Altitude > TakeoffAlt;
                        result = await fcHandler.StartTakeoffAsync();
                    }
                    Debug.Print("Take off finish")
;                   Drone.Instance.IsTakeOffFinish = true;
                } else
                {
                    // start take off fail
                    
                }
            }
            return result;

        }
        public async Task<bool> ConfigDroneAsync()
        {
            // set flight ceiling !! not useful, minimum is 20m
            //IntMsg ceiling;
            //ceiling.value = 2; 
            //GetFlightControllerHandler().SetHeightLimitAsync(ceiling);
            FCFailsafeActionMsg actionMsg;
            actionMsg.value = FCFailsafeAction.LANDING;
            //var value = new GimbalAngleRotation();
            //value.pitch = -16.9;
            //DJISDKManager.Instance.VirtualRemoteController.UpdateJoystickValue(0, 0, 0, 0);
            //if (null != DJISDKManager.Instance)
            //{
            //    var error = await GetGimbalHandler().RotateByAngleAsync(new GimbalAngleRotation
            //    {
            //        mode = GimbalAngleRotationMode.RELATIVE_ANGLE,
            //        pitch = -16.9,
            //        roll = 0,
            //        yaw = 0,
            //        pitchIgnored = false,
            //        rollIgnored = true,
            //        yawIgnored = true,
            //        duration = 0.3
            //    });
            //    Debug.Print(error.ToString());
            //}
            await GetFlightControllerHandler().SetFailsafeActionAsync(actionMsg);

            return true;
        }

        #endregion Methods

        #region Handler
        WiFiHandler GetWifiHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetWiFiHandler(PRODUCT_ID, PRODUCT_INDEX);
        }


        FlightControllerHandler GetFlightControllerHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        CameraHandler GetCameraHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetCameraHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        GimbalHandler GetGimbalHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetGimbalHandler(PRODUCT_ID, PRODUCT_INDEX);
        }

        BatteryHandler GetBatteryHandler()
        {
            return DJISDKManager.Instance.ComponentManager.GetBatteryHandler(PRODUCT_ID, PRODUCT_INDEX);
        }
        #endregion

    }

}
