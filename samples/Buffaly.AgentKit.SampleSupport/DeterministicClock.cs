namespace Buffaly.AgentKit.SampleSupport;

public sealed class DeterministicClock(DateTimeOffset utcNow)
{
    public DateTimeOffset UtcNow { get; } = utcNow;
    public static DeterministicClock Default { get; } = new(new DateTimeOffset(2026, 1, 15, 14, 30, 0, TimeSpan.Zero));
}
