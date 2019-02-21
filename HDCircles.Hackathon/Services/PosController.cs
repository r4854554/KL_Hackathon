using DJI.WindowsSDK;
using System.Threading;

namespace HDCircles.Hackathon
{
    public class PosController
    {
        

        public PosController()
        {
           
        }

        public void Start()
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            var isReg = errorCode == SDKError.NO_ERROR;

        }

        private void Thread_Run()
        {

        }
    }
}
