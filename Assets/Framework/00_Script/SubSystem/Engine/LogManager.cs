using Unity.Entities;
using UnityEngine;

namespace O2un.Core.Utils
{
    public partial class LogManager : EngineSubsystemBase
    {
        protected override void Init()
        {
            Log.Init();
            Log.Print(Log.LogLevel.Info, "LogManager Initialized");
        }

        public override void ClearAll()
        {
            
        }

        protected override void OnUpdate()
        {
            Log.UpdateFlush(SystemAPI.Time.DeltaTime);
        }
    }
}
