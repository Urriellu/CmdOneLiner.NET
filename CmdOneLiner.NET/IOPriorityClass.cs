using System;
using System.Collections.Generic;
using System.Text;

namespace CmdOneLinerNET
{
    public enum IOPriorityClass
    {
        L00_Idle,
        L01_LowEffort,
        L02_NormalEffort,
        L03_HighEffort,
        L04_Admin_RealTime_LowEffort,
        L05_Admin_RealTime_AverageEffort,
        L06_Admin_RealTime_ExtremelyHighEffort
    }
}
