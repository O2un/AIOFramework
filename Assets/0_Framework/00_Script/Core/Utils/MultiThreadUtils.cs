using System;
using Cysharp.Threading.Tasks;

namespace O2un.Utils
{
    public static class MultiThreadUtils
    {
        public static void RunOnMainThread(this Action action, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (action == null) return;
            UniTask.Post(action, timing);
        }
    
        public static void RunOnMainThread<T>(this Action<T> action, T arg, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (action == null) return;
            UniTask.Post(() => action(arg), timing);
        }
    
        public static void RunOnMainThread<T1, T2>(this Action<T1, T2> action, T1 arg1, T2 arg2, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            if (action == null) return;
            UniTask.Post(() => action(arg1, arg2), timing);
        }
    }
}
