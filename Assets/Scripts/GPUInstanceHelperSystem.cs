using Stella3D;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public partial class GPUInstanceHelperSystem : SystemBase
{
    private Matrix4x4[] matricesArraySource;
    private SharedArray<Matrix4x4, float4x4> matricesArrayShared;
    private NativeArray<float4x4> matricesArrayNative;
    
    private Mesh[] meshList;
    private Material[] materialList;

    private bool firstFrameAlreadyRan;
    private bool disableCompletely;

    protected override void OnCreate()
    {
        matricesArraySource = new Matrix4x4[100000];
        matricesArrayShared = new SharedArray<Matrix4x4, float4x4>(matricesArraySource);
        matricesArrayNative = matricesArrayShared;
    }

    private void Init()
    {
        var gpuInstancingConfig = GameObject.FindObjectOfType<GPUInstancingData>();
        meshList = gpuInstancingConfig.meshList;
        materialList = gpuInstancingConfig.materialList;
    }
        
    protected override void OnUpdate()
    {
        if (!firstFrameAlreadyRan) {
            firstFrameAlreadyRan = true;
            Init();
        }
        
        if (Input.GetKeyDown(KeyCode.T)) {
           disableCompletely = !disableCompletely;
        }
        if (disableCompletely) {
            GUITest.instancesCounts += "Not rendering. Press T to toggle rendering back on.";
            return;
        }
        
        var dontRenderNow = Input.GetKey(KeyCode.R);
            
        var instancesCounts = "";
        
        var totalInstancesOfAllMeshTypes = 0;
        for (int meshTypeIndex = 0; meshTypeIndex < meshList.Length; meshTypeIndex++) {
            var mesh = meshList[meshTypeIndex];
            var material = materialList[meshTypeIndex];


            var meshTypeEntityCountQueue = new NativeQueue<bool>(Allocator.TempJob);
            var meshTypeEntityCountQueueParallel = meshTypeEntityCountQueue.AsParallelWriter();

            var arrToCopyTo = matricesArrayNative;

            Dependency = Entities
                .WithBurst()
                // .WithoutBurst()
                .WithSharedComponentFilter(new RenderMe { meshIndex = meshTypeIndex })
                .ForEach((in int entityInQueryIndex, in LocalToWorld localToWorld) =>
                {
                    var matrix = localToWorld.Value;
                    arrToCopyTo[entityInQueryIndex] = matrix;
                    meshTypeEntityCountQueueParallel.Enqueue(true);
                })
                .WithNativeDisableParallelForRestriction(arrToCopyTo)
                .WithNativeDisableParallelForRestriction(meshTypeEntityCountQueueParallel)
                .ScheduleParallel(Dependency);

            Dependency.Complete();

            var totalQueuedCount = meshTypeEntityCountQueue.Count;
            meshTypeEntityCountQueue.Dispose();
            
            var totalInstances = totalQueuedCount;
            totalInstancesOfAllMeshTypes += totalInstances;

            if (dontRenderNow) {
                instancesCounts += $"Not rendering {mesh.name}: {totalInstances} instances\n";
            } else {
                instancesCounts += $"Rendering {mesh.name}: {totalInstances} instances\n";
                Graphics.DrawMeshInstanced(mesh, 0, material, matricesArraySource, totalInstances);
            }
        }
        instancesCounts += $"Total instances: {totalInstancesOfAllMeshTypes}";
        
        GUITest.instancesCounts += instancesCounts;
    }
}
