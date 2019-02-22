using DJI.WindowsSDK;
using System.Threading;

namespace HDCircles.Hackathon
{
    public class PositionController
    {
        AltitudeController altitudeController;
        private const double Gain_p_alt = 1;
        private const double Gain_d_alt = 0.8;
        private double[] Control; 

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

        public double[] Update(double roll, double pitch, double yaw, double altitude, double vx, double vy, double vz)
        {
            // update altitude controller

            
             
            Control[0] = altitudeController.Update(altitude, vz);

            return Control;

        }
        public void Start(double roll, double pitch, double yaw, double altitude,double vx, double vy, double vz)
        {
            altitudeController.Start(altitude, altitude, vz);
        }

        public void SetCommand(double yaw, double altitude, double x, double y)
        {
            altitudeController.SetPoint = altitude;
        }
        //private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        //{
        //    //var isReg = errorCode == SDKError.NO_ERROR;

        //}

   
    }
}
