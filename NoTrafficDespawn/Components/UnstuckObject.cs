using System.Runtime.InteropServices;
using Unity.Entities;

namespace NoTrafficDespawn.Components
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct UnstuckObject : IComponentData
    {
    }
}
