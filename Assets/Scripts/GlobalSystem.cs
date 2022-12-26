using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

public partial class GlobalSystem : SystemBase
{
    public const int startingPopulation = 1000;

    public int3 buildingGridSize;
    public float wallSize;
    public float3 buildingCenter;
    public float3 buildingFrontEntrance;

    private double timeOfLastSpawn;

    private GlobalMonoBehaviour globalMonoBehaviour;
    private GameObject buildingGameObject;
    private bool firstFrameAlreadyRan;
    private double timeOfLastProjectileSpawn;

    private int buildingPopulation = startingPopulation;
    private int zombiesEnteredVacantBuilding;

    protected override void OnCreate()
    {
    }

    private void Init()
    {
        globalMonoBehaviour = UnityEngine.GameObject.FindObjectOfType<GlobalMonoBehaviour>();
        buildingGameObject = UnityEngine.GameObject.Find("Building");

        buildingGridSize = new int3(18, 200, 18);
        wallSize = 20f;
        buildingCenter = (float3)buildingGameObject.transform.position;
        buildingCenter.y = 0;
        buildingFrontEntrance = buildingCenter + new float3(0, 0, -buildingGameObject.transform.localScale.z / 2f);
    }

    protected override void OnUpdate()
    {
        var hasGlobalData = SystemAPI.HasSingleton<GlobalData>();
        if (!hasGlobalData) {
            return;
        }

        if (!firstFrameAlreadyRan) {
            Init();
            // CreateBuilding();
        }


        SpawnProjectilesFromBuilding();

        UpdateProjectiles();

        UpdateFires();

        UpdatePopulation();

        UpdateEndgame();

        Dependency.Complete();

        firstFrameAlreadyRan = true;
    }


