public sealed class FakeClock : IClock
{
    public double UtcNow { get; set; } = 1_800_000_000;
    public double Realtime { get; set; }

    public void Advance(double seconds)
    {
        UtcNow += seconds;
        Realtime += seconds;
    }
}
