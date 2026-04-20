using Unity.Entities;

namespace O2un
{
    public abstract partial class SubSystemBase : SystemBase
    {
        protected override void OnCreate() => Init();
        protected abstract void Init();
        public abstract void ClearAll();
        protected T GetSubsystem<T>() where T : SystemBase => SystemProvider.GetSubsystem<T>();
    }
    
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public abstract partial class EngineSubsystemBase : SubSystemBase { }
    
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public abstract partial class GameSubsystemBase : SubSystemBase { }

    public abstract partial class ServiceSubsystemBase : SubSystemBase
    {
        protected override void OnCreate()
        {
            base.OnCreate();
            Enabled = false;
        }
        protected sealed override void OnUpdate() { }
    }

    // 3. Edit Subsystem (에디터 모드 혹은 디버그 시에만 실행)
    #if UNITY_EDITOR
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public abstract partial class EditSubsystemBase : SubSystemBase { }
    #endif
}
