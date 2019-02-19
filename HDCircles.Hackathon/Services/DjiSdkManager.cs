using DJI.WindowsSDK;
using System;

namespace HDCircles.Hackathon.Services
{
    public struct FlightState
    { }

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

        private int updateFrequence = 50; // milliseconds

        private DjiSdkManager()
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += OnSdkRegistrationStateChanged;
            DJISDKManager.Instance.ComponentManager.GetFlightControllerHandler(0, 0).AttitudeChanged += DjiSdkManager_AttitudeChanged;
        }

        private void DjiSdkManager_AttitudeChanged(object sender, Attitude? value)
        {
            //

            if (StateChanged != null)
            {
                StateChanged.Invoke(new FlightState());
            }
        }

        private async void OnSdkRegistrationStateChanged(SDKRegistrationState state, SDKError error)
        {
            if (SDKError.NO_ERROR != error)
            {
                
            }
        }
    }
}
