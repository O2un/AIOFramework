using UnityEngine;

namespace O2un.Core.Utils
{
    public partial class LogManager : EngineSubsystemBase
    {
        protected override void Init()
        {
            Debug.Log("[LogManager] SubSystem Initialized.");
        }

        public override void ClearAll()
        {
            
        }

        protected override void OnUpdate()
        {
        }

        public void LogToEditor(Log.LogLevel type, string str)
        {
            switch (type)
            {
                case Log.LogLevel.Trace:
                case Log.LogLevel.Debug:
                case Log.LogLevel.Info:
                    Debug.Log(str);
                    break;
                case Log.LogLevel.Warning: 
                    Debug.LogWarning(str); 
                    break;
                case Log.LogLevel.Error: 
                case Log.LogLevel.Fatal:
                    Debug.LogError(str);
                    break;
            }
        }

        public void LogToFile(Log.LogLevel type, string str)
        {
            
        }
    }
}
