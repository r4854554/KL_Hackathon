using DJI.WindowsSDK;
using System.Threading;

namespace HDCircles.Hackathon
{
    public class PositionController
    {
        AltitudeController altitudeController;
        private const double Gain_p_alt = 1;
        private const double Gain_d_alt = 0.8;
        // the output of position controller - control to the drone
        private double throttleCmd = 0.0;
        public double ThrottleCmd { get; set; }

        private double rollCmd = 0.0;
        public double RollCmd { get; set; }

        private double pitchCmd = 0.0;
        public double PitchCmd { get; set; }

        private double yawCmd = 0.0;
        public double YawCmd { get; set; }

        // Controller setpoints
        private double altitudeSetpoint;  
        public double AltitudeSetpoint {
            get => altitudeSetpoint;
            set {
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
                yawSetpoint = value;
            }
        }

        public PositionController()
        {
            Init();
        }
        public void Init()
        {
           
            altitudeController = new AltitudeController(Gain_p_alt, Gain_d_alt);
        }

        public void Reset()
        {

        }


        public void Stop()
        {

        }

        public void Update(double roll, double pitch, double yaw, double altitude, double vx, double vy, double vz)
        {
            // update altitude controller



            throttleCmd = altitudeController.Update(altitude, vz);
            

        }
        public void Start(double roll, double pitch, double yaw, double altitude,double vx, double vy, double vz)
        {
            altitudeController.Start(altitude, altitude, vz);
        }

        public void SetAllCommand(double yaw, double altitude, double relativeX, double relativeY)
        {
            AltitudeSetpoint = altitude;
            YawSetpoint = yaw;


        }
        //private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        //{
        //    //var isReg = errorCode == SDKError.NO_ERROR;

        //}

   
    }
}
