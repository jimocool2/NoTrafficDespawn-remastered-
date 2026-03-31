using System.Runtime.InteropServices;
using Unity.Entities;

namespace NoTrafficDespawn
{
    [StructLayout(LayoutKind.Sequential, Size = 1)]
    public struct UnstuckObject : IComponentData, IQueryTypeParameter
    {
    }
}
