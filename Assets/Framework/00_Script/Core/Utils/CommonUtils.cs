#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace O2un.Utils 
{
    public static class CommonUtils
    {
        public static void Quit()
        {
            #if UNITY_EDITOR
            // 에디터일 경우 플레이 모드 중지
            EditorApplication.isPlaying = false;
            #else
            // 스탠드얼론(빌드)일 경우 애플리케이션 종료
            Application.Quit();
            #endif
        }
    }
}
