using System;
using System.Configuration;
using System.Windows.Forms;

namespace Receiver.Utilities
{
    public class Config
    {
        public static int interval { get; set; }
        public static bool sendEmail { get; set; }
        public static int portToCheck { get; set; }
        public static string serverAddr { get; set; }
        public static string emailUser { get; set; }
        public static string emailPass { get; set; }
        public static string batchFileName { get; set; }
        public static string emailSendTo { get; set; }
        public static string targetExe { get; set; }
        public static bool usePowerShell { get; set; }
        public static string logServer { get; set; }
        public static string logMethod { get; set; }
        public static string targetCmdPath { get; set; }
        public static string targetCmdExe { get; set; }
        public static string cmdServer { get; set; }
        public static string cmdMethod { get; set; }
        public static int subCpu { get; set; }
        public static string conString { get; set; }
        public static int restartThreshold { get; set; }
        public static string screenshotServer { get; set; }
        public static void GetConfigurationValue()
        {
            try
            {
                var interval = ConfigurationManager.AppSettings["Interval"];
                Config.interval = Int32.Parse(interval);
                var SendEmail = ConfigurationManager.AppSettings["SendEmail"];
                Config.sendEmail = SendEmail == "1";
                var portToCheck = ConfigurationManager.AppSettings["portToCheck"];
                Config.portToCheck = Int32.Parse(portToCheck);
                var socketServerAddr = ConfigurationManager.AppSettings["socketServerAddr"];
                Config.serverAddr = socketServerAddr;
                var emailUser = ConfigurationManager.AppSettings["emailUser"];
                Config.emailUser = emailUser;
                var batchFileName = ConfigurationManager.AppSettings["batchFileName"];
                Config.batchFileName = batchFileName;
                var emailSendTo = ConfigurationManager.AppSettings["emailSendTo"];
                Config.emailSendTo = emailSendTo;
                var targetExe = ConfigurationManager.AppSettings["targetExe"];
                Config.targetExe = targetExe;
                var usePowerShell = ConfigurationManager.AppSettings["usePowerShell"];
                Config.usePowerShell = usePowerShell == "1";
                var logServer = ConfigurationManager.AppSettings["logServer"];
                Config.logServer = logServer;
                var logMethod = ConfigurationManager.AppSettings["logMethod"];
                Config.logMethod = logMethod;
                var targetCmdPath = ConfigurationManager.AppSettings["targetCmdPath"];
                Config.targetCmdPath = targetCmdPath;
                var targetCmdExe = ConfigurationManager.AppSettings["targetCmdExe"];
                Config.targetCmdExe = targetCmdExe;
                var cmdServer = ConfigurationManager.AppSettings["cmdServer"];
                Config.cmdServer = cmdServer;
                var cmdMethod = ConfigurationManager.AppSettings["cmdMethod"];
                Config.cmdMethod = cmdMethod;
                var subCpu = ConfigurationManager.AppSettings["subCpu"];
                Config.subCpu = subCpu != null ? Int32.Parse(subCpu) : 0;
                Config.subCpu = Config.subCpu > Environment.ProcessorCount ? Environment.ProcessorCount : Config.subCpu;
                var conString = ConfigurationManager.AppSettings["connectionString"];
                Config.conString = conString != null ? Session.Base64Decode(conString) : null;
                var restartThreshold = ConfigurationManager.AppSettings["restartThreshold"];
                Config.restartThreshold = restartThreshold != null ? Int32.Parse(restartThreshold) : 0;
                Config.screenshotServer = ConfigurationManager.AppSettings["screenshotServer"];
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}
