using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Dreamwalker.Models;

namespace Dreamwalker.Networking
{
    /// <summary>
    /// HTTP client for Scope backend REST API
    /// </summary>
    public class ScopeApiClient : MonoBehaviour
    {
        private string baseUrl;

        /// <summary>
        /// Constructor for non-MonoBehaviour usage
        /// </summary>
        public ScopeApiClient() { }

        /// <summary>
        /// Constructor with server URL
        /// </summary>
        public ScopeApiClient(string serverUrl)
        {
            SetServerUrl(serverUrl);
        }

        /// <summary>
        /// Set the server URL (alias for SetBaseUrl)
        /// </summary>
        public void SetServerUrl(string serverUrl)
        {
            // Normalize URL - ensure http:// prefix and no trailing slash
            if (string.IsNullOrEmpty(serverUrl)) return;

            if (!serverUrl.StartsWith("http://") && !serverUrl.StartsWith("https://"))
            {
                serverUrl = "http://" + serverUrl;
            }
            baseUrl = serverUrl.TrimEnd('/');
            Debug.Log($"[ScopeApiClient] Base URL set to: {baseUrl}");
        }

        /// <summary>
        /// Set the base URL for API calls
        /// </summary>
        public void SetBaseUrl(string url)
        {
            SetServerUrl(url);
        }

        public string BaseUrl => baseUrl;

        // ========== Health Check ==========

        public IEnumerator CheckHealth(Action<bool, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/health"))
            {
                request.timeout = 5;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, "Connected");
                }
                else
                {
                    callback?.Invoke(false, request.error);
                }
            }
        }

        // ========== WebRTC Endpoints ==========

        public IEnumerator GetIceServers(Action<IceServersResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/webrtc/ice-servers"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<IceServersResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator SendOffer(WebRTCOfferRequest offer, Action<WebRTCOfferResponse, string> callback)
        {
            string json = JsonUtility.ToJson(offer);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/webrtc/offer", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<WebRTCOfferResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    Debug.LogError($"[ScopeApiClient] SendOffer failed: {request.error}\n{request.downloadHandler.text}");
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator SendIceCandidates(string sessionId, IceCandidatesRequest candidates, Action<bool, string> callback)
        {
            string json = JsonUtility.ToJson(candidates);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/webrtc/offer/{sessionId}", "PATCH"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 10;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success || request.responseCode == 204)
                {
                    callback?.Invoke(true, null);
                }
                else
                {
                    callback?.Invoke(false, request.error);
                }
            }
        }

        // ========== Pipeline Endpoints ==========

        public IEnumerator LoadPipeline(PipelineLoadRequest loadRequest, Action<bool, string> callback)
        {
            string json = JsonUtility.ToJson(loadRequest);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/pipeline/load", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, null);
                }
                else
                {
                    callback?.Invoke(false, request.error);
                }
            }
        }

        public IEnumerator GetPipelineStatus(Action<PipelineStatusResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/pipeline/status"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<PipelineStatusResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator GetPipelineSchemas(Action<PipelineSchemasResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/pipelines/schemas"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<PipelineSchemasResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        // ========== Hardware Info ==========

        public IEnumerator GetHardwareInfo(Action<HardwareInfoResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/hardware/info"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<HardwareInfoResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        // ========== Model Management ==========

        public IEnumerator GetModelStatus(string pipelineId, Action<ModelStatusResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/models/status?pipeline_id={pipelineId}"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<ModelStatusResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }

        public IEnumerator StartModelDownload(string pipelineId, Action<bool, string> callback)
        {
            var downloadRequest = new ModelDownloadRequest { pipeline_id = pipelineId };
            string json = JsonUtility.ToJson(downloadRequest);
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

            using (var request = new UnityWebRequest($"{baseUrl}/api/v1/models/download", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 30;

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    callback?.Invoke(true, null);
                }
                else
                {
                    callback?.Invoke(false, request.error);
                }
            }
        }

        // ========== LoRA ==========

        public IEnumerator GetLoRAList(Action<LoRAListResponse, string> callback)
        {
            using (var request = UnityWebRequest.Get($"{baseUrl}/api/v1/lora/list"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<LoRAListResponse>(request.downloadHandler.text);
                    callback?.Invoke(response, null);
                }
                else
                {
                    callback?.Invoke(null, request.error);
                }
            }
        }
    }
}
