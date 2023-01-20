using System;
using System.Threading.Tasks;

namespace CmdOneLinerNET.Sample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            {
                // Basic usage
                Console.WriteLine("Sleeping for 5 seconds...");
                CmdResult cmdOut = CmdOneLiner.Run("Sleeper 5", timeout: TimeSpan.FromSeconds(20));
                Console.WriteLine($"Sleeper program executed in {cmdOut?.RunningFor.TotalSeconds} seconds ({cmdOut?.TotalProcessorTime?.TotalSeconds}), used {cmdOut?.MaxRamUsedBytes / 1024 / 1024} MiB of RAM, and printed to the standard output: \"{cmdOut?.StdOut}\".");
            }

            {
                // Option #2 - Background process + callback
                CmdOneLiner.RunInBackground("Sleeper 5", (cmdOut) => { Console.WriteLine($"Sleeper program executed in the background in {cmdOut.RunningFor.TotalSeconds} seconds."); });
            }

            {
                // Option #3 - Async
                CmdResult cmdOut = await CmdOneLiner.RunAsync("Sleeper 5");
                Console.WriteLine($"Sleeper program executed asynchronously in {cmdOut.RunningFor.TotalSeconds} seconds");
            }
        }
    }
}
