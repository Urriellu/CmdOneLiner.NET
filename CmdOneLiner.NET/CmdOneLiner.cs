using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CmdOneLinerNET
{
    /// <summary>Allows easily executing a command line application in one line of code.</summary>
    public static class CmdOneLiner
    {
        /// <summary>Execute a command-line application.</summary>
        /// <param name="cmd">Command to execute, and its arguments</param>
        /// <param name="workingdir">Path to working directory. If null, the current one (<see cref="Environment.CurrentDirectory"/>) will be used.</param>
        /// <param name="timeout">If not null, the command will be killed after the given timeout.</param>
        /// <param name="canceltoken">Optional object used to kill the process on demand.</param>
        public static (int ExitCode, bool Success, string StdOut, string StdErr, Int64 MaxRamUsedBytes, TimeSpan UserProcessorTime, TimeSpan TotalProcessorTime) Run(string cmd, string workingdir = null, TimeSpan? timeout = null, CancellationToken? canceltoken = null)
        {
            using Process p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = cmd.Split(' ').First();
            if (!string.IsNullOrEmpty(workingdir))
            {
                if (System.IO.File.Exists(workingdir)) throw new Exception($"Working directory '{workingdir}' is actually a file, not a directory.");
                if (!System.IO.Directory.Exists(workingdir)) throw new Exception($"Working directory '{workingdir}' does not exist.");
                p.StartInfo.WorkingDirectory = workingdir;
            }
            if (cmd.Length > p.StartInfo.FileName.Length + 1) p.StartInfo.Arguments = cmd.Substring(p.StartInfo.FileName.Length + 1);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            StringBuilder stdout = new StringBuilder();
            StringBuilder stderr = new StringBuilder();

            using AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
            using AutoResetEvent errorWaitHandle = new AutoResetEvent(false);
            p.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null) outputWaitHandle.Set();
                else stdout.AppendLine(e.Data);
            };
            p.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null) errorWaitHandle.Set();
                else stderr.AppendLine(e.Data);
            };

            bool killed = false;

            CancellationTokenRegistration? cancelTokenRegistration = canceltoken?.Register(() =>
            {
                if (p != null && !p.HasExited)
                {
                    killed = true;
                    try { p.Kill(); }
                    catch (Exception ex) { throw new Exception($"Error while trying to kill process '{cmd}': {ex.Message}"); }
                    try { if (!p.WaitForExit(60 * 1000)) throw new Exception($"Process '{cmd}' has not been killed after being canceled"); }
                    catch (Exception ex) { throw new Exception($"Error while waiting after trying to kill process '{cmd}': {ex.Message}"); }
                }
            });

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            int timeoutms = int.MaxValue;
            if (timeout != null) timeoutms = (int)timeout.Value.TotalMilliseconds;

            Int64 maxmem = 0;
            TimeSpan upt = TimeSpan.Zero;
            TimeSpan tpt = TimeSpan.Zero;
            Task.Factory.StartNew(() =>
                {
                    Thread.CurrentThread.Name = $"{nameof(CmdOneLiner)}:Process '{p.StartInfo.FileName}' max mem usage checker";
                    Stopwatch runningfor = Stopwatch.StartNew();

                    try
                    {
                        while (!p.HasExited && runningfor.ElapsedMilliseconds < timeoutms && canceltoken?.IsCancellationRequested != true)
                        {
                            p.Refresh();
                            maxmem = p.PeakWorkingSet64;
                            if (p.UserProcessorTime > upt) upt = p.UserProcessorTime;
                            if (p.TotalProcessorTime > tpt) tpt = p.TotalProcessorTime;
                        }
                        if (canceltoken.HasValue) Task.Delay(500, canceltoken.Value).Wait();
                        else Task.Delay(100).Wait();
                    }
                    catch { }
                });

            try
            {
                bool exited = p.WaitForExit(timeoutms);
                if (exited && outputWaitHandle.WaitOne(timeoutms) && errorWaitHandle.WaitOne(timeoutms))
                {
                    if (killed) return (-1, false, stdout.ToString(), stderr.ToString() + Environment.NewLine + "Process killed.", maxmem, upt, tpt);
                    else if (!exited) return (-1, false, stdout.ToString(), stderr.ToString() + Environment.NewLine + "Process not exited but also not killed. Unknown state.", maxmem, upt, tpt);
                    else
                    {
                        // wait until it has actually exited. This is needed because sometimes WaitForExit returns true but the process hasn't existed and then it throws an exception when trying to read ExistCode
                        Stopwatch sw = Stopwatch.StartNew();
                        while (!p.HasExited)
                        {
                            Thread.Sleep(100);
                            if (sw.Elapsed > TimeSpan.FromSeconds(60)) throw new Exception($".NET says that process '{cmd}' has exited and it has not been killed but after waiting for 60 seconds it is still running.");
                        }
                        p.Refresh();
                        return (p.ExitCode, p.ExitCode == 0, stdout.ToString(), stderr.ToString(), maxmem, upt, tpt);
                    }
                }
                else return (-1, false, stdout.ToString(), stderr.ToString() + Environment.NewLine + "Process timed out.", maxmem, upt, tpt);
            }
            finally
            {
                cancelTokenRegistration?.Dispose();
            }
        }
    }
}
