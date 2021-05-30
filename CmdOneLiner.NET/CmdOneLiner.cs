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
            if (!string.IsNullOrEmpty(workingdir)) p.StartInfo.WorkingDirectory = workingdir;
            p.StartInfo.Arguments = cmd.Substring(p.StartInfo.FileName.Length + 1);
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
                if (p.WaitForExit(timeoutms) && outputWaitHandle.WaitOne(timeoutms) && errorWaitHandle.WaitOne(timeoutms))
                {
                    if (killed) return (-1, false, stdout.ToString(), stderr.ToString() + Environment.NewLine + "Process killed.", maxmem, upt, tpt);
                    else
                    {
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
