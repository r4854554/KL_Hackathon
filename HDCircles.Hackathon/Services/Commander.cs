﻿using Catel;
using DJI.WindowsSDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace HDCircles.Hackathon.Services
{
    public enum MissionType
    {
        SetPoint = 1,
        StockTaking,
        TakeOff,
        Landing,
        Emergency
    }

    public struct MissionArgs
    {
        public float Yaw { get; }

        public float Altitude { get; }

        public float RelativeX { get; }

        public float RelativeY { get; }

        public MissionArgs(float yaw, float altitude, float relativeX, float relativeY)
        {
            Yaw = yaw;
            Altitude = altitude;
            RelativeX = relativeX;
            RelativeY = relativeY;
        }
    }

    public class Mission
    {
        /// <summary>
        /// Mission ID, for reference only
        /// </summary>
        public int Id { get; set; }

        public int ThreadId { get; set; }

        public bool? Result { get; set; }

        public MissionType Type { get; set; }
        
        public MissionArgs Args { get; set; }

        protected Func<Task> _task;

        protected Func<Task> Task => _task;

        protected virtual Func<bool> IsCompleted { get; }

        /// <summary>
        /// the time when this mission start.
        /// </summary>
        public long StartTimestamp { get; }

        /// <summary>
        /// 
        /// </summary>
        public double Timeout { get; protected set; }

        private System.Timers.Timer timer;

        private Task missionTaskObj;

        private void Timer_Timeout(object sender, ElapsedEventArgs args)
        {
            Result = false;
            timer.Stop();
        }

        public void Start()
        {
            Argument.IsNotNull(() => Task);

            if (StartTimestamp > 0)
            {
                return;
            }

            try
            {

                missionTaskObj = Task();
                missionTaskObj.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"mission {Id} - {Type}: {ex.ToString()}");
            }

            //timer = new System.Timers.Timer(Timeout);
            //timer.Elapsed += Timer_Timeout;
            //timer.Start();
        }

        public bool CheckIfCompleted()
        {
            Argument.IsNotNull(() => IsCompleted);
            
            Debug.WriteLine($"Checking mission {Id} {Type} is completed...");

            if (Result.HasValue)
                return Result.Value;

            var result = IsCompleted();

            if (result)
            {
                Debug.WriteLine($"Mission {Id} {Type} is completed...");

                if (null != timer && timer.Enabled)
                    timer.Stop();

                Result = result;
            }

            return result;
        }

        public override bool Equals(object obj)
        {
            var mission = (Mission)obj;

            return null != mission && Id == mission.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

    public sealed class TakeOffMission : Mission
    {
        public TakeOffMission()
        {
            Type = MissionType.TakeOff;
            _task = TaskExecute;
            Timeout = 10000;
        }

        protected override Func<bool> IsCompleted => IsCompletedExecute;

        private async Task TaskExecute()
        {
            await Drone.Instance.TakeOff();
        }

        private bool IsCompletedExecute()
        {
            return Drone.Instance.IsTakeOffFinish;
        }
    }

    public sealed class LandingMission : Mission
    {
        public LandingMission()
        {
            Type = MissionType.Landing;
            _task = TaskExecute;
            Timeout = 10000;
        }

        protected override Func<bool> IsCompleted => IsCompletedExecute; 

        private async Task TaskExecute()
        {
            Drone.Instance.EmergencyLanding();
        }

        private bool IsCompletedExecute()
        {
            return Drone.Instance.IsLanding;
        }
    }

    public sealed class SetPointMission: Mission
    {
        public SetPointMission()
        {
            Type = MissionType.SetPoint;
            _task = TaskExecute;
            Timeout = 5000;
        }

        protected override Func<bool> IsCompleted => IsCompleteExecute;

        private async Task TaskExecute()
        {
            var args = Args;

            //PositionController.Instance.YawSetpoint = args.Yaw;
            FlightStacks.Instance._positionController.YawSetpoint = args.Yaw;
            PositionController.Instance.AltitudeSetpoint = args.Altitude;
            PositionController.Instance.RelativeXSetpoint = args.RelativeX;
            PositionController.Instance.RelativeYSetpoint = args.RelativeY;
        }

        private bool IsCompleteExecute()
        {
            var currentState = Drone.Instance.CurrentState;
            var yaw = Args.Yaw;
            var altitude = Args.Altitude;

            var yawErr = Math.Abs(Drone.Instance.CurrentState.Yaw - yaw);
            var altitudeErr = Math.Abs(Drone.Instance.CurrentState.Altitude - altitude);

            return yawErr < 2f && altitudeErr < 0.2f;
        }
    }

    public sealed class Commander
    {
        /// <summary>
        /// for stack operations.
        /// </summary>
        private object stackLock = new object();

        private object idLock = new object();

        private int nextId = 1;

        public Queue<Mission> activeMissionStasks;

        public Queue<Mission> suspendedMissionStasks;

        private HashSet<Mission> completedMissions;

        private Mission currentMission;

        private Mission suspendedMission;

        private Thread workerThread;

        private long WorkerFrequence = 50L;

        private bool isSdkRegistered;

        private static Commander _instance;
        public static Commander Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Commander();

                return _instance;
            }
        }

        private Commander()
        {
            activeMissionStasks = new Queue<Mission>();
            suspendedMissionStasks = new Queue<Mission>();
            completedMissions = new HashSet<Mission>();
            workerThread = new Thread(Worker_DoWork);

            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            workerThread.Start();
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            isSdkRegistered = SDKRegistrationState.Succeeded == state && SDKError.NO_ERROR == errorCode;
        }

        private async void Worker_DoWork()
        {
            var watch = Stopwatch.StartNew();
            var elapsed = 0L;
            var sleepTime = 0;

            for (; ; )
            {
                watch.Restart();

                if (!isSdkRegistered)
                {
                    elapsed = watch.ElapsedMilliseconds;
                    sleepTime = (int)Math.Max(elapsed - WorkerFrequence, 0);

                    Thread.Sleep(sleepTime);
                    continue;
                }

                try
                {
                    ExecuteMission().Wait();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("commander: " + ex.ToString());
                }

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(elapsed - WorkerFrequence, 0);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task ExecuteMission()
        {
            var mission = default(Mission);

            lock (stackLock)
            {
                mission = currentMission;
            }

            // step 1. check if there is a mission begin executed
            // check the mission timeout
            if (null != mission)
            {
                Logger.Instance.Log($"mission {mission.Id} {mission.Type} is running...");

                // check the result of mission and return if it is already completed.
                if (!mission.CheckIfCompleted())
                {
                    return;
                }

                lock (stackLock)
                {
                    // mission completed, put it into bucket.
                    completedMissions.Add(mission);
                    currentMission = null;
                }
            }

            // step 2. pop a new mission
            lock (stackLock)
            {
                // no more mission, skip this cycle.
                if (!activeMissionStasks.Any())
                {
                    Logger.Instance.Log("empty mission stack...");
                    return;
                }

                mission = activeMissionStasks.Dequeue();
                currentMission = mission;
            }

            mission.Start();
        }

        private int GetNextId()
        {
            int next;

            lock (idLock)
            {
                next = nextId;
                nextId++;
            }

            return next;
        }

        public void AddMission(Mission mission)
        {
            Argument.IsNotNull(() => mission);

            lock (stackLock)
            {
                if (activeMissionStasks.Any(x => x.Equals(mission)))
                {
                    // drop the mission if duplicated
                    return;
                }

                activeMissionStasks.Enqueue(mission);
            }
        }

        public void AddTakeOffMission()
        {
            var id = GetNextId();
            var mission = new TakeOffMission
            {
                Id = id,
            };

            AddMission(mission);
        }

        public void AddLandingMission()
        {
            var id = GetNextId();
            var mission = new LandingMission
            {
                Id = id
            };
            
            AddMission(mission);
        }

        public void AddSetPointMission(float yawSetpoint, float altitudeSetpoint, float relativeXSetpoint, float relativeYSetpoint)
        {
            var id = GetNextId();

            var mission = new SetPointMission
            {
                Id = id,
                Args = new MissionArgs(yawSetpoint, altitudeSetpoint, relativeXSetpoint, relativeYSetpoint)
            };

            AddMission(mission);
        }

        public void EmergencyLanding()
        {
            SuspendAllMission();

            try
            {
                Drone.Instance.EmergencyLanding();
            }
            catch
            {
                // Emergency landing failed.
            }
        }

        private void SuspendAllMission()
        {

        }
    }
}
