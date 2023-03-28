﻿using System.Diagnostics;

namespace onedrive_backup.Graph
{
    public class TransferSpeedHelper
    {
        private Stopwatch _stopWatch = new Stopwatch();
        private double _prevProgress = 0;
        private double _prevTimeStamp = 0;
        private double _speedCap;

        public TransferSpeedHelper(double? speedCapKBSec) 
        {
            _speedCap = speedCapKBSec != null ? speedCapKBSec.Value * 1000 : double.MaxValue;
        }
        public void Start()
        {
            Reset();
            _stopWatch.Start();
        }

        public void Reset()
        {
            _stopWatch.Reset();
            _prevProgress = 0;
            _prevProgress = 0;
        }

        public (double delay, double ratePerSecond) MarkAndCalcThrottle(double progress)
        {
            double bytesTransfered = progress - _prevProgress;
            var ellapsedMS = _stopWatch.ElapsedMilliseconds;
            double timefromPrev = ellapsedMS - _prevTimeStamp;
            double ratePerSecond = 1000 * bytesTransfered / timefromPrev;
            if (ratePerSecond > _speedCap)
            {
                var targetTimeForSpeedMS = bytesTransfered / _speedCap;
                var remainingTimeToWait = targetTimeForSpeedMS - _prevTimeStamp;
                return (remainingTimeToWait, ratePerSecond);
            }

            return (0, ratePerSecond);
        }
    }
}
