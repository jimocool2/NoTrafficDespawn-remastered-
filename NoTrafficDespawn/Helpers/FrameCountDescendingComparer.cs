using System.Collections.Generic;
using Unity.Collections;

namespace NoTrafficDespawn
{
    /// <summary>
    /// Sorts chunk indices descending by <see cref="StuckObject.frameCount"/> so that
    /// entities that have been stuck longest are despawned first when the per-frame
    /// removal budget is limited.
    /// </summary>
    struct FrameCountDescendingComparer : IComparer<int>
    {
        // ReadOnly slice — the array is already written back before we sort.
        [ReadOnly] public NativeArray<StuckObject> stuckObjects;

        public int Compare(int x, int y)
        {
            // Descending: larger frameCount (stuck longer) comes first.
            return stuckObjects[y].frameCount.CompareTo(stuckObjects[x].frameCount);
        }
    }
}