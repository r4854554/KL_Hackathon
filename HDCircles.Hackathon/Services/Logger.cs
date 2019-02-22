using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace HDCircles.Hackathon.Services
{
    public struct LogRecord
    {
        public DateTime Timestamp { get; }

        public string TypeName { get; }

        public int ThreadId { get; }

        public string Message { get; }

        public LogRecord(DateTime timestamp, string typeName, int threadId, string message)
        {
            Timestamp = timestamp;
            TypeName = typeName;
            ThreadId = threadId;
            Message = message;
        }
    }

    public delegate void LogHandler(LogRecord record);

    public sealed class Logger
    {
        public event LogHandler Logged;

        private object logLock = new object();

        private List<LogRecord> Records { get; set; }

        private int MaxCount = 10000;

        private static Logger _instance;
        public static Logger Instance
        {
            get
            {
                if (null == _instance)
                    _instance = new Logger();

                return _instance;
            }
        }

        public Logger()
        {
            Records = new List<LogRecord>();
        }

        public List<LogRecord> GetLogs()
        {
            List<LogRecord> result;

            lock (logLock)
            {
                result = Records.ConvertAll(x => new LogRecord(x.Timestamp, x.TypeName, x.ThreadId, x.Message));
            }

            return result;
        }

        public void Log(string message)
        {
            var stackFrame = new StackFrame(1);
            var method = stackFrame.GetMethod();
            var methodName = method.Name;
            var className = method.ReflectedType.Name;
            var threadId = Thread.CurrentThread.ManagedThreadId;

            Log(message, $"{className}.{methodName}", threadId);
        }

        private void Log(string message, string typeName, int threadId)
        {
            lock (logLock)
            {
                var timestamp = DateTime.UtcNow;

                var record = new LogRecord(timestamp, typeName, threadId, message);

                if (Records.Count >= MaxCount)
                {
                    Records.RemoveAt(0);
                }

                if (Records.Count < MaxCount)
                {
                    Records.Add(record);
                }

                if (null != Logged)
                {
                    Logged.Invoke(record);
                }
            }
        }
    }
}
