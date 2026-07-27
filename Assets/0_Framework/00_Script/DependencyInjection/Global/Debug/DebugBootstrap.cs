using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un.Core;
using O2un.Core.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    /// <summary>
    /// 디버그 UI 전용 엔트리포인트. IDebugModule 을 순회하며 로드·주입·초기화한다.
    /// EngineRootScope 는 VContainerSettings.RootLifetimeScope 라 DontDestroyOnLoad 로 살아남고,
    /// 따라서 여기서 만든 루트도 씬 전환을 넘어 유지된다.
    /// </summary>
    public sealed class DebugBootstrap : IAsyncStartable
    {
        private const string ROOT_NAME = "[Debug UI Root]";

        private readonly IObjectResolver _resolver;
        private readonly DebugConfig _config;
        private readonly IReadOnlyList<IDebugModule> _modules;

        private Transform _root;

        [Inject]
        public DebugBootstrap(IObjectResolver resolver, DebugConfig config, IEnumerable<IDebugModule> modules)
        {
            _resolver = resolver;
            _config = config;
            _modules = modules.ToList();
        }

        public async UniTask StartAsync(CancellationToken cancellation = default)
        {
            var targets = _modules.Where(module => _config.IsModuleEnabled(module.Key)).ToList();
            if (0 == targets.Count)
            {
                return;
            }

            var rootGo = new GameObject(ROOT_NAME);
            UnityEngine.Object.DontDestroyOnLoad(rootGo);
            _root = rootGo.transform;

            foreach (IDebugModule module in targets)
            {
                await LoadModuleAsync(module, cancellation);
            }
        }

        private async UniTask LoadModuleAsync(IDebugModule module, CancellationToken ct)
        {
            try
            {
                await module.WaitUntilReadyAsync(ct);

                var prefab = await AddressablesUtils.LoadAssetAsync<GameObject>(module.Address);
                if (null == prefab)
                {
                    return;
                }

                var instance = UnityEngine.Object.Instantiate(prefab, _root);
                instance.name = module.Key;

                // SafeMono 가 Awake/Start 를 봉인했으므로 주입 뒤 Initialize 를 명시 호출해야 한다.
                // CommonLifetimeScope.InjectOnce 와 같은 순서다.
                _resolver.InjectGameObject(instance);

                var initTargets = instance.GetComponentsInChildren<ISafeInitializable>(true);
                foreach (ISafeInitializable init in initTargets)
                {
                    init.Initialize();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                Log.Print(Log.LogLevel.Error,
                    $"[DebugBootstrap] 모듈 기동 실패. key={module.Key}, error={e}");
            }
        }
    }
}
