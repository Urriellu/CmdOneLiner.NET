using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace CmdOneLinerNET.Test
{
    [TestClass]
    public class OneLinerTests
    {
        [TestMethod, Timeout(10 * 1000)]
        public void T01_BasicTest()
        {
            (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) = CmdOneLiner.Run($"Sleeper.exe 1");
            Assert.AreEqual(ExitCode, 0);
            Assert.AreEqual(Success, true);
            Assert.IsTrue(MaxRamUsedBytes > 0 && MaxRamUsedBytes < 50 * 1000 * 1000);
            Assert.IsTrue(UserProcessorTime > TimeSpan.Zero);
            Assert.IsTrue(UserProcessorTime < TimeSpan.FromSeconds(5));
            Assert.IsTrue(TotalProcessorTime > TimeSpan.Zero);
            Assert.IsTrue(TotalProcessorTime < TimeSpan.FromSeconds(5));
        }

        [TestMethod, Timeout(10 * 1000)]
        public void T02_TimeoutTest()
        {
            (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) = CmdOneLiner.Run($"Sleeper.exe 10", timeout: TimeSpan.FromSeconds(1));
            Assert.AreNotEqual(ExitCode, 0);
            Assert.AreEqual(Success, false);
            Assert.IsTrue(UserProcessorTime < TimeSpan.FromSeconds(5));
            Assert.IsTrue(StdErr.Contains("timed out"));
        }

        [TestMethod, Timeout(10 * 1000)]
        public void T03_KillProcessTest()
        {
            System.Threading.CancellationTokenSource cancelSrc = new System.Threading.CancellationTokenSource();
            System.Threading.CancellationToken token = cancelSrc.Token;

            // Create a new thread/task that will cancel the command after 3 seconds
            Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(t =>
            {
                cancelSrc.Cancel();
            });

            (int ExitCode, bool Success, string StdOut, string StdErr, long? MaxRamUsedBytes, TimeSpan? UserProcessorTime, TimeSpan? TotalProcessorTime) = CmdOneLiner.Run($"Sleeper.exe 60", canceltoken: token);
            Assert.AreNotEqual(ExitCode, 0);
            Assert.AreEqual(Success, false);
            Assert.IsTrue(UserProcessorTime > TimeSpan.FromSeconds(1));
            Assert.IsTrue(UserProcessorTime < TimeSpan.FromSeconds(10));
            Assert.IsTrue(StdErr.Contains("killed"));
        }
    }
}
