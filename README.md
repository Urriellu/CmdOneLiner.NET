# CmdOneLiner.NET

CmdOneLiner.NET easily executes command-line applications from .NET in a single line of code.

## Usage

```C#
using System;

namespace CmdOneLinerNET.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Sleeping for 5 seconds...");
            (int ExitCode, bool Success, string StdOut, string StdErr, long MaxRamUsedBytes, TimeSpan UserProcessorTime, TimeSpan TotalProcessorTime) = CmdOneLiner.Run("Sleeper.exe 5", timeout: TimeSpan.FromSeconds(20));
            Console.WriteLine($"Sleeper program executed in {UserProcessorTime.TotalSeconds} seconds ({TotalProcessorTime.TotalSeconds}), used {MaxRamUsedBytes / 1024 / 1024} MiB of RAM, and printed to the standard output: \"{StdOut}\".");
        }
    }
}
```

## Features

- Extremely easy to use.
- Multiplatform (platform agnostic).
- Measures maximum RAM usage and CPU time.
- Optional command execution timeout.
- Optional token allows cancellation from another thread.
