using System;

/// <summary>Time source for game logic. Production uses <see cref="SystemClock"/>; tests inject a fake.</summary>
public interface IClock
{
    /// <summary>Wall-clock UTC time in Unix seconds (save stamps, boost end, offline earnings).</summary>
    double UtcNow { get; }
    /// <summary>Monotonic seconds since app start (ad pacing and cooldowns).</summary>
    double Realtime { get; }
}

public sealed class SystemClock : IClock
{
    public double UtcNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    public double Realtime => UnityEngine.Time.realtimeSinceStartupAsDouble;
}

/// <summary>Process-wide clock. Swap <see cref="Current"/> in tests (and restore it afterwards).</summary>
public static class GameClock
{
    public static IClock Current = new SystemClock();
}
