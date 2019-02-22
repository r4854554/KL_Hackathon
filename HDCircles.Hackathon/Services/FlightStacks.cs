﻿
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
//using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;

using System.ComponentModel;
using System.Threading;
using DJI.WindowsSDK;
using System.Net.Sockets;
using System.Net;

namespace HDCircles.Hackathon.Services
{
    public class FlightStacks
    {
        public struct UdpState
        {
            public UdpClient u;
            public IPEndPoint e;
        }

        public Drone _drone;
        public PositionController _positionController;

        private const double STATETIMER_UPDATE_FREQUENCE = 100; // 10Hz

        private long updateInterval = 100L; // milliseconds
        private bool _isInitialised = false;

        private bool _isStarted = false;
        IPEndPoint RemoteIpEndPoint;
        // Port number
        private const int _statePort = 11000;
        //Creates a UdpClient for reading incoming data.
        //private IPEndPoint receivingEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        //UdpClient receivingUdpClient = new UdpClient(12000);
        UdpClient sendingUdpClient = new UdpClient("127.0.0.1", _statePort);

        public static CoreDispatcher Dispatcher { get; set; }

        private BackgroundWorker backgroundWorker;

        public FlightStacks()    
        {
            RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.31.65"), 15000);

            Debug.WriteLine("Info:DroneController: constructor");

            if (!_isInitialised)
            {
                Debug.WriteLine("Info:DroneController: initialised");

                // initialise drone instance
                _drone = Drone.Instance;

                // init position controller
                _positionController = new PositionController();
                _positionController.Init();

                // add a background worker to perform regular tick
                backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += BackgroundWorker_Timing;
                backgroundWorker.RunWorkerAsync();

                _isInitialised = true;
            }

        }

        private void Start()
        {
            _positionController.Start(_drone.CurrentState.Roll, _drone.CurrentState.Pitch, _drone.CurrentState.Yaw,
                _drone.CurrentState.Altitude, _drone.CurrentState.Vx, _drone.CurrentState.Vy, _drone.CurrentState.Vz);

            _isStarted = true;

        }

        private void BackgroundWorker_Timing(object sender, DoWorkEventArgs e)
        {
            var watch = Stopwatch.StartNew();

            var elapsed = 0L;
            var sleepTime = 0;

            //var receiveState = new UdpState();

            //receiveState.u = receivingUdpClient;
            //receiveState.e = receivingEndPoint;

            //receivingUdpClient.Connect(receivingEndPoint);
            //receivingUdpClient.BeginReceive(ProcessReceive, receiveState);

            for (; ; )
            {
                watch.Reset();
                watch.Start();
                //Debug.WriteLine($"Info:ControlLoop:Collect Data thread id: {Thread.CurrentThread.ManagedThreadId} {_drone._isSdkRegistered}");
                if (!_drone._isSdkRegistered)
                {
                    watch.Stop();

                    elapsed = watch.ElapsedMilliseconds;
                    sleepTime = sleepTime = (int)Math.Max(updateInterval - elapsed, 0L);

                    Thread.Sleep(sleepTime);
                    continue;
                }
                else
                {
                    // reset?
                }

                ControlLoop().Wait();

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(updateInterval - elapsed, 0L);

                //Debug.WriteLine($"Background thread id: {Thread.CurrentThread.ManagedThreadId}");
                //Debug.WriteLine("elapsed: " + watch.Elapsed.TotalMilliseconds);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task ControlLoop()
        {
            //Debug.WriteLine($"Info:ControlLoop:Collect Data thread id: {Thread.CurrentThread.ManagedThreadId}");
            
            // Safetyguad to prevent drone go crazy 
            if (_drone.CurrentState.Altitude>2.5)
            {
                Debug.Print("Info:Emergency");

                _drone.EmergencyLanding();
            }

            // check start condition
            if (_drone._isSdkRegistered)
            {
                Start();
            }
            
            // get setpoint
            

            // only update afte start the whole controller
            if (_isStarted)
            {
                // update postion controller
                _positionController.Update(_drone.CurrentState.Roll, _drone.CurrentState.Pitch, _drone.CurrentState.Yaw,
                    _drone.CurrentState.Altitude, _drone.CurrentState.Vx, _drone.CurrentState.Vy, _drone.CurrentState.Vz);
                // update drone control
                _drone.SetJoystick((float)_positionController.ThrottleCmd,
                    (float)_positionController.YawCmd, (float)_positionController.PitchCmd, (float)_positionController.RollCmd);
            }


            DateTime localDate = DateTime.Now;
            Debug.WriteLine($"Info:ControlLoop:{localDate.Millisecond:G} " +
                $"|yaw - {_drone.CurrentState.Yaw} pitch - {_drone.CurrentState.Pitch} roll - {_drone.CurrentState.Roll} z- {_drone.CurrentState.Altitude}"
                + $"\t|Vx - {_drone.CurrentState.Vx} pitch - {_drone.CurrentState.Vx} Vy - {_drone.CurrentState.Vy} Vz- {_drone.CurrentState.Vz}" 
                + $"");

        }

        public void SendUdpDebug()
        {
            double[] udpState = new double[10];


            //Byte[] sendBytes = Encoding.ASCII.GetBytes("Is anybody there");
            udpState[0] = (int)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
            udpState[1] = _drone.CurrentState.Roll;
            udpState[2] = _drone.CurrentState.Pitch;
            udpState[3] = _drone.CurrentState.Yaw;
            udpState[4] = _drone.CurrentState.Altitude;
            udpState[9] = (double)_statePort;

            Byte[] sendBytes = udpState.SelectMany(value => BitConverter.GetBytes(value)).ToArray();

            try
            {
                sendingUdpClient.Send(sendBytes, sendBytes.Length);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }
        }

        // spare code for udp send
        //try
        //{
        //    //Debug.WriteLine($"This is the message you received {receivingUdpClient.Available}");
        //    if (receivingUdpClient.Available > 0)
        //    {

        //        // Blocks until a message returns on this socket from a remote host.
        //        Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);

        //        double[] values = new double[receiveBytes.Length / 8];

        //        Debug.WriteLine("This is the message you received 1");
        //        Buffer.BlockCopy(receiveBytes, 0, values, 0, values.Length * 8);

        //        //string returnData = Encoding.ASCII.GetString(receiveBytes);


        //        Debug.WriteLine("This is the message you received " +
        //                                     values[0].ToString());

        //        Debug.WriteLine("This is the message you received " +
        //                                     values[4].ToString());
        //        Debug.WriteLine("This message was sent from " +
        //                                    RemoteIpEndPoint.Address.ToString() +
        //                                    " on their port number " +
        //                                    RemoteIpEndPoint.Port.ToString());
        //    }
        //}
        //catch (Exception e)
        //{
        //    Debug.WriteLine(e.ToString());
        //}

    }


}


