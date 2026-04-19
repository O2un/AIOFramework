using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using O2un;
using O2un.Core.Network;
using O2un.Core.Utils;
using UnityEngine;

namespace O2un.DEV 
{
#if UNITY_EDITOR
    public class TestMono : SafeMono
    {
        [Serializable]
        public struct DummyPost
        {
            public int userId;
            public int id;
            public string title;
            public string body;
        }
        
        [TestButton]
        public async UniTaskVoid TestHTTP()
        {
            Log.Dev("HTTP GET 테스트 시작...", Log.LogLevel.Info);
            string getUrl = "https://jsonplaceholder.typicode.com/posts/1";
            var getResult = await HttpService.GetAsync<DummyPost>(getUrl);

            if (getResult.id != 0) 
            {
                Log.Dev($"GET 성공! 제목: {getResult.title}", Log.LogLevel.Info);
            }
            else
            {
                Log.Dev("GET 실패 (데이터가 비어있음)", Log.LogLevel.Error);
            }
            
            Log.Dev("HTTP POST 테스트 시작...", Log.LogLevel.Info);
            string postUrl = "https://jsonplaceholder.typicode.com/posts";
            var newPost = new DummyPost 
            { 
                userId = 999, 
                title = "오투언 테스트", 
                body = "Zero-GC 네트워크 테스트 중입니다." 
            };
            var postResult = await HttpService.PostAsync<DummyPost, DummyPost>(postUrl, newPost);
            if (postResult.id > 0)
            {
                Log.Dev($"POST 성공! 서버가 발급한 ID: {postResult.id} / 전송한 제목: {postResult.title}", Log.LogLevel.Info);
            }
            else
            {
                Log.Dev("POST 실패", Log.LogLevel.Error);
            }
        }
    }
#endif
}

