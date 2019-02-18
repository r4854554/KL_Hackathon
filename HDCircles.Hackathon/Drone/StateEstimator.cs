using DJI.WindowsSDK;
using System.Threading;

namespace HDCircles.Hackathon
{
    public class StateEstimator
    {
        private Thread _thread;

        public StateEstimator()
        {
            _thread = new Thread(Thread_Run);
        }

        public void Start()
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            var isReg = errorCode == SDKError.NO_ERROR;

            _thread.Start();
        }

        private void Thread_Run()
        {

        }
    }
}
