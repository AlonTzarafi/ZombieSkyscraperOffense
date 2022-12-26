using Unity.Entities;
using Unity.Mathematics;

public partial struct ZombieData : IComponentData
{
    public float preferredDistance;
    public float flockRadius;
    public float bobPhaseScale;
    public float3 velocity;
}
