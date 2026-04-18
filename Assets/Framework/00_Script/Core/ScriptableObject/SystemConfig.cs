using UnityEngine;

public class SystemConfig<T> : ScriptableObject where T : SystemConfig<T>
{
    public static string EDITORCONFIGPATH
    {
        get
        {
            return $"Assets/Framework/99_DEV/SystemConfg/{typeof(T).Name}.asset";
        }
    }

    public static string GLOBALCONFIGPATH
    {
        get
        {
            return $"Assets/Resources/SystemConfig/{typeof(T).Name}.asset";
        }
    }
}
