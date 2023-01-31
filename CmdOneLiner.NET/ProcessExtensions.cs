using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CmdOneLinerNET
{
    public static class ProcessExtensions
    {
        public static void SetIOPriority(this Process p, IOPriorityClass iopriority) => CmdOneLiner.SetIOPriority(p.Id, iopriority);

        public static bool HasExitedSafe(this Process p)
        {
            try { return p.HasExited; }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }
}
