using Unity.Entities;

namespace NoTrafficDespawn.Components
{
    public struct StuckObject : IComponentData
    {
        public int FrameCount { get; set; }

        public StuckObject(int frameCount)
        {
            this.FrameCount = frameCount;
        }
    }
}
