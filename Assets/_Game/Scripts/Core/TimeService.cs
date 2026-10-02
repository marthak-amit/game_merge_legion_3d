using System;

namespace MergeLegion.Core
{
    /// <summary>Single source of time so offline rewards, missions and tests can be driven deterministically.</summary>
    public interface ITimeService
    {
        DateTime UtcNow { get; }
        DateTime LocalNow { get; }
    }

    public sealed class SystemTimeService : ITimeService
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public DateTime LocalNow => DateTime.Now;
    }
}
