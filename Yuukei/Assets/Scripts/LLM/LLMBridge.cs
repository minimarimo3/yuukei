// ==========================================================================
// LLMBridge.cs
// クラウドAPI / ローカルLLM の差異を吸収する統一インターフェースを提供する（§10）。
// APIキーの保存・取得は ICredentialStorage に委譲する。
// ==========================================================================

using System;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// クラウドAPI / ローカルLLM の差異を吸収する統一インターフェースを提供するコンポーネント（§10）。
/// APIキーの永続化は ICredentialStorage 経由のみ行う（§17.2参照）。
/// </summary>
public class LLMBridge : MonoBehaviour
{
    public static LLMBridge Instance { get; private set; }

    // ICredentialStorage は PlatformServiceFactory.CreateCredentialStorage() で生成する
    private ICredentialStorage _credentialStorage;

    private const string ApiKeyStorageKey = "Yuukei/LLMApiKey";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _credentialStorage = PlatformServiceFactory.CreateCredentialStorage();
    }

    /// <summary>
    /// テキストを送信してLLMの返答を受け取る。
    /// LLMSettings.provider == "none" の場合は "" を返す（例外を投げない）。
    /// LLM通信失敗時は Debug.LogWarning を出して "" を返す（§17.1参照）。
    /// </summary>
    public async UniTask<string> ChatAsync(string userMessage, string systemPrompt = "")
    {
        if (ConfigManager.Instance == null || ConfigManager.Instance.Settings == null)
        {
            Debug.LogWarning("[LLMBridge] ConfigManager が初期化されていません。");
            return "";
        }

        var settings = ConfigManager.Instance.Settings.llmSettings;

        switch (settings.provider)
        {
            case "cloud":
                return await ChatWithCloudAsync(userMessage, systemPrompt, settings);
            case "local":
                return await ChatWithLocalAsync(userMessage, systemPrompt, settings);
            case "none":
            default:
                return "";
        }
    }

    /// <summary>APIキーを ICredentialStorage に保存する。</summary>
    public void SaveApiKey(string apiKey)
        => _credentialStorage.Save(ApiKeyStorageKey, apiKey);

    /// <summary>APIキーを ICredentialStorage から取得する。</summary>
    public string LoadApiKey()
        => _credentialStorage.Load(ApiKeyStorageKey);

    // ── クラウドAPI (OpenAI Chat Completions 形式) ───────────────────────

    private async UniTask<string> ChatWithCloudAsync(
        string userMessage, string systemPrompt, LLMSettings settings)
    {
        if (string.IsNullOrEmpty(settings.endpointUrl))
        {
            Debug.LogWarning("[LLMBridge] クラウドAPIのエンドポイントURLが設定されていません。");
            return "";
        }

        var apiKey = LoadApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[LLMBridge] APIキーが設定されていません。");
            return "";
        }

        try
        {
            var messages = BuildMessages(userMessage, systemPrompt);
            var requestBody = new CloudChatRequest
            {
                model = settings.modelName,
                messages = messages
            };
            var json = JsonUtility.ToJson(requestBody);

            using var req = new UnityWebRequest(settings.endpointUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + apiKey);

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LLMBridge] クラウドAPIリクエスト失敗 ({req.responseCode}): {req.error} - URL: {settings.endpointUrl}");
                return "";
            }

            return ParseCloudResponse(req.downloadHandler.text);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLMBridge] クラウドAPI通信エラー: {e.Message}");
            return "";
        }
    }

    // ── ローカルLLM (Ollama 形式) ─────────────────────────────────────────

    private async UniTask<string> ChatWithLocalAsync(
        string userMessage, string systemPrompt, LLMSettings settings)
    {
        var baseUrl = string.IsNullOrEmpty(settings.endpointUrl)
            ? "http://localhost:11434"
            : settings.endpointUrl;
        var url = baseUrl.TrimEnd('/') + "/api/chat";

        try
        {
            var messages = BuildMessages(userMessage, systemPrompt);
            var requestBody = new OllamaChatRequest
            {
                model = settings.modelName,
                messages = messages,
                stream = false
            };
            var json = JsonUtility.ToJson(requestBody);

            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            await req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[LLMBridge] ローカルLLMリクエスト失敗: {req.error}");
                return "";
            }

            return ParseOllamaResponse(req.downloadHandler.text);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLMBridge] ローカルLLM通信エラー: {e.Message}");
            return "";
        }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    private ChatMessage[] BuildMessages(string userMessage, string systemPrompt)
    {
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            return new[]
            {
                new ChatMessage { role = "system", content = systemPrompt },
                new ChatMessage { role = "user", content = userMessage }
            };
        }
        return new[] { new ChatMessage { role = "user", content = userMessage } };
    }

    private string ParseCloudResponse(string json)
    {
        try
        {
            var response = JsonUtility.FromJson<CloudChatResponse>(json);
            if (response?.choices != null && response.choices.Length > 0)
                return response.choices[0].message?.content ?? "";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLMBridge] クラウドAPIレスポンスのパース失敗: {e.Message}");
        }
        return "";
    }

    private string ParseOllamaResponse(string json)
    {
        try
        {
            var response = JsonUtility.FromJson<OllamaChatResponse>(json);
            return response?.message?.content ?? "";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[LLMBridge] Ollamaレスポンスのパース失敗: {e.Message}");
        }
        return "";
    }

    // ── JSON シリアライズ用クラス ────────────────────────────────────────

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class CloudChatRequest
    {
        public string model;
        public ChatMessage[] messages;
    }

    [Serializable]
    private class CloudChatResponse
    {
        public CloudChoice[] choices;
    }

    [Serializable]
    private class CloudChoice
    {
        public ChatMessage message;
    }

    [Serializable]
    private class OllamaChatRequest
    {
        public string model;
        public ChatMessage[] messages;
        public bool stream;
    }

    [Serializable]
    private class OllamaChatResponse
    {
        public ChatMessage message;
    }
}
