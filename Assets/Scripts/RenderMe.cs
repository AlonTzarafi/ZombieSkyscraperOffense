using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public struct RenderMe : ISharedComponentData
{
    public int meshIndex;
}

