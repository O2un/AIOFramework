using System;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using O2un.Core.Utils;
using UnityEngine;
using UnityEngine.Networking;

namespace O2un.Core.Network
{
    public static class HttpService
    {
        private const int DEFAULT_TIMEOUT = 5;
        
        public static async UniTask<TResponse> GetAsync<TResponse>(string url, int timeout = DEFAULT_TIMEOUT)
        {
            using var uwr = UnityWebRequest.Get(url);
            uwr.timeout = timeout;

            return await SendRequestAsync<TResponse>(uwr);
        }
        
        public static async UniTask<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest requestData, int timeout = DEFAULT_TIMEOUT)
        {
            string json = JsonUtility.ToJson(requestData);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using var uwr = new UnityWebRequest(url, "POST");
            uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.timeout = timeout;

            return await SendRequestAsync<TResponse>(uwr);
        }
        
        private static async Task<TResponse> SendRequestAsync<TResponse>(UnityWebRequest uwr)
        {
            try
            {
                var operation = uwr.SendWebRequest();
                await operation;
                if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
                {
                    Log.Dev($"HTTP Request Error: {uwr.url} | {uwr.error}", Log.LogLevel.Error);
                    return default;
                }

                string responseText = uwr.downloadHandler.text;
                Log.Dev($"HTTP Response [{uwr.responseCode}]: {responseText}", Log.LogLevel.Trace);
                return JsonUtility.FromJson<TResponse>(responseText);
            }
            catch (Exception ex)
            {
                Log.Dev($"HTTP Exception: {uwr.url} | {ex.Message}", Log.LogLevel.Fatal);
                return default;
            }
        }
    }
}
