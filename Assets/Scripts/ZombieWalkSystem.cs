using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public partial class ZombieWalkSystem : SystemBase
{
    private int chunkToProcessIndex;

    private EntityQuery allZombies;
    private EntityQuery queriedZombies;

    protected override void OnUpdate()
    {
        chunkToProcessIndex++;
        if (chunkToProcessIndex >= LeChunkification.maxValue) {
            chunkToProcessIndex = 0;
        }

        var allZombiesCount = allZombies.CalculateEntityCount();
        GUITest.instancesCounts += $"Zombie flock iterations: {GUITest.Prettify(allZombiesCount * (int)math.floor(0.1f * allZombiesCount))}\n\n";


        // GameObject crap:
        var myCharacterPosition = new float3(0, 0, 0);
        var myCameraController = UnityEngine.GameObject.FindObjectOfType<MyCameraController>();
        if (myCameraController != null) {
            myCharacterPosition = myCameraController.transform.position;
        }

        float deltaTime = SystemAPI.Time.DeltaTime;
        
        Entities.ForEach((ref LocalTransform localTransform, in ZombieData zombieData) =>
        {
            localTransform.Position += zombieData.velocity * deltaTime;
        }).ScheduleParallel();

        var chunkFilter = new LeChunkification { chonkyChunker = chunkToProcessIndex };
        
        queriedZombies = GetEntityQuery(typeof(LocalTransform), typeof(ZombieData), typeof(LeChunkification));
        queriedZombies.SetSharedComponentFilter<LeChunkification>(chunkFilter);

        var queriedZombiesCount = queriedZombies.CalculateEntityCount();

        var queriedZombiesPositions = new NativeArray<float3>(queriedZombiesCount+1, Allocator.TempJob);

        queriedZombiesPositions[queriedZombiesPositions.Length - 1] = myCharacterPosition;

        var jobHandle = Entities
            .WithBurst()
            // .WithoutBurst()
            .WithSharedComponentFilter<LeChunkification>(chunkFilter)
            .ForEach((int entityInQueryIndex, ref LocalTransform localTransform, in ZombieData zombieData) =>
            {
                queriedZombiesPositions[entityInQueryIndex] = localTransform.Position;
            }).ScheduleParallel(Dependency);

        var timeForCos = (float)(SystemAPI.Time.ElapsedTime % math.PI * 2);

        jobHandle = Entities
            .WithBurst()
            // .WithoutBurst()
            .WithStoreEntityQueryInField(ref allZombies)
            .ForEach((ref LocalTransform localTransform, ref ZombieData zombieData) =>
            {
                var preferredDistanceSq = zombieData.preferredDistance * zombieData.preferredDistance;
                var flockRadiusSq = zombieData.flockRadius * zombieData.flockRadius;
                float3 position = localTransform.Position;
                position.y = 0;
                for (int i = 0; i < queriedZombiesCount; i++) {
                    float3 otherPosition = queriedZombiesPositions[i];
                    otherPosition.y = 0;

                    var toOther = otherPosition - position;
                    var distToOtherSq = math.lengthsq(toOther);
                    var flockStrength = 1f - (distToOtherSq / flockRadiusSq);
                    var preferredDistanceStrength = 1f - (distToOtherSq / preferredDistanceSq);

                    if (distToOtherSq < flockRadiusSq) {
                        var distanceSq = distToOtherSq;
                        if (toOther.z > 0) {
                            zombieData.velocity.x += (toOther.x / distanceSq) * 1f * deltaTime;
                        }
                        if (distanceSq < preferredDistanceSq) {
                            // localTransform.Position -= toOther * 0.04f * flockStrength * deltaTime;
                            zombieData.velocity -= toOther * 0.004f * flockStrength * deltaTime;
                        } else {
                            // localTransform.Position += toOther * 0.04f * flockStrength * deltaTime;
                            zombieData.velocity += toOther * 0.002f * flockStrength * deltaTime;
                        }
                        
                    }

                    if (distToOtherSq < preferredDistanceSq) {
                        position.x += (position.x - otherPosition.x) * 0.12f * preferredDistanceStrength;
                        position.z += (position.z - otherPosition.z) * 0.12f * preferredDistanceStrength;
                    }
                }

                // Get closer to building center in the X axis
                zombieData.velocity.x += -position.x * 0.0005f * deltaTime;
                
                var maxXSpeed = 4f;
                zombieData.velocity.x = math.clamp(zombieData.velocity.x, -maxXSpeed, maxXSpeed);

                var bob = math.sin(SystemAPI.Time.ElapsedTime * 2f) * 0.1f;
                position.y = math.cos(timeForCos * zombieData.bobPhaseScale) * 0.14f;
                localTransform.Position = position;
                localTransform.Rotation = quaternion.Euler(0, zombieData.velocity.x * 0.06f, 0);
            })
            .WithNativeDisableParallelForRestriction(queriedZombiesPositions)
            .WithDisposeOnCompletion(queriedZombiesPositions)
            .ScheduleParallel(jobHandle);

        Dependency = jobHandle;

        if (SystemSettings.completeSystemOnUpdateImmediately) {
            Dependency.Complete();
        }
    }
}
