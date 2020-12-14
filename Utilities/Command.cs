using Receiver.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Receiver.Utilities
{
    class Command
    {
        public static void ExecuteCommand(string command)
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
        public static string ExecutePowerShell(string command)
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
        public static void createBatchFileAndRun(OscarCommand cmd)
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
                    sw.WriteLine(Config.targetCmdPath[0].ToString() + Config.targetCmdPath[1].ToString());
                    sw.WriteLine(@"cd " + Config.targetCmdPath);
                    if (cmd.command != null)
                    {
                        sw.Write("START " + Config.targetCmdExe + " " + cmd.command + " ");
                    } else
                    {
                        sw.Write("START " + Config.targetCmdExe + " /run ");
                        sw.Write(cmd.process);
                        sw.Write(" ");
                        
                        if (cmd.parameter != null)
                        {
                            sw.Write(" ");
                            sw.Write("/startp " + cmd.parameter);
                        }
                    }
                    if (cmd.userPass != null)
                    {
                        sw.Write("/user ");
                        sw.Write(cmd.userPass);
                    }
                }
                ExecuteCommand(batFilePath);
            }

        }
        public static void killTask(string name)
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
    }
}
