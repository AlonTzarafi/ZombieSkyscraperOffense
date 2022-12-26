using Unity.Entities;
using UnityEngine;

public class GlobalDataAuthoring : MonoBehaviour
{
    public GameObject prefabWall1;
    public GameObject prefabProjectile1;
    public GameObject prefabProjectile2;
    public GameObject prefabExplosion;
}

public class GlobalDataAuthoringBaker : Baker<GlobalDataAuthoring>
{
    public override void Bake(GlobalDataAuthoring authoring)
    {
        AddComponent(new GlobalData
        {
            prefabWall1 = GetEntity(authoring.prefabWall1),
            prefabProjectile1 = GetEntity(authoring.prefabProjectile1),
            prefabProjectile2 = GetEntity(authoring.prefabProjectile2),
            prefabExplosion = GetEntity(authoring.prefabExplosion),
        });
    }
}
