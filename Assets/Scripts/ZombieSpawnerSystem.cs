using System;
using System.Runtime.CompilerServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class ZombieSpawnerSystem : SystemBase
{
    public const float zombieHeight = 0.5f;

    private double timeOfLastSpawn;
    private bool spawnedStartingZombies = false;


    protected override void OnUpdate()
    {
        var myCameraController = UnityEngine.GameObject.FindObjectOfType<MyCameraController>();
        var canSpawnStartingZombies = myCameraController != null;
        if (!spawnedStartingZombies && canSpawnStartingZombies) {
            var hasZombieSpawnerData = SystemAPI.HasSingleton<ZombieSpawnerData>();
            if (hasZombieSpawnerData) {
                SpawnStartingZombies();
                spawnedStartingZombies = true;
            } else {
                return;
            }
        }
        
        var zombieSpawnerData = SystemAPI.GetSingleton<ZombieSpawnerData>();

        var ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var rotation = quaternion.identity;

        var timeSinceLastSpawn = SystemAPI.Time.ElapsedTime - timeOfLastSpawn;
        var countToSpawnNow = (int)math.floor(timeSinceLastSpawn / zombieSpawnerData.spawnRate);
        countToSpawnNow = math.min(countToSpawnNow, zombieSpawnerData.zombiesLeftToSpawn);

        if (countToSpawnNow > 0) {
            for (int i = 0; i < countToSpawnNow; i++) {
                ecb = SpawnZombie(ecb, zombieSpawnerData.prefab, float3.zero);
            }

            timeOfLastSpawn = SystemAPI.Time.ElapsedTime;
            zombieSpawnerData.zombiesLeftToSpawn -= countToSpawnNow;
            SystemAPI.SetSingleton<ZombieSpawnerData>(zombieSpawnerData);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static EntityCommandBuffer SpawnZombie(EntityCommandBuffer ecb, Entity zombiePrefab, float3 positionOffset)
    {
        var entity = ecb.Instantiate(zombiePrefab);

        var zombieSpawnXDistance = 200f;

        var scale = UnityEngine.Random.Range(38f, 42f);

        float3 translation;
        translation.x = UnityEngine.Random.Range(-zombieSpawnXDistance, zombieSpawnXDistance);
        // translation.y = UnityEngine.Random.Range(-num, num);
        translation.y = zombieHeight;
        // translation.z = UnityEngine.Random.Range(-num, num);
        translation.z = 0.0f;

        ecb.SetComponent<LocalTransform>(entity, new LocalTransform()
        {
            Position = translation + positionOffset,
            Rotation = quaternion.identity,
            Scale = scale,
        });

        ecb.AddComponent<ZombieData>(entity, new ZombieData()
        {
            preferredDistance = UnityEngine.Random.Range(4f, 8f),
            flockRadius = UnityEngine.Random.Range(2f, 60f),
            bobPhaseScale = UnityEngine.Random.Range(2f, 6f),
            velocity = new float3(
                UnityEngine.Random.Range(-2f, 2f),
                0f,
                UnityEngine.Random.Range(2.5f, 6.5f)),
        });

        ecb.AddSharedComponent<LeChunkification>(entity, new LeChunkification()
        {
            chonkyChunker = UnityEngine.Random.Range(0, LeChunkification.maxValue),
        });
        return ecb;
    }

    private void SpawnStartingZombies()
    {
        var zombieSpawnerData = SystemAPI.GetSingleton<ZombieSpawnerData>();

        var ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        var myCameraController = UnityEngine.GameObject.FindObjectOfType<MyCameraController>();
        var spawnRandomZombiesUpToZ = myCameraController.transform.position.z + 300f;

        var zombieCountToSpawnOnStart = zombieSpawnerData.zombiesToSpawnOnStart;

        for (int i = 0; i < zombieCountToSpawnOnStart; i++) {
            var offset = new float3(0f, 0f, UnityEngine.Random.Range(0f, spawnRandomZombiesUpToZ));
            ecb = SpawnZombie(ecb, zombieSpawnerData.prefab, offset);
        }

        zombieSpawnerData.zombiesLeftToSpawn -= zombieCountToSpawnOnStart;
        SystemAPI.SetSingleton<ZombieSpawnerData>(zombieSpawnerData);
    }
}
