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
        private const double Gain_p_alt = 0.8;
        private const double Gain_d_alt = 0.5;
        private const double Gain_p_yaw = 0.01;
        private const double Gain_i_yaw = 0.001;
        private const double Gain_d_yaw = 0;

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
            get => altitudeSetpoint;
            set {
                Debug.Print($"Info:Altitude Setpoint chainged: {value}");
                altitudeSetpoint = value;
                altitudeController.SetPoint = altitudeSetpoint;
            }
        }

        private double relativeXSetpoint = 0.0;
        public double RelativeXSetpoint
        {
            get => relativeXSetpoint;
            set
            {
                relativeXSetpoint = value;
            }
        }

        private double relativeYSetpoint = 0.0;
        public double RelativeYSetpoint
        {
            get => relativeYSetpoint;
            set
            {
                relativeYSetpoint = value;

            }
        }

        private double relativeZSetpoint;
        public double RelativeZSetpoint
        {
            get => relativeZSetpoint;
            set
            {
                relativeZSetpoint = value;

            }
        }
        private double yawSetpoint;
        public double YawSetpoint {
            get => yawSetpoint;
            set {
                Debug.Print($"Info:YawSetpoint changed: {value}");
                
                yawSetpoint = value;
                yawController.SetPoint = yawSetpoint;
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
            
        }
        public void Start(double roll, double pitch, double yaw, double altitude,double vx, double vy, double vz)
        {
            double defaultTakeoffAltitude = 1.2;
            altitudeController.Start(defaultTakeoffAltitude, altitude, vz);
            yawController.Start(0.0, 0.0, 0.0);

        }

        public void SetAltitudeStepCommand(double step)
        {
            Debug.Print($"Info:SetAltitudeStepCommand: {step}");
            AltitudeSetpoint = AltitudeSetpoint + step;
        }

    }
}
