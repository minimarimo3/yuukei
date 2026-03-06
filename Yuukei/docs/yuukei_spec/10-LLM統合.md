# 10 LLM 統合

## 1. 概要

クラウド（OpenAI 互換 API）またはローカル（Ollama）の LLM と連携し、  
台本スクリプトから `LLM` 関数として呼び出せます。

## 2. LLMBridge

### 2.1 責務

- 設定（`ConfigManager.Settings.llmSettings`）を読み取り、適切な API クライアントを選択
- API キーを `ICredentialStorage` から安全に取得（`settings.json` には保存しない）
- UniTask ベースの非同期インターフェースを提供

### 2.2 インターフェース

```csharp
public class LLMBridge : MonoBehaviour
{
    public static LLMBridge Instance { get; private set; }

    // 依存（Inspector から注入）
    [SerializeField] private ConfigManager configManager;

    /// <summary>
    /// LLM にメッセージを送信し、応答文字列を返す。
    /// provider が "none" の場合は空文字を返す。
    /// エラー時は警告ログを出して空文字を返す（例外を投げない）。
    /// </summary>
    public async UniTask<string> ChatAsync(
        string userMessage,
        string systemPrompt = "",
        CancellationToken ct = default);
}
```

## 3. プロバイダー設定

`LLMSettings.provider` の値によって動作が変わります。

| `provider` | 動作 |
|-----------|------|
| `"none"` | 即座に空文字を返す |
| `"cloud"` | OpenAI 互換 API（HTTP POST）を呼び出す |
| `"local"` | Ollama API（HTTP POST）を呼び出す |

## 4. Cloud API（OpenAI 互換）

### 4.1 リクエスト

```
POST {endpointUrl}
Authorization: Bearer {apiKey}
Content-Type: application/json

{
  "model": "{modelName}",
  "messages": [
    { "role": "system", "content": "{systemPrompt}" },
    { "role": "user",   "content": "{userMessage}" }
  ]
}
```

### 4.2 レスポンス解析

```json
{
  "choices": [
    { "message": { "content": "応答テキスト" } }
  ]
}
```

取得パス：`response.choices[0].message.content`

### 4.3 API キーの管理

- 設定画面でユーザーが入力した API キーは **`settings.json` には保存しません**。
- `ICredentialStorage.Save("llm_api_key", apiKey)` で OS の認証情報ストアに保存します。
- 呼び出し時は `ICredentialStorage.Load("llm_api_key")` で取得します。

## 5. Local API（Ollama）

### 5.1 リクエスト

```
POST {endpointUrl}/api/chat
Content-Type: application/json

{
  "model": "{modelName}",
  "messages": [
    { "role": "system", "content": "{systemPrompt}" },
    { "role": "user",   "content": "{userMessage}" }
  ],
  "stream": false
}
```

デフォルト `endpointUrl`：`http://localhost:11434`

### 5.2 レスポンス解析

```json
{
  "message": { "content": "応答テキスト" }
}
```

取得パス：`response.message.content`

## 6. エラーハンドリング

| 状況 | 挙動 |
|------|------|
| `provider == "none"` | 空文字を返す（警告なし） |
| API キーが未設定 | 空文字を返す + 警告ログ |
| ネットワークエラー | 空文字を返す + 警告ログ（例外を投げない） |
| タイムアウト | 空文字を返す + 警告ログ |
| 不正なレスポンス形式 | 空文字を返す + エラーログ |

すべてのエラーは **グレースフルデグラデーション**（機能なし状態で継続）を原則とします。
