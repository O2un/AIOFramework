using Unity.Entities;
using UnityEngine;

namespace O2un.Core.Utils
{
    public partial class LogManager : ServiceSubsystemBase
    {
        protected override void Init()
        {
            Log.Init();
            Log.Print(Log.LogLevel.Info, "LogManager Initialized");
        }

        public override void ClearAll()
        {
            
        }
    }
}
