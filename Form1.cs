using System;
using System.Text;
using System.Windows.Forms;
using System.Net.Mail;
using System.Net;
using System.Diagnostics;
using System.Configuration;
using System.Net.Sockets;
using System.IO;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;
using System.Reflection;
using System.Data.SqlClient;
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
        static PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        static PerformanceCounter  ramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use", null);
        static PerformanceCounter diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        static List<PerformanceCounter> subCpus = new List<PerformanceCounter>();
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
            GetConfigurationValue();

            // Timer Init 
            aTimer = new System.Timers.Timer();

            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.Interval = config.interval * 1000;

            // Have the timer fire repeated events (true is the default)
            aTimer.AutoReset = true;
            WriteToFile("Start Oscar");
            Process[] pname = Process.GetProcessesByName(config.targetExe);
            if (pname.Length == 0)
            {
                WriteToFile("Execute First Command");
                ExecuteCommand(config.batchFileName);
            }

            // Start the timer
            aTimer.Enabled = true;
            button1.Text = APP_STATE[1];
            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            for (int i = 0;i < config.subCpu; i++)
            {
                PerformanceCounter perfCnt = new PerformanceCounter("Processor", "% Processor Time", i.ToString()) ;
                subCpus.Add(perfCnt);
            }

        }
        public static void GetConfigurationValue()
        {
            try
            {
                var interval = ConfigurationManager.AppSettings["Interval"];
                config.interval = Int32.Parse(interval);
                var SendEmail = ConfigurationManager.AppSettings["SendEmail"];
                config.sendEmail = SendEmail == "1";
                var portToCheck = ConfigurationManager.AppSettings["portToCheck"];
                config.portToCheck = Int32.Parse(portToCheck);
                var socketServerAddr = ConfigurationManager.AppSettings["socketServerAddr"];
                config.serverAddr = socketServerAddr;
                var emailUser = ConfigurationManager.AppSettings["emailUser"];
                config.emailUser = emailUser;
                var batchFileName = ConfigurationManager.AppSettings["batchFileName"];
                config.batchFileName = batchFileName;
                var emailSendTo = ConfigurationManager.AppSettings["emailSendTo"];
                config.emailSendTo = emailSendTo;
                var targetExe = ConfigurationManager.AppSettings["targetExe"];
                config.targetExe = targetExe;
                var usePowerShell = ConfigurationManager.AppSettings["usePowerShell"];
                config.usePowerShell = usePowerShell == "1";
                var logServer = ConfigurationManager.AppSettings["logServer"];
                config.logServer = logServer;
                var logMethod = ConfigurationManager.AppSettings["logMethod"];
                config.logMethod = logMethod;
                var targetCmdPath = ConfigurationManager.AppSettings["targetCmdPath"];
                config.targetCmdPath = targetCmdPath;
                var targetCmdExe = ConfigurationManager.AppSettings["targetCmdExe"];
                config.targetCmdExe = targetCmdExe;
                var cmdServer = ConfigurationManager.AppSettings["cmdServer"];
                config.cmdServer = cmdServer;
                var cmdMethod = ConfigurationManager.AppSettings["cmdMethod"];
                config.cmdMethod = cmdMethod;
                var subCpu = ConfigurationManager.AppSettings["subCpu"];
                config.subCpu = subCpu != null ?Int32.Parse(subCpu): 0;
                config.subCpu = config.subCpu > Environment.ProcessorCount ? Environment.ProcessorCount : config.subCpu;
                var conString = ConfigurationManager.AppSettings["connectionString"];
                config.conString = conString != null ? Base64Decode(conString) : null;
                var restartThreshold = ConfigurationManager.AppSettings["restartThreshold"];
                config.restartThreshold = restartThreshold != null ? Int32.Parse(restartThreshold) : 0;
            } catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }
        private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            int port = config.portToCheck;
            if (config.usePowerShell)
            {
                string cmdOutput = ExecutePowerShell(@"& Test-NetConnection 127.0.0.1 -PORT " + port);
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
                hostStatus = PingHost("127.0.0.1", port);
            }


            string hostname = Dns.GetHostEntry("").HostName;
            
            if (hostStatus == false )
            {
                int status = checkSession(hostname);
                failedCount++;
                if (
                    (failedCount >= config.restartThreshold && (status <= (int)BPStatus.Running || status == (int)BPStatus.Warning)) || 
                    ((status > (int)BPStatus.Running) && (status < (int)BPStatus.Warning))
                    )
                {
                    killTask(config.targetExe);
                    ExecuteCommand(config.batchFileName);
                } 
                WriteToFile("Check Status : " + hostStatus.ToString() + " (Session Status = " + status + ")");

            } else
            {
                failedCount = 0;
                WriteToFile("Check Status : " + hostStatus.ToString());
            }
            

            // StartClient(hostname + "," + port.ToString() + ","+ hostStatus.ToString() + "<EOF>");
            if (restCount >= 12)
            {
                float[] perf = getPerformaceCounter();
                LogData logObj = new LogData();
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
                WriteToFile("Log Status to Server ");
                _ = CurlRequestAsync(config.logServer, config.logMethod, logData);
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
                aTimer.Interval = config.interval * 1000;

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

        private static void commandHandler(string cmd)
        {
            if (cmd.IndexOf("runBatch") > -1)
            {
                string[] splited = cmd.Split(' ');
                string fileName = splited[1];
                ExecuteCommand(fileName);
            }
        }
        static void ExecuteCommand(string command)
        {

            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            process.WaitForExit();

            process.Close();
        }
        static string ExecutePowerShell(string command)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = @"powershell.exe";
            startInfo.Arguments = command;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            Process process = new Process();
            process.StartInfo = startInfo;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            return output;
        }
        public static bool PingHost(string hostUri, int portNumber)
        {
            try
            {
                using (var client = new TcpClient(hostUri, portNumber))
                    return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }
        private static void sendEmail(string to, string subject, string body)
        {
            if (config.sendEmail)
            {
                using (SmtpClient smtpClient = new SmtpClient())
                {
                    using (MailMessage message = new MailMessage())
                    {
                        MailAddress fromAddress = new MailAddress(config.emailUser);

                        smtpClient.Host = "smtp.office365.com";
                        smtpClient.Port = 587;
                        smtpClient.UseDefaultCredentials = false;
                        smtpClient.EnableSsl = true;
                        smtpClient.Credentials = new System.Net.NetworkCredential(config.emailUser, config.emailPass);
                        smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                        message.From = fromAddress;
                        message.Subject = subject;
                        // Set IsBodyHtml to true means you can send HTML email.
                        message.IsBodyHtml = true;
                        message.Body = body;
                        message.To.Add(to);

                        try
                        {
                            smtpClient.Send(message);
                            WriteToFile("Email Sent");
                        }
                        catch (Exception ex)
                        {
                            //Error, could not send the message
                            WriteToFile("Error : " + ex.Message);
                        }
                    }
                }
            }

        }
        private static void killTask(string name)
        {
            Process[] pname = Process.GetProcessesByName(name);
            if (pname.Length > 0)
            {
                foreach (Process pr in pname)
                {
                    try
                    {
                        pr.Kill();
                    }
                    catch { }
                }
            }
        }
        public static string Request(string URL, string method, string DATA = null, string user = null, string password = null)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(URL);
            request.Method = method;
            request.ContentType = "application/json";
            request.KeepAlive = false;
            if (user != null && password != null)
            {
                String encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(user + ":" + password));
                request.Headers.Add("Authorization", "Basic " + encoded);
            }
            if (DATA != null)
            {
                byte[] byteArray = Encoding.UTF8.GetBytes(DATA);
                request.ContentLength = byteArray.Length;
                using (Stream webStream = request.GetRequestStream())
                {
                    webStream.Write(byteArray, 0, byteArray.Length); // Send the data.
                    webStream.Close();
                }

            }

            try
            {
                WebResponse webResponse = request.GetResponse();
                using (Stream webStream = webResponse.GetResponseStream() ?? Stream.Null)
                using (StreamReader responseReader = new StreamReader(webStream))
                {
                    string response = responseReader.ReadToEnd();
                    return response;
                }
            }
            catch (Exception e)
            {
                WriteToFile("-----------------");
                WriteToFile(e.Message);
                return null;
            }
        }
        protected static async System.Threading.Tasks.Task<HttpResponseMessage> CurlRequestAsync(string url, string method, string DATA = null, string user = null, string password = null)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
            try
            {
                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod(method), url))
                    {
                        if (user != null && password != null) {
                            var base64authorization = Convert.ToBase64String(Encoding.ASCII.GetBytes(user + ":" + password));
                            request.Headers.TryAddWithoutValidation("Authorization", $"Basic {base64authorization}");
                        }
                        if (DATA != null)
                        {
                            request.Content = new StringContent(DATA, Encoding.UTF8, "application/json");
                        }
                        var result = await httpClient.SendAsync(request);
                        if (result.IsSuccessStatusCode)
                        {
                            using (HttpContent content = result.Content)
                            {

                                var jsonStr = content.ReadAsStringAsync().Result;
                                
                                WriteToFile("Server Respond : " + jsonStr);
                                callBackDelegate pFunc = new callBackDelegate(responseCallback);
                                pFunc(jsonStr);
                            }
                        }
                        else
                        {
                            WriteToFile("Server Not Respond");
                        }
                        return result;
                        
                    }
                }
            }
            catch (Exception e)
            {
                WriteToFile("-----------------");
                WriteToFile(e.Message);
                return null;
            }
        }
        private delegate void callBackDelegate(string res);
        private static void responseCallback(string res)
        {
            try
            {
                var json = JsonConvert.DeserializeObject<OscarResponse>(res);
                if (json.success && json.command != null)
                {
                    createBatchFileAndRun(json.command);
                    OscarUpdateStatus update = new OscarUpdateStatus();
                    update._id = json.command.id;
                    update.status = "Completed";
                    string updateData = JsonConvert.SerializeObject(update);
                    
                    _ = CurlRequestAsync(config.cmdServer, config.cmdMethod, updateData);
                    WriteToFile("Update Status to Server ");
                }
            } catch (Exception e )
            {
                WriteToFile("Error during callback : " + e.ToString());
            }
            
        }
        private static void createBatchFileAndRun(OscarCommand cmd)
        {
            if (cmd.process != "")
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + "\\BatchFiles";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                
                string batFilePath = AppDomain.CurrentDomain.BaseDirectory + "\\BatchFiles\\" + DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds + "_" + cmd.process + ".bat";
                using (StreamWriter sw = new StreamWriter(batFilePath))
                {
                    sw.WriteLine(config.targetCmdPath[0].ToString() + config.targetCmdPath[1].ToString());
                    sw.WriteLine(@"cd " + config.targetCmdPath);
                    sw.Write("START " + config.targetCmdExe + " /run ");
                    sw.Write(cmd.process);
                    sw.Write(" ");
                    if (cmd.userPass != null)
                    {
                        sw.Write("/user ");
                        sw.Write(cmd.userPass);
                    }
                    if (cmd.parameter != null)
                    {
                        sw.Write(" ");
                        sw.Write("/startp " + cmd.parameter);
                    }
                }
                ExecuteCommand(batFilePath);
            }
            
        }
        public static float[] getPerformaceCounter()
        {

            // will always start at 0
            _ = cpuCounter.NextValue();
            _ = ramCounter.NextValue();
            _ = diskCounter.NextValue();
            foreach (var subCpu in subCpus)
            {
                _ = subCpu.NextValue();
            }
            System.Threading.Thread.Sleep(500);
            // now matches task manager reading

            List<float> val = new List<float>();
            val.Add(cpuCounter.NextValue());
            val.Add(ramCounter.NextValue());
            val.Add(diskCounter.NextValue());
            foreach (var subCpu in subCpus)
            {
                val.Add(subCpu.NextValue());
            }
            return val.ToArray();

        }
        private static void WriteToFile(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                // Create a file to write to.   
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(DateTime.Now.ToString() + " : " + Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(DateTime.Now.ToString() + " : " + Message);
                }
            }
        }
        private static int checkSession(string hostName)
        {
            string connetionString;
            SqlConnection connection;
            SqlCommand command;
            string sql;
            SqlDataReader dataReader;
            int status = 0;

            if (config.conString != null)
            {
                connetionString = config.conString;
                sql = @"SELECT TOP (1) [sessionid]
                  ,[processid]
                  ,[statusid]
                  ,[BPASession].[lastupdated]
                  ,[laststage]
	              ,[BPAResource].FQDN
                    FROM [dbo].[BPASession] 
                    inner join [BPAResource] on starterresourceid  = [BPAResource].[resourceid]
                    where FQDN = '" + hostName + @"'
                    order by [BPASession].[lastupdated] DESC";

                connection = new SqlConnection(connetionString);
                try
                {
                    connection.Open();
                    command = new SqlCommand(sql, connection);
                    dataReader = command.ExecuteReader();
                    while (dataReader.Read())
                    {
                        status = Int32.Parse(dataReader.GetValue(2).ToString());
                    }
                    command.Dispose();
                    connection.Close();
                }
                catch (Exception ex)
                {
                    WriteToFile("Error sql : " + ex.ToString());
                }
            }
            
            return status;
        }
        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        private static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
    // State object for receiving data from remote device.  
    public class StateObject
    {
        // Client socket.  
        public Socket workSocket = null;
        // Size of receive buffer.  
        public const int BufferSize = 256;
        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];
        // Received data string.  
        public StringBuilder sb = new StringBuilder();
    }
    public class Config
    {
        public int interval;
        public bool sendEmail;
        public int portToCheck;
        public string serverAddr;
        public string emailUser;
        public string emailPass;
        public string batchFileName;
        public string emailSendTo;
        public string targetExe;
        public bool usePowerShell;
        public string logServer;
        public string logMethod;
        public string targetCmdPath;
        public string targetCmdExe;
        public string cmdServer;
        public string cmdMethod;
        public int subCpu;
        public string conString;
        public int restartThreshold;
    }
    public class LogData
    {
        public string hostName { get; set; }
        public string status { get; set; }
        public int timestamps { get; set; }
        public int statusCount { get; set; }
        public float cpuUsage { get; set; }
        public float ramUsage { get; set; }
        public float diskUsage { get; set; }
        public float? cpu1 { get; set; }
        public float? cpu2 { get; set; }
        public float? cpu3 { get; set; }
        public float? cpu4 { get; set; }
        public float? cpu5 { get; set; }
        public float? cpu6 { get; set; }
        public float? cpu7 { get; set; }
        public float? cpu8 { get; set; }
        public float? cpu9 { get; set; }
        public float? cpu10 { get; set; }
        public float? cpu11 { get; set; }
        public float? cpu12 { get; set; }
        public float? cpu13 { get; set; }
        public float? cpu14 { get; set; }
        public float? cpu15 { get; set; }
        public float? cpu16 { get; set; }
    }
    public class OscarResponse
    {
        public bool success;
        public OscarCommand command;
    }
    public class OscarCommand
    {
        public bool success;
        public string id;
        public string process;
        public string userPass;
        public string parameter;
    }
    public class OscarUpdateStatus
    {
        public string _id;
        public string status;
    }
}
