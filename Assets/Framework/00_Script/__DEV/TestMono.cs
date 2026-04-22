using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un;
using O2un.Core;
using O2un.Core.Network;
using O2un.Core.Utils;
using UnityEngine;

namespace O2un.DEV 
{
#if UNITY_EDITOR
    public class TestMono : SafeMono
    {
        [TestButton]
        public void LoadScene()
        {
            
        }
    }
#endif
}

