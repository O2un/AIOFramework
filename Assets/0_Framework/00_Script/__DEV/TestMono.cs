using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un;
using O2un.Core;
using O2un.Core.Network;
using O2un.Core.Utils;
using R3;
using UnityEngine;

namespace O2un.DEV 
{
#if UNITY_EDITOR
    public class TestMono : SafeMono
    {
            private readonly Subject<Unit> _moveEndSubject = new();
        [TestButton]
        public void LoadScene()
        {
        }
    }
#endif
}

