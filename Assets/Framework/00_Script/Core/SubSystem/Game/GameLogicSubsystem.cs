using System.Collections.Generic;
using O2un;
using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class GameUpdateSubSystem : GameSubsystemBase
{
    protected readonly List<ISafeUpdate> _updateList = new();

    public void Register(ISafeUpdate obj) => _updateList.Add(obj);
    public void Unregister(ISafeUpdate obj) => _updateList.Remove(obj);
    public override void ClearAll() => _updateList.Clear();

    protected override void OnUpdate()
    {
        for (int ii = _updateList.Count - 1; 0 <= ii; --ii)
        {
            _updateList[ii].SafeUpdate();
        }
    }

    protected override void Init()
    {
        
    }
}

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
public partial class GameFixedSubsystem : GameSubsystemBase
{
    protected readonly List<ISafeFixedUpdate> _fixedUpdateList = new();

    public void Register(ISafeFixedUpdate obj) => _fixedUpdateList.Add(obj);
    public void Unregister(ISafeFixedUpdate obj) => _fixedUpdateList.Remove(obj);
    public override void ClearAll() => _fixedUpdateList.Clear();

    protected override void OnUpdate()
    {
        for (int ii = _fixedUpdateList.Count - 1; 0 <= ii; --ii)
        {
            _fixedUpdateList[ii].SafeFixedUpdate();
        }
    }

    protected override void Init()
    {
    }
}

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class GameLateSubsystem : GameSubsystemBase
{
    protected readonly List<ISafeLateUpdate> _lateUpdateList = new();

    public void Register(ISafeLateUpdate obj) => _lateUpdateList.Add(obj);
    public void Unregister(ISafeLateUpdate obj) => _lateUpdateList.Remove(obj);
    public override void ClearAll() => _lateUpdateList.Clear();

    protected override void OnUpdate()
    {
        for (int ii = _lateUpdateList.Count - 1; 0 <= ii; --ii)
        {
            _lateUpdateList[ii].SafeLateUpdate();
        }
    }

    protected override void Init()
    {
    }
}