    private void SpawnProjectilesFromBuilding()
    {
        var globalData = SystemAPI.GetSingleton<GlobalData>();

        var projectileScale = globalMonoBehaviour.projectileScale;

        var projectilePrefab = globalData.prefabProjectile1;
        if (globalMonoBehaviour.microwaveChance > UnityEngine.Random.Range(0f, 1f)) {
            projectilePrefab = globalData.prefabProjectile2;
        }

        var timeSinceLastSpawn = SystemAPI.Time.ElapsedTime - timeOfLastProjectileSpawn;
        // UnityEngine.Debug.Log($"timeSinceLastSpawn {timeSinceLastSpawn}");

        var populationStrength = buildingPopulation / (float)startingPopulation;

        var currentTimeBetweenProjectileThrows = 10000000f;
        if (populationStrength > 0) {
            currentTimeBetweenProjectileThrows = globalMonoBehaviour.timeBetweenProjectileThrows / populationStrength;
        }

        var countToSpawnNow = (int)math.floor(timeSinceLastSpawn / currentTimeBetweenProjectileThrows);

        if (populationStrength == 0) {
            // Can never spawn projectiles if there is no population
            countToSpawnNow = 0;
        }

        if (countToSpawnNow > 0) {
            timeOfLastProjectileSpawn = SystemAPI.Time.ElapsedTime;

            // UnityEngine.Debug.Log("Spawning " + countToSpawnNow + " projectiles");

            var translation = new float3();

            var ecb =
                SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(World.Unmanaged);

            for (int i = 0; i < countToSpawnNow; i++) {
                var entity = ecb.Instantiate(projectilePrefab);


                var projectileSpawnPlace =
                    buildingFrontEntrance +
                    new float3(
                        UnityEngine.Random.Range(-buildingGridSize.x /2, buildingGridSize.x /2) * wallSize,
                        UnityEngine.Random.Range(0f, buildingGridSize.y) * wallSize,
                        // 1 * wallSize
                        0f
                    );

                translation = projectileSpawnPlace;

                ecb.SetComponent<LocalTransform>(entity, new LocalTransform()
                {
                    Position = translation,
                    Rotation = quaternion.Euler(UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f), UnityEngine.Random.Range(0f, 360f)),
                    Scale = projectileScale,
                });

                ecb.AddComponent<ProjectileData>(entity, new ProjectileData()
                {
                    velocity = new float3(
                        UnityEngine.Random.Range(-2.5f, 2.5f),
                        0f,
                        UnityEngine.Random.Range(-60f, -5f)),
                });

                ecb.AddSharedComponent<LeChunkification>(entity, new LeChunkification()
                {
                    chonkyChunker = UnityEngine.Random.Range(0, LeChunkification.maxValue),
                });
            }
        }
    }

    private void UpdateProjectiles()
    {
        var projectileYAcceleration = globalMonoBehaviour.projectileYAcceleration;
        var projectileYMinVel = globalMonoBehaviour.projectileYMinVel;

        var deltaTime = (float)SystemAPI.Time.DeltaTime;

        var slightRandomRotationEuler = new float3(4f) * deltaTime;
        var slightRandomRotation = quaternion.Euler(slightRandomRotationEuler);

        Dependency.Complete();
        
        Dependency = Entities
            .WithBurst()
            .ForEach((Entity entity, ref LocalTransform localTransform, ref ProjectileData projectileData) =>
            {
                projectileData.velocity.y += projectileYAcceleration;
                projectileData.velocity.y = math.max(projectileData.velocity.y, projectileYMinVel);
                localTransform.Position += projectileData.velocity * deltaTime;
                localTransform.Rotation = math.mul(localTransform.Rotation, slightRandomRotation);
            })
            .ScheduleParallel(Dependency);

        // Dependency.Complete();

        var ecb =
            SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        var ecbParallel = ecb.AsParallelWriter();

        var projectilesToRemove = new NativeQueue<Entity>(Allocator.TempJob);
        var projectilesToRemoveParallelWriter = projectilesToRemove.AsParallelWriter();
        var projectilesToRemovePositions = new NativeQueue<float3>(Allocator.TempJob);
        var projectilesToRemovePositionsParallelWriter = projectilesToRemovePositions.AsParallelWriter();

        Dependency = Entities
            .WithBurst()
            .ForEach((Entity entity, ref LocalTransform localTransform, ref ProjectileData projectileData) =>
            {
                if (localTransform.Position.y < 0f) {
                    projectilesToRemoveParallelWriter.Enqueue(entity);
                    projectilesToRemovePositionsParallelWriter.Enqueue(localTransform.Position);
                }
            })
            .ScheduleParallel(Dependency);

        Dependency.Complete();

        AllocatorHandle allocatorHandle = AllocatorManager.Persistent;
        //
        var projectilesToRemoveFinal = projectilesToRemove.ToArray(allocatorHandle);
        var projectilesToRemovePositionsFinal = projectilesToRemovePositions.ToArray(allocatorHandle);

        var prefabExplosion = SystemAPI.GetSingleton<GlobalData>().prefabExplosion;
        var explosionScale = globalMonoBehaviour.explosionScale;


        var explosionsToSpawn = new NativeQueue<float3>(Allocator.TempJob);
        var explosionsToSpawnParallelWriter = explosionsToSpawn.AsParallelWriter();

        Dependency = Job
            .WithBurst()
            // .WithoutBurst()
            .WithCode(() =>
            {
                for (int i = 0; i < projectilesToRemoveFinal.Length; i++) {
                    var projectileEntity = projectilesToRemoveFinal[i];
                    var projectilePosition = projectilesToRemovePositionsFinal[i];

                    var isMicrowave = SystemAPI.HasComponent<Microwave>(projectileEntity);
                    

                    var microwaveHitPosition = projectilePosition;
                    microwaveHitPosition.y = 0f;
                    if (isMicrowave) {
                        // Microwave hit ground
                        // Notify to add fire
                        explosionsToSpawnParallelWriter.Enqueue(microwaveHitPosition);
                    }
                }
            })
            .Schedule(Dependency);

        Dependency.Complete();
        
        Dependency = Entities
            .WithBurst()
            // .WithoutBurst()
            .ForEach((Entity entity, int entityInQueryIndex, in LocalTransform localTransform, in ZombieData zombieData) =>
            {
                for (int i = 0; i < projectilesToRemoveFinal.Length; i++) {
                    var projectileEntity = projectilesToRemoveFinal[i];
                    var projectilePosition = projectilesToRemovePositionsFinal[i];

                    var isMicrowave = SystemAPI.HasComponent<Microwave>(projectileEntity);

                    var pos1 = projectilePosition;
                    var pos2 = localTransform.Position;
                    pos1.y = 0f;
                    pos2.y = 0f;
                    
                    var distance = math.distance(pos1, pos2);
                    var killRadius = 7f;
                    if (isMicrowave) {
                        // Microwave hit ground
                        killRadius = 20f;
                    }
                    if (distance < killRadius) {
                        ecbParallel.DestroyEntity(entityInQueryIndex, entity);
                    }
                }
            })
            .WithNativeDisableParallelForRestriction(projectilesToRemoveFinal)
            .WithNativeDisableParallelForRestriction(projectilesToRemovePositionsFinal)
            .ScheduleParallel(Dependency);


        Dependency.Complete();

        // Also kill player if any of the projectiles hit the player

        var myCameraController = GameObject.FindObjectOfType<MyCameraController>();
        var playerTransform = myCameraController.transform;
        for (int i = 0; i < projectilesToRemoveFinal.Length; i++) {
            var projectileEntity = projectilesToRemoveFinal[i];
            var projectilePosition = projectilesToRemovePositionsFinal[i];

            var isMicrowave = SystemAPI.HasComponent<Microwave>(projectileEntity);

            var pos1 = projectilePosition;
            var pos2 = playerTransform.position;
            pos1.y = 0f;
            pos2.y = 0f;
            
            var distance = math.distance(pos1, pos2);
            var killRadius = 7f;
            if (isMicrowave) {
                // Microwave hit ground
                killRadius = 20f;
            }
            killRadius *= 0.9f; // Make it a bit easier to dodge
            if (distance < killRadius) {
                myCameraController.TurnIntoCamera();
            }
        }
        
        // Spawn explosions
        
        Job
            .WithBurst()
            .WithCode(() =>
            {
                while (explosionsToSpawn.TryDequeue(out var explosionPosition)) {
                    var fireEntity = ecb.Instantiate(prefabExplosion);

                    ecb.SetComponent<LocalTransform>(fireEntity, new LocalTransform()
                    {
                        Position = explosionPosition,
                        Rotation = quaternion.identity,
                        Scale = explosionScale,
                    });

                    ecb.AddComponent<FireData>(fireEntity, new FireData() {});

                    ecb.AddSharedComponent<LeChunkification>(fireEntity, new LeChunkification()
                    {
                        chonkyChunker = UnityEngine.Random.Range(0, LeChunkification.maxValue),
                    });
                }
            }).Run();

        ecb.DestroyEntity(projectilesToRemoveFinal);

        projectilesToRemove.Dispose();
        projectilesToRemovePositions.Dispose();
        explosionsToSpawn.Dispose();
    }

    private void UpdateFires()
    {
        var deltaTime = (float)SystemAPI.Time.DeltaTime;
        var fireUpVelocity = 20f;

        var ecbParallel = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged)
            .AsParallelWriter();

        Entities
            .WithBurst()
            .ForEach((Entity entity, int entityInQueryIndex, ref LocalTransform localTransform, ref FireData fireData) =>
            {
                localTransform.Position.y += fireUpVelocity * deltaTime;
                localTransform.Scale *= 0.97f;
                if (localTransform.Scale < 0.1f) {
                    ecbParallel.DestroyEntity(entityInQueryIndex, entity);
                }
            })
            .ScheduleParallel();
    }

    private void CreateBuilding()
    {
        var globalData = SystemAPI.GetSingleton<GlobalData>();

        for (int x = 0; x < buildingGridSize.x; x++) {
            for (int y = 0; y < buildingGridSize.y; y++) {
                for (int z = 0; z < buildingGridSize.z; z++) {
                    
                    var isEdge =
                        x == 0 || x == buildingGridSize.x - 1 ||
                        y == 0 || y == buildingGridSize.y - 1 ||
                        z == 0 || z == buildingGridSize.z - 1;

                    if (isEdge) {

                        var entity = EntityManager.Instantiate(globalData.prefabWall1);
                        // var position = new float3(x - (buildingGridSize.x / 2), y, z) * wallSize + buildingOffset;
                        var gridPos = new float3(x, y, z);
                        var fixToCenter = new float3(buildingGridSize.x / 2, 0, buildingGridSize.z / 2);

                        var position = (gridPos - fixToCenter) * wallSize + buildingCenter;

                        EntityManager.SetComponentData<LocalTransform>(entity, new LocalTransform()
                        {
                            Position = position,
                            Rotation = quaternion.identity,
                            Scale = wallSize,
                        });
                    }
                }
            }
        }
    }

    private void UpdatePopulation()
    {
        float3 buildingCenterCache = buildingCenter;
        float3 buildingSize = buildingGameObject.transform.localScale;

        var ecbParallel = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged)
            .AsParallelWriter();

        var zombiesReachBuildingQueue = new NativeQueue<bool>(Allocator.TempJob);
        var zombiesReachBuildingQueueParallelWriter = zombiesReachBuildingQueue.AsParallelWriter();

        Dependency = Entities
            .WithBurst()
            .ForEach((Entity entity, int entityInQueryIndex, in LocalTransform localTransform, in ZombieData zombieData) =>
            {
                var toBuildingCenter = buildingCenterCache - localTransform.Position;
                var touchBuilding = math.abs(toBuildingCenter.x) < buildingSize.x / 2f &&
                                    math.abs(toBuildingCenter.z) < buildingSize.z / 2f;

                var beyondBuildingBack = toBuildingCenter.z < -buildingSize.z / 2f;
                
                if (touchBuilding) {
                    ecbParallel.DestroyEntity(entityInQueryIndex, entity);
                    zombiesReachBuildingQueueParallelWriter.Enqueue(true);
                }
            })
            .ScheduleParallel(Dependency);

        Dependency.Complete();

        var zombiesWhoReachedBuildingCount = zombiesReachBuildingQueue.Count;
        zombiesReachBuildingQueue.Dispose();


        // Update computer-controlled zombies reaching the building, and the damage they do
        if (buildingPopulation == 0) {
            zombiesEnteredVacantBuilding++;
        }
        buildingPopulation -= zombiesWhoReachedBuildingCount * globalMonoBehaviour.populationLossPerZombie;

        var myCameraController = GameObject.FindObjectOfType<MyCameraController>();
        var isPlayerPlayingZombie = myCameraController.isAZombie;
        if (isPlayerPlayingZombie) {
            float3 cameraPosition = myCameraController.transform.position;
            var toBuildingCenter = buildingCenterCache - cameraPosition;
            var touchBuilding = math.abs(toBuildingCenter.x) < buildingSize.x / 2f &&
                                math.abs(toBuildingCenter.z) < buildingSize.z / 2f;
            if (touchBuilding) {
                if (buildingPopulation == 0) {
                    zombiesEnteredVacantBuilding++;
                }
                buildingPopulation -= globalMonoBehaviour.populationLossPerPlayerZombie;
                myCameraController.TurnIntoCamera(1);
            }
        }


        // Update population text display
        {
            var populationText = globalMonoBehaviour.populationText;

            if (buildingPopulation <= 0) {
                buildingPopulation = 0;
            }

            populationText.text = $"Population: {buildingPopulation}";
        }

        // Update zombie scanner text display
        {
            var zombieSpawnerData = SystemAPI.GetSingleton<ZombieSpawnerData>();
            var zombiesInAreaButNotSpawnedYet = zombieSpawnerData.zombiesLeftToSpawn;
            var zombieEntitiesCount = EntityManager.CreateEntityQuery(typeof(ZombieData)).CalculateEntityCount();

            var scannedZombiesCount = 0;
            scannedZombiesCount += zombiesInAreaButNotSpawnedYet;
            scannedZombiesCount += zombieEntitiesCount;
            scannedZombiesCount += zombiesEnteredVacantBuilding;
            if (isPlayerPlayingZombie) {scannedZombiesCount += 1;}
            
            var zombieScannerText = globalMonoBehaviour.zombieScannerText;
            zombieScannerText.text = $"<u>Zombie scanner</u>\n {scannedZombiesCount} in area";


            // var zombieScannerText = zo
            // globalMonoBehaviour.zombieScannerText = zombieScannerText;
        }
    }


    private void UpdateEndgame()
    {
        if (!firstFrameAlreadyRan) {
            return;
        }

        if (buildingPopulation <= 0) {
            GUITest.centerScreenText = "Skyscraper depopulated!";
        } else {
            var allZombiesCount = EntityManager.CreateEntityQuery(typeof(ZombieData)).CalculateEntityCount();
            
            // Also consider player as zombie if it's a zombie
            var myCameraController = GameObject.FindObjectOfType<MyCameraController>();
            if (myCameraController.isAZombie) {
                allZombiesCount++;
            }
            
            if (allZombiesCount <= 0) {
                GUITest.centerScreenText = "All zombies exterminated!";
            }
        }
    }
    
}
