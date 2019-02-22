namespace HDCircles.Hackathon.Services
{
    using DJI.WindowsSDK;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseService
    {
        private object sdkLock = new object();

        /// <summary>
        /// indicates whether the Dji Windows SDK is registered.
        /// </summary>
        private bool _isSdkRegistered;

        /// <summary>
        /// indicates whether the Drone is connected.
        /// </summary>
        private bool _isDroneConnected;

        /// <summary>
        /// indicates whether the service loop is able to run.
        /// </summary>
        private bool _isLoopEnabled;

        /// <summary>
        /// the thread which run the service loop.
        /// </summary>
        private Thread _thread;

        protected BaseService()
        {
            DJISDKManager.Instance.SDKRegistrationStateChanged += DjiSdkManager_SDKRegistrationStateChanged;

            _thread = new Thread(Thread_TheLoop);
        }

        private void Thread_TheLoop()
        {
            var watch = Stopwatch.StartNew();

            for (; ; )
            {
            }
        }

        private void DjiSdkManager_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            lock (sdkLock)
            {
                _isSdkRegistered = state == SDKRegistrationState.Succeeded && errorCode == SDKError.NO_ERROR;

                if (_isSdkRegistered)
                {
                    var productHandler = DJISDKManager.Instance.ComponentManager.GetProductHandler(0);
                    
                    if (null != productHandler)
                    {
                        var isConnectedTask = productHandler.GetConnectionAsync();

                        isConnectedTask.Wait();

                        _isDroneConnected = (isConnectedTask.Result.value ?? default(BoolMsg)).value;
                        
                        productHandler.ConnectionChanged += ProductHandler_ConnectionChanged;
                    } 

                    // TODO: handle the case that product handler is not available,
                    // we need to check the connectivity between the drone and app at some point.
                }
            }
        }

        private void ProductHandler_ConnectionChanged(object sender, BoolMsg? value)
        {
            lock (sdkLock)
            {
                var oldValue = _isDroneConnected;

                _isDroneConnected = (value ?? default(BoolMsg)).value;
            }
        }

        /// <summary>
        /// the task the service executed repeatly.
        /// </summary>
        /// <returns></returns>
        protected abstract Task DoService();
    }
}
