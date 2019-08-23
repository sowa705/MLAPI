namespace MLAPI.Engine
{
    public abstract class TimeManager
    {
        public abstract float Time { get; }
        public abstract int FrameNumber { get; }
        public abstract float DeltaTime { get; }
        public abstract float RealTimeSinceStartup { get; }
    }
}
