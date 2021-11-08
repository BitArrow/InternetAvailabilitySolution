using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Runtime.InteropServices;
using System.Threading;
using Timer = System.Timers.Timer;

namespace InternetAvailability
{
    public partial class InternetAvailabilityCheckService : ServiceBase
    {
        private const string ESource = "IACService";
        private const string ELog = "IACServiceLog";

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(System.IntPtr handle, ref ServiceStatus serviceStatus);

        public InternetAvailabilityCheckService()
        {
            InitializeComponent();
            eventLog = new EventLog();
            if (!EventLog.SourceExists(ESource))
            {
                EventLog.CreateEventSource(
                    ESource, ELog);
            }
            eventLog.Source = ESource;
            eventLog.Log = ELog;
        }

        protected override void OnStart(string[] args)
        {
            eventLog.WriteEntry("InternetAvailabilityCheckService started.", EventLogEntryType.Information, EventType.ServiceStarted);

            var timer = new Timer { Interval = 300000 }; // 5 min
            timer.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer.Start();

            // Update the service state to Start Pending.
            var serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_START_PENDING,
                dwWaitHint = 100000
            };
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }

        public async void OnTimer(object sender, ElapsedEventArgs args)
        {
            eventLog.WriteEntry("Starting internet connection check.", EventLogEntryType.Information, EventType.Info);

            // Update the service state to Running.
            var serviceStatus = new ServiceStatus { dwCurrentState = ServiceState.SERVICE_RUNNING };
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            var runs = 0;
            const int maxRuns = 3;
            while (runs < maxRuns)
            {
                var connected = CheckConnection();
                runs++;

                if (connected)
                {
                    eventLog.WriteEntry("System is connected to internet! :)", EventLogEntryType.Information,
                        EventType.ConnectionSuccess);
                    return;
                }

                eventLog.WriteEntry($"System failed to connect to internet! Fail count: {runs} :(",
                    EventLogEntryType.Warning, EventType.ConnectionFailed);

                if (runs < maxRuns)
                    await Task.Delay(30000); // 30 sec
            }

            eventLog.WriteEntry("Connection to internet failed, restarting computer.", EventLogEntryType.Error, EventType.RestartingComputer);
            Shutdown.Restart();
        }

        public bool CheckConnection()
        {
            try
            {
                var uri = new Uri("http://google.com/generate_204");
                using (var client = new WebClient())
                using (client.OpenRead(uri))
                    return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnStop()
        {
            eventLog.WriteEntry("InternetAvailabilityCheckService stopped.", EventLogEntryType.Information, EventType.ServiceStopped);

            // Update the service state to Stop Pending.
            var serviceStatus = new ServiceStatus
            {
                dwCurrentState = ServiceState.SERVICE_STOP_PENDING,
                dwWaitHint = 100000
            };
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);

            // Update the service state to Stopped.
            serviceStatus.dwCurrentState = ServiceState.SERVICE_STOPPED;
            SetServiceStatus(this.ServiceHandle, ref serviceStatus);
        }
    }

    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public int dwServiceType;
        public ServiceState dwCurrentState;
        public int dwControlsAccepted;
        public int dwWin32ExitCode;
        public int dwServiceSpecificExitCode;
        public int dwCheckPoint;
        public int dwWaitHint;
    };
}
