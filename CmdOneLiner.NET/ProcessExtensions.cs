using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace CmdOneLinerNET
{
    public static class ProcessExtensions
    {
        public static void SetIOPriority(this Process p, IOPriorityClass iopriority) => CmdOneLiner.SetIOPriority(p.Id, iopriority);
    }
}
