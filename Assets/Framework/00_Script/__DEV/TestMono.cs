using System.Threading;
using Cysharp.Threading.Tasks;
using O2un;
using O2un.Core.Utils;
using UnityEngine;

namespace O2un.DEV 
{
#if UNITY_EDITOR
    public class TestMono : SafeMono, ISafeUpdate
    {
        private float _time = 0;
        public void SafeUpdate()
        {
            _time += Time.deltaTime;
            if (_time > 1)
            {
                Log.Dev("test");
                _time = 0;
            }
        }
    }
#endif
}

