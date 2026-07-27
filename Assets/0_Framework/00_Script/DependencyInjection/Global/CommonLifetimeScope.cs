using System.Collections.Generic;
using System.Linq;
using O2un.Core;
using O2un.MVVM;
using O2un.Roslyn.Analyzer;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace O2un.DI
{
    public abstract class CommonLifetimeScope : LifetimeScope
    {
        [Header("Auto Inject Roots")]
        [SerializeField] private Transform[] _uiRoots;
        [SerializeField] private Transform[] _batchedRoots;

        private readonly HashSet<GameObject> _injectedObjects = new();
        private readonly List<ISafeInitializable> _initTargets = new();
        protected abstract void ConfigureScene(IContainerBuilder builder);

        [MustCallBase]
        protected override void Configure(IContainerBuilder builder)
        {
            // Configure CommonScene
            ConfigureScene(builder);

            builder.RegisterEntryPoint<SafeInitRumtime>();
            RegisterAutoInject(builder);
        }

        private void RegisterAutoInject(IContainerBuilder builder)
        {
            builder.RegisterBuildCallback(resolver =>
            {
                InjectContexts(resolver);
                InjectTargets(resolver);

                foreach (var init in _initTargets)
                {
                    init.Initialize();
                }
            });
        }
        
        private void InjectContexts(IObjectResolver resolver)
        {
            if(null == _uiRoots)
            {
                return;
            }

            foreach(var uiRoot in _uiRoots)
            {
                var contextList = uiRoot.GetComponentsInChildren<SafeMono>(true).OfType<IContextBase>();
                foreach (var context in contextList)
                {
                    var mono = (SafeMono)context;
                    InjectOnce(resolver, mono);
                }
            }
        }

        private void InjectTargets(IObjectResolver resolver)
        {
            foreach (var batchRoot in _batchedRoots)
            {
                var contextList = batchRoot.GetComponentsInChildren<SafeMono>(true);
                foreach (var target in contextList)
                {
                    InjectOnce(resolver, target);
                }
            }
        }

        private void InjectOnce(IObjectResolver resolver, SafeMono target)
        {
            if (target == null)
                return;

            if (!_injectedObjects.Add(target.gameObject))
                return;

            resolver.InjectGameObject(target.gameObject);
            _initTargets.AddRange(target.GetComponents<ISafeInitializable>());
        }
    }
}
