using System.Collections.Generic;
using System.Linq;
using O2un.Core;
using VContainer.Unity;

namespace O2un.DI
{
    public sealed class SafeInitRumtime : IInitializable
    {
        private readonly IReadOnlyList<ISafeInitializable> _targets;
        public SafeInitRumtime(IEnumerable<ISafeInitializable> targets)
        {
            _targets = targets.ToArray();
        }

        public void Initialize()
        {
            foreach (var target in _targets)
            {
                target.Initialize();
            }
        }
    }
}
