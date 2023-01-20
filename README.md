# CmdOneLiner.NET

CmdOneLiner.NET easily executes command-line applications from .NET in a single line of code.

[![NuGet version (CmdOneLiner.NET)](https://img.shields.io/nuget/v/CmdOneLiner.NET?style=flat-square)](https://www.nuget.org/packages/CmdOneLiner.NET/)

## Usage

### Basic usage
```C#
CmdResult cmdOut = CmdOneLiner.Run("Sleeper 5", timeout: TimeSpan.FromSeconds(20));
Console.WriteLine($"Sleeper program executed in {cmdOut?.RunningFor.TotalSeconds} seconds ({cmdOut?.TotalProcessorTime?.TotalSeconds}), used {cmdOut?.MaxRamUsedBytes / 1024 / 1024} MiB of RAM, and printed to the standard output: \"{cmdOut?.StdOut}\".");
```

### Background process + callbacks
```C#
CmdOneLiner.RunInBackground("Sleeper 5", (cmdOut) => { Console.WriteLine($"Sleeper program executed in the background in {cmdOut.RunningFor.TotalSeconds} seconds."); });
```

### Background process + callbacks
```C#
CmdResult cmdOut = await CmdOneLiner.RunAsync("Sleeper 5");
Console.WriteLine($"Sleeper program executed asynchronously in {cmdOut.RunningFor.TotalSeconds} seconds");
```

## Features

- Extremely easy to use.
- Multiplatform (platform agnostic).
- Measures maximum RAM usage and CPU time.
- Optional command execution timeout.
- Optional token allows cancellation from another thread.
- Can be run synchronously, asynchronously, or with a callback.
