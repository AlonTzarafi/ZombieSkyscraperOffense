using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Entities;

public class ZombieSpawnerDataAuthoring : MonoBehaviour
{
    public GameObject prefab;
    public int zombiesLeftToSpawn;
    public int zombiesToSpawnOnStart;
    public float spawnRate;
}

public class ZombieSpawnerDataAuthoringBaker : Baker<ZombieSpawnerDataAuthoring>
{
    public override void Bake(ZombieSpawnerDataAuthoring authoring)
    {
        AddComponent(new ZombieSpawnerData
        {
            prefab = GetEntity(authoring.prefab),
            zombiesLeftToSpawn = authoring.zombiesLeftToSpawn,
            zombiesToSpawnOnStart = authoring.zombiesToSpawnOnStart,
            spawnRate = authoring.spawnRate,
        });
    }
}

