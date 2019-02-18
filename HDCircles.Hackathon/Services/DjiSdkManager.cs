using DJI.WindowsSDK;

namespace HDCircles.Hackathon.Services
{
    public sealed class DjiSdkManager
    {
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
        }

        private async void OnSdkRegistrationStateChanged(SDKRegistrationState state, SDKError error)
        {
            if (SDKError.NO_ERROR != error)
            {
                
            }
        }
    }
}
