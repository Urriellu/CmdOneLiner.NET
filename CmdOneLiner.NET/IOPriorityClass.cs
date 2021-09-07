using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace System.IO
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

    public static class IOPriorityClassExtensions
    {
        public static ProcessPriorityClass GetSimilarProcessPriority(this IOPriorityClass ioPriorityClass)
        {
            switch (ioPriorityClass)
            {
                case IOPriorityClass.L00_Idle: return ProcessPriorityClass.Idle;
                case IOPriorityClass.L01_LowEffort: return ProcessPriorityClass.BelowNormal;
                case IOPriorityClass.L02_NormalEffort: return ProcessPriorityClass.Normal;
                case IOPriorityClass.L03_HighEffort: return ProcessPriorityClass.AboveNormal;
                case IOPriorityClass.L04_Admin_RealTime_LowEffort: return ProcessPriorityClass.High;
                case IOPriorityClass.L05_Admin_RealTime_AverageEffort:
                case IOPriorityClass.L06_Admin_RealTime_ExtremelyHighEffort:
                    return ProcessPriorityClass.RealTime;
                default: throw new NotImplementedException(ioPriorityClass.ToString());
            }
        }
    }
}
