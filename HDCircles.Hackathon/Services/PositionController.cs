using DJI.WindowsSDK;
using System;
using System.Diagnostics;
using System.Threading;

namespace HDCircles.Hackathon
{
    public class PositionController
    {
        AltitudeController altitudeController;
        YawController yawController;
        LateralController lateralController;
        private const double Gain_p_alt = 0.8;
        private const double Gain_d_alt = 0.5;
        private const double Gain_p_yaw = 0.05;
        private const double Gain_i_yaw = 0.0001;
        private const double Gain_d_yaw = 0;

        // lock for thread safe set
        private object _altitudeSetpointLock = new object();
        private object _yawSetpointLock = new object();
        private object _relativeXSetpointLock = new object();
        private object _relativeYSetpointLock = new object();


        // the output of position controller - control to the drone
        //private double throttleCmd = 0.0;
        public double ThrottleCmd { get; private set; } = 0;
        //private double rollCmd = 0.0;
        public double RollCmd { get; private set; } = 0;

        //private double pitchCmd = 0.0;
        public double PitchCmd { get; private set; } = 0;

        //private double yawCmd = 0.0;
        public double YawCmd { get; private set; } = 0;

        // Controller setpoints
        private double altitudeSetpoint;  
        public double AltitudeSetpoint {
            get
            {
                lock (_altitudeSetpointLock)
                {
                    return altitudeSetpoint;
                }
                
            }
            set {
                Debug.Print($"Info:Altitude Setpoint chainged: {value}");
                lock (_altitudeSetpointLock)
                {
                    altitudeSetpoint = value;
                }
                altitudeController.SetPoint = altitudeSetpoint;
            }
        }

        private double relativeXSetpoint = 0.0;
        public double RelativeXSetpoint
        {
            get
            {
                lock (_relativeXSetpointLock)
                {
                    return relativeXSetpoint;
                }

            }
            set
            {
                lock (_relativeXSetpointLock)
                {
                    relativeXSetpoint = value;
                }
            }
        }

        private double relativeYSetpoint = 0.0;
        public double RelativeYSetpoint
        {
            get
            {
                lock (_relativeYSetpointLock)
                {
                    return relativeYSetpoint;
                }

            }
            set
            {
                lock (_relativeYSetpointLock)
                {
                    relativeYSetpoint = value;
                }
            }
        }

        //private double relativeZSetpoint;
        //public double RelativeZSetpoint
        //{
        //    get => relativeZSetpoint;
        //    set
        //    {
        //        relativeZSetpoint = value;

        //    }
        //}
        private double yawSetpoint;
        public double YawSetpoint {
            get
            {
                lock (_yawSetpointLock)
                {
                    return yawSetpoint;
                }

            }
            set
            {
                Debug.Print($"Info:YawSetpoint changed: {value}");
                lock (_yawSetpointLock)
                {
                    yawSetpoint = value;
                }
                yawController.SetPoint = yawSetpoint;
            }
           
        }

        // LeteralController
        public bool RightSide { get; set; } = true;
        private double _targetIndex; 
        public double TargetIndex
        {
            get
            {
                lock (indexLock)
                {
                    return _targetIndex;
                }
            }
            set
            {
                lock (indexLock)
                {
                    _targetIndex = value;
                    Debug.Print($"Info:SetLateralIndexCommand: {value}");

                }
            }
        } 

        private object indexLock = new object();

        private double _currentIndex;
        public double CurrentIndex
        {
            get
            {
                lock (indexLock)
                {
                    return _currentIndex;
                }
            }
            set
            {
                lock (indexLock)
                {
                    _currentIndex = value; 

                }
            }
        }

        // constructor
        private static PositionController _instance;
        public static PositionController Instance
        {
            get
            {
                if (null == _instance)
                    _instance = new PositionController();

                return _instance;
            }
        }

        private PositionController()
        {
            Init();
        }
        public void Init()
        {
            altitudeController = new AltitudeController(Gain_p_alt, Gain_d_alt);
            yawController = new YawController(Gain_p_yaw, Gain_i_yaw, Gain_d_yaw);
            lateralController = new LateralController();
        }

        public void Reset()
        {

        }


        public void Stop()
        {

        }
        /// <summary>
        /// The controller output
        /// </summary>
        /// <param name="timeSinceLastUpdate">timespan of the elapsed time
        /// since the previous time that ControlVariable was called</param>
        /// <returns>Value of the variable that needs to be controlled</returns>
        public void Update(double dt, double roll, double pitch, double yaw, double altitude, double vx, double vy, double vz)
        {
          // Get feedback
          // Update altitude controller
            ThrottleCmd = altitudeController.Update(altitude, vz);
            double falseRate = 0.0;
            YawCmd = yawController.Update(yaw, falseRate);
            // update lateralController

            RollCmd = 0;
            lateralController.Update(CurrentIndex, TargetIndex, RightSide);
            //Debug.Print($"RollCmd: { }");
        }
        public void Start(double roll, double pitch, double yaw, double altitude,double vx, double vy, double vz)
        {
            double defaultTakeoffAltitude = 1.2;
            altitudeController.Start(defaultTakeoffAltitude, altitude, vz);
            AltitudeSetpoint = defaultTakeoffAltitude;
            yawController.Start(yaw, yaw, 0.0);
            YawSetpoint = yaw;
            lateralController.Start();
        }

        public void SetAltitudeStepCommand(double step)
        {
           
           AltitudeSetpoint = AltitudeSetpoint + step; 
        }

        public void SetYawStepCommand(double step)
        {
            //Debug.Print($"Info:SetYawStepCommand: {step}");
            YawSetpoint = YawSetpoint + step; 
        }

    }
}
