using System;
using System.Diagnostics;

namespace Sleeper
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1 || !int.TryParse(args[0], out int seconds))
            {
                Console.Error.WriteLine($"Usage: {Process.GetCurrentProcess().ProcessName} N{Environment.NewLine}\tN - Amount of seconds to freeze this application, while utilizing one CPU to 100%.");
            }
            else
            {
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.Elapsed < TimeSpan.FromSeconds(seconds)) ; // loop which utilizes 100% of one CPU core
                Console.Write($"Finished sleeping {seconds} seconds.");
            }
        }
    }
}
