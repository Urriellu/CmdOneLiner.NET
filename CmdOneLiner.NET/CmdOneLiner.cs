using System;
using System.Diagnostics;
using System.IO;
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
        public static (int ExitCode, bool Success, string StdOut, string StdErr, Int64? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) Run(string cmd, string workingdir = null, TimeSpan? timeout = null, CancellationToken? canceltoken = null, ProcessPriorityClass priority = ProcessPriorityClass.Normal, IOPriorityClass iopriority = IOPriorityClass.L02_NormalEffort, string StdIn = null, bool ignoreStatistics = false)
        {
            using Process p = new Process();
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = cmd.Split(' ').First();
            if (!string.IsNullOrEmpty(workingdir))
            {
                if (File.Exists(workingdir)) throw new Exception($"Working directory '{workingdir}' is actually a file, not a directory.");
                if (!Directory.Exists(workingdir)) throw new Exception($"Working directory '{workingdir}' does not exist.");
                p.StartInfo.WorkingDirectory = workingdir;
            }

            if (cmd.Length > p.StartInfo.FileName.Length + 1) p.StartInfo.Arguments = cmd.Substring(p.StartInfo.FileName.Length + 1);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;

            if (!string.IsNullOrEmpty(StdIn)) p.StartInfo.RedirectStandardInput = true;

            StringBuilder stdout = new StringBuilder();
            StringBuilder stderr = new StringBuilder();

            using AutoResetEvent outputWaitHandle = new AutoResetEvent(false);
            using AutoResetEvent errorWaitHandle = new AutoResetEvent(false);
            p.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    try { outputWaitHandle.Set(); }
                    catch { }
                }
                else stdout.AppendLine(e.Data);
            };
            p.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    try { errorWaitHandle.Set(); }
                    catch { }
                }
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

                    try
                    {
                        if (!p.WaitForExit(60 * 1000)) throw new Exception($"Process '{cmd}' has not been killed after being canceled");
                    }
                    catch (Exception ex) { throw new Exception($"Error while waiting after trying to kill process '{cmd}': {ex.Message}"); }
                }
            });

            p.Start();
            try { p.PriorityClass = priority; }
            catch { }

            if (Environment.OSVersion.Platform == PlatformID.Unix && iopriority != IOPriorityClass.L02_NormalEffort)
            {
                try { SetIOPriority(p.Id, iopriority); }
                catch { }
            }

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            int timeoutms = int.MaxValue;
            if (timeout != null) timeoutms = (int) timeout.Value.TotalMilliseconds;

            Int64? maxmem = 0;
            TimeSpan? upt = TimeSpan.Zero;
            TimeSpan? tpt = TimeSpan.Zero;
            if (!ignoreStatistics)
            {
                Task.Factory.StartNew(() =>
                {
                    Thread.CurrentThread.Name = $"[{Thread.CurrentThread.ManagedThreadId}] {nameof(CmdOneLiner)}:Process '{p.StartInfo.FileName}' max mem usage checker";
                    Stopwatch runningfor = Stopwatch.StartNew();

                    try
                    {
                        while (!p.HasExited && runningfor.ElapsedMilliseconds < timeoutms && canceltoken?.IsCancellationRequested != true)
                        {
                            p.Refresh();
                            maxmem = Max(maxmem, p.PeakWorkingSet64, p.WorkingSet64);
                            if (p.UserProcessorTime > upt) upt = p.UserProcessorTime;
                            if (p.TotalProcessorTime > tpt) tpt = p.TotalProcessorTime;
                        }

                        if (canceltoken.HasValue) Task.Delay(500, canceltoken.Value).Wait();
                        else Task.Delay(100).Wait();
                    }
                    catch { }
                });
            }

            if (!string.IsNullOrEmpty(StdIn))
            {
                p.StandardInput.Write(StdIn);
                p.StandardInput.Close();
            }

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

                        { // sometimes 'Process' throws an exception while trying to read ExitCode saying the process hasn't finished even though it has. May be a sync issue when CPU is overloaded. So we retry a few times
                            int retried = 0;
                            bool exception = false;
                            do
                            {
                                try
                                {
                                    int exitcode = p.ExitCode;
                                    exception = false;
                                }
                                catch (InvalidOperationException ex)
                                {
                                    exception = true;
                                    retried++;
                                    if (retried > 5) throw new Exception($"Unable to retrieve process '{cmd}' info: {ex.Message}");
                                    Thread.Sleep(TimeSpan.FromSeconds(2 * retried));
                                }
                            } while (exception);
                        }

                        int errored = 0;
                        while (true)
                        {
                            try { return (p.ExitCode, p.ExitCode == 0, stdout.ToString(), stderr.ToString(), maxmem, upt, tpt); }
                            catch (InvalidOperationException ex)
                            {
                                errored++;
                                if (errored < 10)
                                {
                                    Console.Error.WriteLine($"Process '{cmd}' is supposed to be terminated but we cannot read its status. Error: {ex.Message}. Retrying...");
                                    Thread.Sleep(TimeSpan.FromSeconds(errored));
                                }
                                else throw new Exception($"Process '{cmd}' is supposed to be terminated but we could not read its status after retrying {errored} times. Error: {ex.Message}.", ex);
                            }
                        }
                    }
                }
                else
                {
                    try { p.Kill(); }
                    catch { }

                    return (-1, false, stdout.ToString(), stderr.ToString() + Environment.NewLine + $"Process timed out ({timeout?.TotalMinutes} minutes).", maxmem, upt, tpt);
                }
            }
            finally { cancelTokenRegistration?.Dispose(); }
        }

        /// <summary>Execute a command-line application.</summary>
        /// <param name="cmd">Command to execute, and its arguments</param>
        /// <param name="workingdir">Path to working directory. If null, the current one (<see cref="Environment.CurrentDirectory"/>) will be used.</param>
        /// <param name="timeout">If not null, the command will be killed after the given timeout.</param>
        /// <param name="canceltoken">Optional object used to kill the process on demand.</param>
        public static void RunInBackground(string cmd, Action<(int ExitCode, bool Success, string StdOut, string StdErr, Int64? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime)> exited, string workingdir = null, TimeSpan? timeout = null, CancellationToken? canceltoken = null, ProcessPriorityClass priority = ProcessPriorityClass.Normal, IOPriorityClass iopriority = IOPriorityClass.L02_NormalEffort, string StdIn = null, bool ignoreStatistics = false)
        {
            Task.Factory.StartNew(() =>
            {
                try { Thread.CurrentThread.Name = $"[{Thread.CurrentThread.ManagedThreadId}] Run command in background: {cmd}"; }
                catch { }

                (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) result = Run(cmd, workingdir, timeout, canceltoken, priority, iopriority, StdIn, ignoreStatistics);
                exited(result);
            }, TaskCreationOptions.LongRunning);
        }

        public static void SetIOPriority(int pid, IOPriorityClass iopriority)
        {
            if (Environment.OSVersion.Platform != PlatformID.Unix) throw new Exception($"IO Priority is only supported on Linux");
            int ioclass, ioclassdata = 0;
            switch (iopriority)
            {
                case IOPriorityClass.L00_Idle:
                    ioclass = 3;
                    break;
                case IOPriorityClass.L01_LowEffort:
                    ioclass = 2;
                    ioclassdata = 7;
                    break;
                case IOPriorityClass.L02_NormalEffort:
                    ioclass = 2;
                    ioclassdata = 4;
                    break;
                case IOPriorityClass.L03_HighEffort:
                    ioclass = 2;
                    ioclassdata = 0;
                    break;
                case IOPriorityClass.L04_Admin_RealTime_LowEffort:
                    ioclass = 1;
                    ioclassdata = 7;
                    break;
                case IOPriorityClass.L05_Admin_RealTime_AverageEffort:
                    ioclass = 1;
                    ioclassdata = 4;
                    break;
                case IOPriorityClass.L06_Admin_RealTime_ExtremelyHighEffort:
                    ioclass = 1;
                    ioclassdata = 0;
                    break;
                default: throw new NotImplementedException(iopriority.ToString());
            }

            int ExitCode;
            bool? Success = null;
            string StdOut;
            string StdErr1 = "";
            string StdErr2 = "";
            long? MaxRamUsedBytes;
            TimeSpan? UserProcessorTime;
            TimeSpan? TotalProcessorTime;
            try { (ExitCode, Success, StdOut, StdErr1, MaxRamUsedBytes, UserProcessorTime, TotalProcessorTime) = Run($"ionice -p {pid} -c {ioclass} -n {ioclassdata}", timeout: TimeSpan.FromMinutes(1), ignoreStatistics: true); }
            catch (InvalidOperationException) { } // ignore, probably the process already exited

            if (Success != true)
            {
                // try as root, in case user has been added as no-pass sudoer in /etc/sudoers as:
                // username ALL=(ALL) NOPASSWD: /usr/bin/ionice
                try { (ExitCode, Success, StdOut, StdErr2, MaxRamUsedBytes, UserProcessorTime, TotalProcessorTime) = Run($"sudo -n ionice -p {pid} -c {ioclass} -n {ioclassdata}", timeout: TimeSpan.FromMinutes(1), ignoreStatistics: true); }
                catch (InvalidOperationException) { } // ignore, probably the process already exited
            }

            if (Success != true) throw new Exception($"Unable to set I/O Priority '{iopriority}' of process {pid}: {StdErr1}. Trying again as root: {StdErr2}");
        }
        
        

        static Int64 Max(params long?[] values)
        {
            Int64 max = 0;
            foreach (Int64? value in values) if (value > max) max = value.Value;
            return max;
        }
    }
}
