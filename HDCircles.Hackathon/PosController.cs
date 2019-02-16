﻿using DJI.WindowsSDK;
using System.Threading;

namespace HDCircles.Hackathon
{
    public class PosController
    {
        private Thread _thread;

        public bool Init()
        {
            // get initial state

            // load parameters
            // 


            // resolve frames if needed

            // initial PID controllers


            return true;
        }
        void Reset()
        {

        }
        
        public PosController()
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
