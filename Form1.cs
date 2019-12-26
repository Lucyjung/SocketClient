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

namespace Receiver
{
    public partial class Form1 : Form
    {
        public static string data = null;
        private const int port = 11000;
        private static System.Timers.Timer aTimer;
        public static bool hostStatus = false;
        // The response from the remote device.  
        private static String response = String.Empty;
        private static bool mailSent = false;
        private static string[] APP_STATE = { "Start", "Running" };
        private static Config config = new Config();
        private static int restCount = 0;

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

            } catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            
        }
        private static void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            int port = config.portToCheck;
            hostStatus = PingHost("localhost", port);
            string hostname = Dns.GetHostName();
            
            if (hostStatus == false && mailSent == false)
            {
                mailSent = true;
                killTaskIfNotResponse(config.targetExe);
                ExecuteCommand(config.batchFileName);
                sendEmail(config.emailSendTo, "test sbuject", "test body");
                
            }
            else if (hostStatus)
            {
                mailSent = false;
            }
            WriteToFile("Check Status : " + hostStatus.ToString());
            // StartClient(hostname + "," + port.ToString() + ","+ hostStatus.ToString() + "<EOF>");
            if (restCount > 10)
            {
                LogData logObj = new LogData();
                logObj.hostName = hostname;
                logObj.status = hostStatus.ToString();
                logObj.timestamps = (Int32)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                string logData = JsonConvert.SerializeObject(logObj);
                WriteToFile("Log Status to Server ");
                _ = CurlRequestAsync("https://oscar-demo.azurewebsites.net/oscar/log", "POST", logData);
                restCount = 0;
            }
            else
            {
                restCount++;
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

                mailSent = false;
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
        public static bool PingHost(string hostUri, int portNumber)
        {
            try
            {
                using (var client = new TcpClient(hostUri, portNumber))
                    return true;
            }
            catch (SocketException ex)
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
        private static void killTaskIfNotResponse(string name)
        {
            Process[] pname = Process.GetProcessesByName(name);
            if (pname.Length > 0)
            {
                foreach (Process pr in pname)
                {
                    if (!pr.Responding)
                    {
                        try
                        {
                            pr.Kill();
                        }
                        catch { }
                    }
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
                        WriteToFile("Server Respond");
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
        private void label1_Click(object sender, EventArgs e)
        {

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
    }
    public class LogData
    {
        public string hostName;
        public string status;
        public int timestamps;
    }
}
