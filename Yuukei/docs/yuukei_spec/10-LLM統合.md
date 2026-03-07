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
    // 依存（Inspector から注入）
    [SerializeField] private ConfigManager configManager;

    /// <summary>
    /// LLM にメッセージを送信し、応答文字列を返す。
    /// 会話履歴は自動的に保持・送信される。
    /// provider が "none" の場合は空文字を返す。
    /// エラー時は警告ログを出して空文字を返す（例外を投げない）。
    /// </summary>
    public async UniTask<string> ChatAsync(
        string userMessage,
        string systemPrompt = "",
        CancellationToken ct = default);

    /// <summary>会話履歴をクリアする。</summary>
    public void ClearHistory();
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
    ...{会話履歴のメッセージ},
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
    ...{会話履歴のメッセージ},
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

## 7. 会話履歴の永続化

### 7.1 概要

`LLMBridge` は会話履歴を永続的に保持します。過去のやりとりを含めて LLM に送信することで、文脈を踏まえた応答が可能になります。

### 7.2 保存先

会話履歴は `{persistentDataPath}/llm_history.json` に JSON 配列として保存します。

```json
[
  { "role": "user",      "content": "今日の天気は？" },
  { "role": "assistant", "content": "晴れですよ！" },
  { "role": "user",      "content": "ありがとう" },
  { "role": "assistant", "content": "どういたしまして！" }
]
```

### 7.3 動作仕様

- **起動時**：`llm_history.json` が存在すればロードし、履歴を復元する。
- **`ChatAsync` 呼び出し時**：ユーザーメッセージと LLM の応答を履歴に追加し、ファイルに保存する。
- **`ClearHistory()` 呼び出し時**：メモリ上の履歴をクリアし、`llm_history.json` を削除する。
- **最大件数**：履歴が 100 件を超えた場合、古いメッセージから破棄する。
- **`provider` 変更時**：履歴はクリアしない（異なるプロバイダーでも文脈を維持するため）。
