using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI.Core;

using System.ComponentModel;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace HDCircles.Hackathon.Services
{
    public class FlightStacks
    {
        /// <summary>
        /// Typical way to create the static class instacne, so all other class can access this unique object instance
        /// </summary>

        private static FlightStacks _instance;
        public static FlightStacks Instance
        {
            get
            {
                if (null == _instance)
                    _instance = new FlightStacks();

                return _instance;
            }
        }

        // Flgiht stacts inclues below components
        public Drone _drone;
        public PositionController _positionController;

        // constant parameter
        private const double STATETIMER_UPDATE_FREQUENCE = 100; // 10Hz
        private long updateInterval = 100L;                     // milliseconds
        private double ControlValueDeadzone = 0.05;     // any control less this value will be ignored

        // flags
        private bool _isInitialised = false;
        private bool _isMissionDone = false;
        private bool _isStarted = false;


        // background worker
        private BackgroundWorker backgroundWorker;

        private FlightStacks()
        {
            Debug.WriteLine("Info:DroneController: constructor");

            if (!_isInitialised)
            {
                Init();
                _isInitialised = true;
            }

        }
        private void Init()
        {
            if (!_isInitialised)
            {
                Debug.WriteLine("Info:DroneController: initialised");

                // initialise flight stacks components
                _drone = Drone.Instance;
                _positionController = PositionController.Instance;

                var commander = Commander.Instance;

                // add a background worker to perform regular tick
                backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += BackgroundWorker_Timing;
                backgroundWorker.RunWorkerAsync();

                _isInitialised = true;
            }
        }


        private void Start()
        {
            // only start when it is flying
            _positionController.Start(_drone.CurrentState.Roll, _drone.CurrentState.Pitch, _drone.CurrentState.Yaw,
                _drone.CurrentState.Altitude, _drone.CurrentState.Vx, _drone.CurrentState.Vy, _drone.CurrentState.Vz);

            _isStarted = true;

        }

        private void BackgroundWorker_Timing(object sender, DoWorkEventArgs e)
        {
            // Maintain the timing to makse sure a 10 Hz cycle for the flight stack
            var watch = Stopwatch.StartNew();

            var elapsed = 0L;
            var sleepTime = 0;

            for (; ; )
            {
                watch.Reset();
                watch.Start();
                //Debug.WriteLine($"Info:ControlLoop:Collect Data thread id: {Thread.CurrentThread.ManagedThreadId} {_drone._isSdkRegistered}");
                if (!_drone._isSdkRegistered)
                {
                    watch.Stop();

                    elapsed = watch.ElapsedMilliseconds;
                    sleepTime = sleepTime = (int)Math.Max(updateInterval - elapsed, 0L);

                    Thread.Sleep(sleepTime);
                    continue;
                }
                else
                {
                    // reset?
                }

                ControlLoop((double)updateInterval).Wait();

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(updateInterval - elapsed, 0L);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task ControlLoop(double updateIntervalInSeconds)
        {
            //Debug.WriteLine($"Info:ControlLoop:Collect Data thread id: {Thread.CurrentThread.ManagedThreadId}");

            // Safetyguad to prevent drone go crazy 
            if (_drone.CurrentState.Altitude > 2)
            {
                Debug.Print("Info:Emergency");

                _drone.EmergencyLanding();
            }

            // Need to deicid when to start controllercheck start condition
            if (_drone._isSdkRegistered && !_isStarted && _drone.CurrentState.Altitude>1.19)
            {
                Start();
                _isStarted = true;

            }

            // Setpoint: set point is 

            //if (!_isMissionDone && _drone._isSdkRegistered)
            //{
            //    var watch = Stopwatch.StartNew();

            //    var elapsed = 0L;
            //    var sleepTime = 0;

            //    watch.Reset();
            //    watch.Start();


            //    elapsed = watch.ElapsedMilliseconds;

            //    watch.Stop();

            //}


            // Update: only update afte start and the drone is flying, and the drone is not landing
            //      Update the current process variable 
            //      Update the crrent time
            //      Update the control variable  
            if (_isStarted && _drone.CurrentState.IsFlying && !_drone.IsLanding && _drone.IsTakeOffFinish)
            {

                // update postion controller
                _positionController.Update(updateIntervalInSeconds,
                    _drone.CurrentState.Roll,
                    _drone.CurrentState.Pitch,
                    _drone.CurrentState.Yaw,
                    _drone.CurrentState.Altitude,
                    _drone.CurrentState.Vx,
                    _drone.CurrentState.Vy,
                    _drone.CurrentState.Vz);

                // update drone control
                // if the control value is less than 0.05, ignore it 
                float throttleCmd = (float)DeadzoneControlValue(_positionController.ThrottleCmd, ControlValueDeadzone);
                float rollCmd = (float)DeadzoneControlValue(_positionController.RollCmd, ControlValueDeadzone);
                float pitchCmd = (float)DeadzoneControlValue(_positionController.PitchCmd, ControlValueDeadzone);
                float yawCmd = (float)DeadzoneControlValue(_positionController.YawCmd, ControlValueDeadzone);

                //if (throttleCmd!=0f || rollCmd != 0f || pitchCmd != 0f || yawCmd != 0f)
                //{
                    _drone.SetJoystick(throttleCmd,yawCmd, pitchCmd, rollCmd);
                    //Debug.WriteLine($"Info:ControlLoop: {throttleCmd} ");
                //} 
               
                DateTime localDate = DateTime.Now;;
                //Debug.WriteLine($"Info:ControlLoop:{localDate.Millisecond:G} " +
                //    $"|yaw - {_drone.CurrentState.Yaw} pitch - {_drone.CurrentState.Pitch} roll - {_drone.CurrentState.Roll} z- {_drone.CurrentState.Altitude}"
                //    + $"\t|Vx - {_drone.CurrentState.Vx} pitch - {_drone.CurrentState.Vx} Vy - {_drone.CurrentState.Vy} Vz- {_drone.CurrentState.Vz}"
                //    + $"");

            }


        }

        private double DeadzoneControlValue(double value, double threshold)
        {

            if (Math.Abs(value) < threshold)
            {
                return 0f;
            }
            else
            {
                return value;
            }

        }

        

    }


}


