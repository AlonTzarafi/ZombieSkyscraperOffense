using Unity.Entities;

public partial struct ZombieSpawnerData : IComponentData
{
    // Config:
    public Entity prefab;
    public int zombiesLeftToSpawn;
    public int zombiesToSpawnOnStart;
    public float spawnRate;
}
