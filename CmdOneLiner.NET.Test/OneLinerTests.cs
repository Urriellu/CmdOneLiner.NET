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

        [TestMethod, Timeout(10 * 1000)] public void T01_BasicTest() => T01_BasicTest_Logic();
        
        public void T01_BasicTest_Logic(bool multithreaded = false)
        {
            CmdResult cmdOut = CmdOneLiner.Run($"{nameSleeperBinary} 1");
            Assert.AreEqual(cmdOut?.ExitCode, 0);
            Assert.AreEqual(cmdOut?.Success, true);
            if (multithreaded)
            {
                Assert.IsTrue(cmdOut.MaxRamUsedBytes >= 0 && cmdOut.MaxRamUsedBytes < 50 * 1000 * 1000);
                Assert.IsTrue(cmdOut.UserProcessorTime >= TimeSpan.Zero);
                Assert.IsTrue(cmdOut.TotalProcessorTime >= TimeSpan.Zero);
            }
            else
            {
                Assert.IsTrue(cmdOut.MaxRamUsedBytes > 0 && cmdOut.MaxRamUsedBytes < 50 * 1000 * 1000);
                Assert.IsTrue(cmdOut.UserProcessorTime > TimeSpan.Zero);
                Assert.IsTrue(cmdOut.TotalProcessorTime > TimeSpan.Zero);
            }
            Assert.IsTrue(cmdOut.RunningFor < TimeSpan.FromSeconds(5));
            Assert.IsTrue(cmdOut.RunningFor < TimeSpan.FromSeconds(5));
        }

        [TestMethod, Timeout(10 * 1000)]
        public void T02_TimeoutTest()
        {
            CmdResult cmdOut = CmdOneLiner.Run($"{nameSleeperBinary} 10", timeout: TimeSpan.FromSeconds(1));
            Assert.AreNotEqual(cmdOut.ExitCode, 0);
            Assert.AreEqual(cmdOut.Success, false);
            Assert.IsTrue(cmdOut.RunningFor < TimeSpan.FromSeconds(5));
            Assert.IsTrue(cmdOut.StdErr.Contains("timed out"));
        }

        [TestMethod, Timeout(10 * 1000)] public void T03_KillProcessTest() => T03_KillProcessTest_Logic(false);
        
        public void T03_KillProcessTest_Logic(bool multithreaded = false)
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

            CmdResult cmdOut = CmdOneLiner.Run($"{nameSleeperBinary} 60", canceltoken: token);
            Assert.AreNotEqual(cmdOut.ExitCode, 0);
            Assert.AreEqual(cmdOut.Success, false);
            if (multithreaded) Assert.IsTrue(cmdOut.UserProcessorTime >= TimeSpan.FromSeconds(0));
            else Assert.IsTrue(cmdOut.UserProcessorTime > TimeSpan.FromSeconds(0));
            Assert.IsTrue(cmdOut.UserProcessorTime < TimeSpan.FromSeconds(1));
            Assert.IsTrue(cmdOut.RunningFor >= TimeSpan.FromSeconds(4));
            Assert.IsTrue(cmdOut.RunningFor < TimeSpan.FromSeconds(multithreaded ? 120 : 10));
            Assert.IsTrue(cmdOut.StdErr.Contains("killed"));
        }

        [TestMethod, Timeout(5 * 60 * 1000)]
        public void T04_StressTest()
        {
            const int amountThreadsPerTest = 5;
            TimeSpan runFor = TimeSpan.FromSeconds(30);
            Stopwatch runningFor = Stopwatch.StartNew();
            List<Task> tasks = new List<Task>();
            for (int i = 0; i < amountThreadsPerTest; i++) {
                tasks.Add(Task.Factory.StartNew(() => { while(runningFor.Elapsed < runFor) T01_BasicTest_Logic(multithreaded: true); }));
                tasks.Add(Task.Factory.StartNew(() => { while(runningFor.Elapsed < runFor) T02_TimeoutTest(); }));
                tasks.Add(Task.Factory.StartNew(() => { while(runningFor.Elapsed < runFor) T03_KillProcessTest_Logic(multithreaded: true); }));
            }
            Task.WaitAll(tasks.ToArray());
        }
    }
}
