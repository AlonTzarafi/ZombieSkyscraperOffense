using System.Collections;
using System.Collections.Generic;
using Unity.Entities;

public partial struct GlobalData : IComponentData
{
    public Entity prefabWall1;
    public Entity prefabProjectile1;
    public Entity prefabProjectile2;
    public Entity prefabExplosion;
}
