using System;
using MergeLegion.Core;

namespace MergeLegion.Tests
{
    public sealed class FakeTimeService : ITimeService
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        public DateTime LocalNow => UtcNow.ToLocalTime();
        public void Advance(TimeSpan span) => UtcNow += span;
    }
}
