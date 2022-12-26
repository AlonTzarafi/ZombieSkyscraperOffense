using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class MicrowaveAuthoring : MonoBehaviour
{
}

public class MicrowaveAuthoringBaker : Baker<MicrowaveAuthoring>
{
    public override void Bake(MicrowaveAuthoring authoring)
    {
        AddComponent<Microwave>(new Microwave());
    }
}


