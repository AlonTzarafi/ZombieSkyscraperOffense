using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class RenderMeAuthoring : MonoBehaviour
{
    public int MeshIndex;
}

public class RenderMeBaker : Baker<RenderMeAuthoring>
{
    public override void Bake(RenderMeAuthoring authoring)
    {
        AddSharedComponent<RenderMe>(new RenderMe { meshIndex = authoring.MeshIndex });
    }
}
