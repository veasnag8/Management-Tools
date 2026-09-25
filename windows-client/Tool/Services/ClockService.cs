using System;

namespace Tool.Services
{
    public interface IClockService
    {
        DateTime GetCurrentUtc();
        bool IsClockRollback(DateTime lastRecordedUtc, out TimeSpan rollbackAmount);
    }

    public class ClockService : IClockService
    {
        // Tolerable clock drift threshold (e.g. 5 minutes)
        private static readonly TimeSpan MaxAllowedBackwardDrift = TimeSpan.FromMinutes(5);

        public DateTime GetCurrentUtc()
        {
            return DateTime.UtcNow;
        }

        public bool IsClockRollback(DateTime lastRecordedUtc, out TimeSpan rollbackAmount)
        {
            var now = GetCurrentUtc();
            rollbackAmount = TimeSpan.Zero;

            if (lastRecordedUtc == DateTime.MinValue || lastRecordedUtc == default)
            {
                return false;
            }

            if (now < lastRecordedUtc - MaxAllowedBackwardDrift)
            {
                rollbackAmount = lastRecordedUtc - now;
                return true;
            }

            return false;
        }
    }
}
