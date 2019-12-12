using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net.Mail;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Configuration;

namespace Receiver
{
    public partial class Form1 : Form
    {
        public static string data = null;
        private const int port = 11000;
        private static System.Timers.Timer aTimer;
        // ManualResetEvent instances signal completion.  
        private static ManualResetEvent connectDone =
            new ManualResetEvent(false);
        private static ManualResetEvent sendDone =
            new ManualResetEvent(false);
        private static ManualResetEvent receiveDone =
            new ManualResetEvent(false);
        public static bool hostStatus = false;
        private static Socket client;
        // The response from the remote device.  
        private static String response = String.Empty;
        private static bool mailSent = false;
        private static string[] APP_STATE = { "Start", "Running" };
        private static Config config = new Config();

        public Form1()
        {
            InitializeComponent();
            GetConfigurationValue();
            aTimer = new System.Timers.Timer();

            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += OnTimedEvent;
            aTimer.Interval = config.interval * 1000;

            // Have the timer fire repeated events (true is the default)
            aTimer.AutoReset = true;

            Process[] pname = Process.GetProcessesByName(config.targetExe);
            if (pname.Length == 0)
            {
                ExecuteCommand(config.batchFileName);
            }

            // Start the timer
            aTimer.Enabled = true;
            button1.Text = APP_STATE[1];

            
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
                ExecuteCommand(config.batchFileName);
                sendEmail(config.emailSendTo, "test sbuject", "test body");
                
            }
            else if (hostStatus)
            {
                mailSent = false;
            }
            StartClient(hostname + "," + port.ToString() + ","+ hostStatus.ToString() + "<EOF>");

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
        private static void StartListening()
        {
            // Data buffer for incoming data.  
            byte[] bytes = new Byte[1024];

            // Establish the local endpoint for the socket.  
            // Dns.GetHostName returns the name of the   
            // host running the application.  
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            // Create a TCP/IP socket.  
            Socket listener = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and   
            // listen for incoming connections.  
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                // Start listening for connections.  
                while (true)
                {
                    
                    Console.WriteLine("Waiting for a connection...");
                    // Program is suspended while waiting for an incoming connection.  
                    Socket handler = listener.Accept();
                    data = null;

                    // An incoming connection needs to be processed.  
                    while (true)
                    {
                        int bytesRec = handler.Receive(bytes);
                        data += Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf("<EOF>") > -1)
                        {
                            data = data.Substring(0, data.Length - 5);
                            break;
                        }
                    }

                    // Show the data on the console.  
                    //Console.WriteLine("Text received : {0}", data);
                    MessageBox.Show("Text received :" + data);
                    commandHandler(data);
                    // Echo the data back to the client.  
                    byte[] msg = Encoding.ASCII.GetBytes(data);

                    handler.Send(msg);
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
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

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                Console.WriteLine("output>>" + e.Data);
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                Console.WriteLine("error>>" + e.Data);
            process.BeginErrorReadLine();

            process.WaitForExit();

            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            process.Close();
        }
        private static void StartClient(string msg)
        {
            // Connect to a remote device.  
            try
            {
                // Establish the remote endpoint for the socket.  
                // The name of the   
                // remote device is "host.contoso.com".  
                IPHostEntry ipHostInfo = Dns.GetHostEntry(config.serverAddr);
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint remoteEP = new IPEndPoint(ipAddress, port);

                // Create a TCP/IP socket.  
                client = new Socket(ipAddress.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp);

                // Connect to the remote endpoint.  
                client.BeginConnect(remoteEP,
                    new AsyncCallback(ConnectCallback), client);
                connectDone.WaitOne();
                Send(client, msg);

                // Receive the response from the remote device.  
                Receive(client);


                // Release the socket.  
                // client.Shutdown(SocketShutdown.Both);
                // client.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete the connection.  
                client.EndConnect(ar);

                Console.WriteLine("Socket connected to {0}",
                    client.RemoteEndPoint.ToString());

                // Signal that the connection has been made.  
                connectDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Receive(Socket client)
        {
            try
            {
                // Create the state object.  
                StateObject state = new StateObject();
                state.workSocket = client;

                // Begin receiving the data from the remote device.  
                client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReceiveCallback), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the state object and the client socket   
                // from the asynchronous state object.  
                StateObject state = (StateObject)ar.AsyncState;
                Socket client = state.workSocket;

                // Read data from the remote device.  
                int bytesRead = client.EndReceive(ar);

                if (bytesRead > 0)
                {
                    // There might be more data, so store the data received so far.  
                    state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                    // Get the rest of the data.  
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                else
                {
                    // All the data has arrived; put it in response.  
                    if (state.sb.Length > 1)
                    {
                        response = state.sb.ToString();
                    }
                    // Signal that all bytes have been received.  
                    commandHandler(response);
                    receiveDone.Set();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void Send(Socket client, String data)
        {
            // Convert the string data to byte data using ASCII encoding.  
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device.  
            client.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), client);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.  
                sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
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
                        }
                        catch (Exception ex)
                        {
                            //Error, could not send the message
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
            
        }

        private void label1_Click(object sender, EventArgs e)
        {

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
}
