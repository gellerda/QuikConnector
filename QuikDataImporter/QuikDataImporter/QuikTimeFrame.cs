using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FancyCandles;

namespace QuikDataImporter
{
    internal enum QuikTimeFrame
    {
        INTERVAL_TICK = 0,
        INTERVAL_M1 = 1,
        INTERVAL_M2 = 2,
        INTERVAL_M3 = 3,
        INTERVAL_M4 = 4,
        INTERVAL_M5 = 5,
        INTERVAL_M6 = 6,
        INTERVAL_M10 = 10,
        INTERVAL_M15 = 15,
        INTERVAL_M20 = 20,
        INTERVAL_M30 = 30,
        INTERVAL_H1 = 60,
        INTERVAL_H2 = 120,
        INTERVAL_H4 = 240,
        INTERVAL_D1 = 1440,
        INTERVAL_W1 = 10080,
        INTERVAL_MN1 = 23200
    }
    //************************************************************************************************************************************
    internal static class ConvertToQuikTimeFrame
    {
        public static QuikTimeFrame FromTimeFrame(FancyCandles.TimeFrame timeFrame)
        {
            if (timeFrame == TimeFrame.Monthly)
                return QuikTimeFrame.INTERVAL_MN1;
            else if (((int)timeFrame) >= 0)
                return (QuikTimeFrame)timeFrame;
            else
                throw new ArgumentOutOfRangeException("Second time frames is not supported.");
        }
    }
}
