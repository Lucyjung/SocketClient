using System;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Reflection;
using Receiver.Utilities;
using Receiver.Data;
using Microsoft.Win32;
using Excel = Microsoft.Office.Interop.Excel;
namespace Receiver
{
    public partial class Form1 : Form
    {
        public static string data = null;
        private static System.Timers.Timer aTimer;
        public static bool hostStatus = false;
        private static string[] APP_STATE = { "Start", "Running" };
        private static Config config = new Config();
        private static int restCount = 0;
        private static int failedCount = 0;
        private static int statusCount = 0;
        
        
        enum BPStatus
        {
            Pending = 1,
            Running,
            Terminated,
            Stopped,
            Completed,
            Debugging,
            Archived,
            Stopping,
            Warning
        }
        public Form1()
        {
            InitializeComponent();
            Config.GetConfigurationValue();

            // Timer Init 
            aTimer = new System.Timers.Timer();

            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.Interval = Config.interval * 1000;

            // Have the timer fire repeated events (true is the default)
            aTimer.AutoReset = true;
            LogFile.WriteToFile("Start Oscar");
            prepareEnvironment();

            // Start the timer
            aTimer.Enabled = true;
            button1.Text = APP_STATE[1];
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            for (int i = 0;i < Config.subCpu; i++)
            {
                PerformanceCounter perfCnt = new PerformanceCounter("Processor", "% Processor Time", i.ToString()) ;
                Performance.subCpus.Add(perfCnt);
            }
            
        }
        private static void prepareEnvironment()
        {
            string keyName = @"Software\Microsoft\Office\16.0\Excel";
            Registry.CurrentUser.DeleteSubKeyTree(keyName, false);
            Excel.Application app = new Excel.Application();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(app);
            keyName = @"Software\Microsoft\Office\16.0\Excel\Security";

            using (RegistryKey security = Registry.CurrentUser.CreateSubKey(keyName))
            {
                // Create data for the TestSettings subkey.
                security.SetValue("VBAWarnings", 1);
                string protectKey = @"Software\Microsoft\Office\16.0\Excel\Security\ProtectedView";
                using (RegistryKey protect = Registry.CurrentUser.CreateSubKey(protectKey))
                {
                    protect.SetValue("DisableAttachmentsInPV",1);
                    protect.SetValue("DisableInternetFilesInPV", 1);
                    protect.SetValue("DisableUnsafeLocationsInPV", 1);
                }
            }
            Process[] pname = Process.GetProcessesByName(Config.targetExe);
            if (pname.Length == 0)
            {
                LogFile.WriteToFile("Execute First Command");
                Command.ExecuteCommand(Config.batchFileName);
            }
        }
        private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            int port = Config.portToCheck;
            if (Config.usePowerShell)
            {
                string cmdOutput = Command.ExecutePowerShell(@"& Test-NetConnection 127.0.0.1 -PORT " + port);
                string[] splited = cmdOutput.Replace("TcpTestSucceeded", "|").Split('|');
                if (splited.Length > 0 && splited[1].Contains("True"))
                {
                    hostStatus = true;
                }
                else
                {
                    hostStatus = false;
                }

            } else
            {
                hostStatus = Connection.PingHost("127.0.0.1", port);
            }


            string hostname = Dns.GetHostEntry("").HostName;
            
            if (hostStatus == false )
            {
                int status = Session.checkSession(hostname);
                failedCount++;
                if (
                    (failedCount >= Config.restartThreshold && (status <= (int)BPStatus.Running || status == (int)BPStatus.Warning)) || 
                    (failedCount >= Config.restartThresholdIdle && (status > (int)BPStatus.Running) && (status < (int)BPStatus.Warning))
                    )
                {
                    Command.killTask(Config.targetExe);
                    Command.ExecuteCommand(Config.batchFileName);
                } 
                LogFile.WriteToFile("Check Status : " + hostStatus.ToString() + " (Session Status = " + status + ")");

            } else
            {
                failedCount = 0;
                LogFile.WriteToFile("Check Status : " + hostStatus.ToString());
            }
            

            // StartClient(hostname + "," + port.ToString() + ","+ hostStatus.ToString() + "<EOF>");
            if (restCount >= 12)
            {
                float[] perf = Performance.getPerformaceCounter();
                OscarLog logObj = new OscarLog();
                logObj.hostName = hostname;
                logObj.status = hostStatus.ToString();
                logObj.timestamps = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                logObj.statusCount = statusCount;
                logObj.cpuUsage = perf[0];
                logObj.ramUsage = perf[1];
                logObj.diskUsage = perf[2];
                for (int i = 3; i < perf.Length; i++)
                {
                    PropertyInfo propertyInfo = logObj.GetType().GetProperty("cpu" + (i-2).ToString());
                    var t = typeof(float?);
                    t = Nullable.GetUnderlyingType(t);
                    propertyInfo.SetValue(logObj, Convert.ChangeType(perf[i], t), null);
                }
                string logData = JsonConvert.SerializeObject(logObj, Newtonsoft.Json.Formatting.None,
                            new JsonSerializerSettings
                            {
                                NullValueHandling = NullValueHandling.Ignore
                            });
                LogFile.WriteToFile("Log Status to Server ");
                _ = HttpReq.CurlRequestAsync(Config.logServer, Config.logMethod, logData);
                string filename = Screenshot.Capture();
                _ = HttpReq.UploadImage(Config.screenshotServer, filename);
                restCount = 0;
                statusCount = 0;
            }
            {
                restCount++;
                if (hostStatus)
                {
                    statusCount += restCount;
                }
            }
        }
        private void Form1_Resize(object sender, EventArgs e)
        {

            if (FormWindowState.Minimized == this.WindowState)
            {
                notifyIcon1.Visible = true;
                this.Hide();
            }
            else if (FormWindowState.Normal == this.WindowState)
            {
                notifyIcon1.Visible = false;
            }
        }
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == APP_STATE[0])
            {
                button1.Text = APP_STATE[1];
                aTimer.Interval = Config.interval * 1000;

                // Have the timer fire repeated events (true is the default)
                aTimer.AutoReset = true;

                // Start the timer
                aTimer.Enabled = true;

            }
            else
            {
                button1.Text = APP_STATE[0];
                aTimer.Enabled = false;
            }
        }

        
    }
}
