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

namespace HDCircles.Hackathon.Services
{
    public class DroneController
    {
        public Drone _drone;        

        private const double STATETIMER_UPDATE_FREQUENCE = 100; // 10Hz

        private long updateInterval = 100L; // milliseconds
        private bool _isInitialised = false;
        public static CoreDispatcher Dispatcher { get; set; }

        private BackgroundWorker backgroundWorker;

        public DroneController()
        {

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

            for (; ; )
            {
                watch.Reset();
                watch.Start();

                if ( !_drone._isSdkRegistered )
                {
                    watch.Stop();

                    elapsed = watch.ElapsedMilliseconds;
                    sleepTime = sleepTime = (int)Math.Max(updateInterval - elapsed, 0L);

                    Thread.Sleep(sleepTime);
                    continue;
                } else
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


            DateTime localDate = DateTime.Now;            
            Debug.WriteLine($"Info:ControlLoop:{localDate.Millisecond:G} - yaw - {_drone.CurrentState.Yaw} pitch - {_drone.CurrentState.Pitch} roll - {_drone.CurrentState.Roll} altitude- {_drone.CurrentState.Altitude}");

        }
    }


}


