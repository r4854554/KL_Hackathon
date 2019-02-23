using Catel;
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

            missionTaskObj = Task();
            missionTaskObj.Start();

            timer = new System.Timers.Timer(Timeout);
            timer.Elapsed += Timer_Timeout;
            timer.Start();
        }

        public bool CheckIfCompleted()
        {
            Argument.IsNotNull(() => IsCompleted);

            if (Result.HasValue)
                return Result.Value;

            var result = IsCompleted();

            if (result)
            {
                if (null != timer)
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

    }

    public sealed class Commander
    {
        /// <summary>
        /// for stack operations.
        /// </summary>
        private object stackLock = new object();

        private object idLock = new object();

        private int nextId = 1;

        public Stack<Mission> activeMissionStasks;

        public Stack<Mission> suspendedMissionStasks;

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
            activeMissionStasks = new Stack<Mission>();
            suspendedMissionStasks = new Stack<Mission>();
            completedMissions = new HashSet<Mission>();
            workerThread = new Thread(Worker_DoWork);

            DJISDKManager.Instance.SDKRegistrationStateChanged += Instance_SDKRegistrationStateChanged;

            workerThread.Start();
        }

        private void Instance_SDKRegistrationStateChanged(SDKRegistrationState state, SDKError errorCode)
        {
            isSdkRegistered = SDKRegistrationState.Succeeded == state && SDKError.NO_ERROR == errorCode;
        }

        private void Worker_DoWork()
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
                }

                ExecuteMission().Wait();

                watch.Stop();

                elapsed = watch.ElapsedMilliseconds;
                sleepTime = (int)Math.Max(elapsed - WorkerFrequence, 0);

                Thread.Sleep(sleepTime);
            }
        }

        private async Task ExecuteMission()
        {
            var mission = currentMission;

            // step 1. check if there is a mission begin executed
            // check the mission timeout
            if (null != mission)
            {
                // check the result of mission and return if it is already completed.
                if (!mission.CheckIfCompleted())
                {
                    return;
                }

                // mission completed, put it into bucket.
                completedMissions.Add(mission);
            }

            // step 2. pop a new mission

            // no more mission, skip this cycle.
            if (!activeMissionStasks.Any())
            {
                return;
            }

            mission = activeMissionStasks.Pop();
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

                activeMissionStasks.Push(mission);
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
