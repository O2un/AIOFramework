using System;
using Unity.Entities;

public static class SystemProvider
{
    private static World DefaultWorld => World.DefaultGameObjectInjectionWorld;
    public static EntityManager DefaultEntityManager => DefaultWorld.EntityManager;

    public static T GetSubsystem<T>() where T : SystemBase
    {
        if(null == DefaultWorld || false == DefaultWorld.IsCreated)
        {
            UnityEngine.Debug.LogError("SystemProvider: Default World가 존재하지 않습니다.");
            return null;
        }

        return DefaultWorld.GetExistingSystemManaged<T>();
    }

    public static SystemBase GetSubsystem(Type systemType)
    {
        if (null == DefaultWorld || false == DefaultWorld.IsCreated)
        {
            UnityEngine.Debug.LogError("SystemProvider: Default World가 존재하지 않습니다.");
            return null;
        }
        
        return DefaultWorld.GetExistingSystemManaged(systemType) as SystemBase;
    }
}