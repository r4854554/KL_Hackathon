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


    public class DroneController
    {
        public struct UdpState
        {
            public UdpClient u;
            public IPEndPoint e;
        }

        public Drone _drone;

        private const double STATETIMER_UPDATE_FREQUENCE = 100; // 10Hz

        private long updateInterval = 100L; // milliseconds
        private bool _isInitialised = false;
        IPEndPoint RemoteIpEndPoint;
        // Port number
        private const int _statePort = 11000;
        //Creates a UdpClient for reading incoming data.
        //private IPEndPoint receivingEndPoint = new IPEndPoint(IPAddress.Any, 0);
        
        UdpClient receivingUdpClient = new UdpClient(12000);
        UdpClient sendingUdpClient = new UdpClient("192.168.31.65", _statePort);

        public static CoreDispatcher Dispatcher { get; set; }

        private BackgroundWorker backgroundWorker;

        public DroneController()    
        {
            
            
             RemoteIpEndPoint = new IPEndPoint(IPAddress.Parse("192.168.31.65"), 15000);
            Debug.WriteLine("Info:DroneController: constructor");

            if (!_isInitialised)
            {
                Debug.WriteLine("Info:DroneController: initialised");

                // initialise drone instance
                _drone = new Drone();

                // add a background worker to perform regular tick
                backgroundWorker = new BackgroundWorker();
                backgroundWorker.DoWork += BackgroundWorker_Timing;
                backgroundWorker.RunWorkerAsync();

                _isInitialised = true;
            }

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


            double[] udpState = new double[10];

            var j = 0;
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

            //var data = receivingUdpClient.Receive(ref receivingEndPoint);
            //Debug.WriteLine("received");

            // receive 

            //Creates an IPEndPoint to record the IP Address and port number of the sender. 
            // The IPEndPoint will allow you to read datagrams sent from any source.
            //IPEndPoint RemoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);
            
            try
            {
                //Debug.WriteLine($"This is the message you received {receivingUdpClient.Available}");
                if (receivingUdpClient.Available > 0)
                {
                    
                    // Blocks until a message returns on this socket from a remote host.
                    Byte[] receiveBytes = receivingUdpClient.Receive(ref RemoteIpEndPoint);

                    double[] values = new double[receiveBytes.Length / 8];

                    Debug.WriteLine("This is the message you received 1");
                    Buffer.BlockCopy(receiveBytes, 0, values, 0, values.Length * 8);

                    //string returnData = Encoding.ASCII.GetString(receiveBytes);


                    Debug.WriteLine("This is the message you received " +
                                                 values[0].ToString());

                    Debug.WriteLine("This is the message you received " +
                                                 values[4].ToString());
                    Debug.WriteLine("This message was sent from " +
                                                RemoteIpEndPoint.Address.ToString() +
                                                " on their port number " +
                                                RemoteIpEndPoint.Port.ToString());
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
            }

            //
            DateTime localDate = DateTime.Now;
            //Debug.WriteLine($"Info:ControlLoop:{localDate.Millisecond:G} {udpState[0]} - yaw - {_drone.CurrentState.Yaw} pitch - {_drone.CurrentState.Pitch} roll - {_drone.CurrentState.Roll} altitude- {_drone.CurrentState.Altitude}");

        }

        //private void ProcessReceive(IAsyncResult result)
        //{
        //    var state = (UdpState)result.AsyncState;
        //    var u = state.u;
        //    var e = state.e;
        //    var data = u.EndReceive(result, ref e);

        //    var doubleValues = new double[data.Length / 8];

        //    Buffer.BlockCopy(data, 0, doubleValues, 0, data.Length);

        //    Debug.WriteLine(string.Join(",", doubleValues));

        //    receivingUdpClient.BeginReceive(ProcessReceive, state);
        //}
    }


}


