using Unity.Entities;

namespace NoTrafficDespawn
{
    public struct StuckObject : IComponentData, IQueryTypeParameter
    {
        public int frameCount;

        public StuckObject(int frameCount)
        {
            this.frameCount = frameCount;
        }
    }
}
