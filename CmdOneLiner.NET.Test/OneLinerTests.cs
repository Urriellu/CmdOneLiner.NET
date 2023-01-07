using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CmdOneLinerNET.Test
{
    [TestClass]
    public class OneLinerTests
    {
        private readonly string nameSleeperBinary = System.IO.File.Exists("Sleeper.exe") ? "Sleeper.exe" : "Sleeper";

        [TestMethod, Timeout(10 * 1000)]
        public void T01_BasicTest(bool multithreaded = false)
        {
            (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) = CmdOneLiner.Run($"{nameSleeperBinary} 1");
            Assert.AreEqual(ExitCode, 0);
            Assert.AreEqual(Success, true);
            if (multithreaded)
            {
                Assert.IsTrue(MaxRamUsedBytes >= 0 && MaxRamUsedBytes < 50 * 1000 * 1000);
                Assert.IsTrue(UserProcessorTime >= TimeSpan.Zero);
                Assert.IsTrue(TotalProcessorTime >= TimeSpan.Zero);
            }
            else
            {
                Assert.IsTrue(MaxRamUsedBytes > 0 && MaxRamUsedBytes < 50 * 1000 * 1000);
                Assert.IsTrue(UserProcessorTime > TimeSpan.Zero);
                Assert.IsTrue(TotalProcessorTime > TimeSpan.Zero);
            }
            Assert.IsTrue(UserProcessorTime < TimeSpan.FromSeconds(5));
            Assert.IsTrue(TotalProcessorTime < TimeSpan.FromSeconds(5));
        }

        [TestMethod, Timeout(10 * 1000)]
        public void T02_TimeoutTest()
        {
            (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) = CmdOneLiner.Run($"{nameSleeperBinary} 10", timeout: TimeSpan.FromSeconds(1));
            Assert.AreNotEqual(ExitCode, 0);
            Assert.AreEqual(Success, false);
            Assert.IsTrue(UserProcessorTime < TimeSpan.FromSeconds(5));
            Assert.IsTrue(StdErr.Contains("timed out"));
        }

        [TestMethod, Timeout(10 * 1000)]
        public void T03_KillProcessTest(bool multithreaded = false)
        {
            System.Threading.CancellationTokenSource cancelSrc = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = cancelSrc.Token;

            // Create a new thread/task that will cancel the command after 3 seconds
            Debug.WriteLine($"Starting at {DateTime.Now}");
            Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(t =>
            {
                Debug.WriteLine($"Canceling at {DateTime.Now}");
                cancelSrc.Cancel();
            });

            Stopwatch execTime = Stopwatch.StartNew();
            (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) = CmdOneLiner.Run($"{nameSleeperBinary} 60", canceltoken: token);
            execTime.Stop();
            Assert.AreNotEqual(ExitCode, 0);
            Assert.AreEqual(Success, false);
            if (multithreaded) Assert.IsTrue(UserProcessorTime >= TimeSpan.FromSeconds(0));
            else Assert.IsTrue(UserProcessorTime > TimeSpan.FromSeconds(0));
            Assert.IsTrue(UserProcessorTime < TimeSpan.FromSeconds(10));
            Assert.IsTrue(execTime.Elapsed >= TimeSpan.FromSeconds(3));
            Assert.IsTrue(execTime.Elapsed < TimeSpan.FromSeconds(multithreaded ? 120 : 10));
            Assert.IsTrue(StdErr.Contains("killed"));
        }

        [TestMethod, Timeout(5 * 60 * 1000)]
        public void T04_StressTest()
        {
            const int amountThreadsPerTest = 5;
            TimeSpan runFor = TimeSpan.FromSeconds(30);
            Stopwatch runningFor = Stopwatch.StartNew();
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < amountThreadsPerTest; i++) {
                tasks.Add(Task.Factory.StartNew(() => { while(runningFor.Elapsed < runFor) T01_BasicTest(multithreaded: true); }));
                tasks.Add(Task.Factory.StartNew(() => { while(runningFor.Elapsed < runFor) T02_TimeoutTest(); }));
                tasks.Add(Task.Factory.StartNew(() => { while(runningFor.Elapsed < runFor) T03_KillProcessTest(multithreaded: true); }));
            }
            Task.WaitAll(tasks.ToArray());
        }
    }
}
