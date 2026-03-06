# Yuukei — アプリケーション実装仕様書 v0.2

> **本書の読み方（AIエージェント向け）**
> 本書はコードを実装するAIエージェントが「何を・どの順で・どこまで」実装すべきかを迷いなく判断できるよう記述されている。
> - 各セクションは **責務（何をするか）→ API定義 → 制約・エラー処理** の順で記述する。
> - `実装メモ:` ブロックはUnity固有の注意事項を示す。
> - `禁止:` ブロックは明示的にやってはならないことを示す。
> - 未確定事項は `TODO:` でマークし、現時点では実装しない。

---

## 目次

1. [プロジェクト構成と実装順序](#1-プロジェクト構成と実装順序)
2. [データ構造定義](#2-データ構造定義)
3. [プラットフォーム抽象化レイヤー](#3-プラットフォーム抽象化レイヤー)
4. [ConfigManager](#4-configmanager)
5. [ThemeManager](#5-thememanager)
6. [PackageManager](#6-packagemanager)
7. [PluginLoader](#7-pluginloader)
8. [TriggerManager](#8-triggermanager)
9. [DaihonSystem](#9-daihon-system)
10. [LLMBridge](#10-llmbridge)
11. [CharacterManager](#11-charactermanager)
12. [SpeechBubbleSystem](#12-speechbubblesystem)
13. [SettingsUI](#13-settingsui)
14. [OS統合](#14-os統合)
15. [ファイルレイアウト](#15-ファイルレイアウト)
16. [起動シーケンス](#16-起動シーケンス)
17. [エラー処理規則](#17-エラー処理規則)

---

## 1. プロジェクト構成と実装順序

### 1.1 アセンブリ構成

```
Yuukei.Unity             # Unity プロジェクト本体（MonoBehaviour群）
Yuukei.Plugin.Contracts  # プラグイン開発者に配布するインターフェース定義DLL（PC専用）
Daihon.dll               # 台本DSLパーサー＋インタープリタ（既実装済み、参照のみ）
```

### 1.2 プラットフォーム定義

本仕様で使用するコンパイルシンボルは以下の通り。

| シンボル | 対象 |
|---|---|
| `UNITY_STANDALONE` | Windows / macOS / Linux ビルド |
| `UNITY_STANDALONE_WIN` | Windows ビルドのみ |
| `UNITY_EDITOR` | Unityエディタ上での実行 |

### 1.3 実装優先順序

以下の順に実装すること。依存関係の逆順に実装しても動作しない。

```
1.  データ構造定義（§2）
2.  プラットフォーム抽象化レイヤー（§3）  ← §10・§14の前提
3.  ConfigManager（§4）                   ← 全コンポーネントの基盤
4.  ThemeManager（§5）                    ← UIの前提
5.  CharacterManager（§11）               ← 表示の中核
6.  SpeechBubbleSystem（§12）             ← 台本実行の前提
7.  DaihonSystem（§9）                    ← トリガーの前提
8.  TriggerManager（§8）                  ← パッケージの前提
9.  PackageManager（§6）
10. PluginLoader（§7）                    ← UNITY_STANDALONE 専用
11. LLMBridge（§10）
12. SettingsUI（§13）
13. OS統合（§14）
```

---

## 2. データ構造定義

### 2.1 settings.json にマッピングされるクラス群

```csharp
[Serializable]
public class AppSettings
{
    public string currentCharacterId = "";
    public List<CharacterData> savedCharacters = new List<CharacterData>();
    public string activePackageId = "";
    public List<PackageInfo> installedPackages = new List<PackageInfo>();
    public LLMSettings llmSettings = new LLMSettings();
    // NOTE: triggerBindings は packages 内の triggers.json が管理する。
    //       AppSettings には保存しない。
}

[Serializable]
public class CharacterData
{
    public string id;        // Guid.NewGuid().ToString() で生成
    public string name;      // ファイル名（拡張子なし）をデフォルト値とする
    public string filePath;  // 絶対パス
}

[Serializable]
public class PackageInfo
{
    public string id;           // package.json の "id" フィールド
    public string name;
    public string version;
    public string installPath;  // persistentDataPath/Packages/{id}/
    public bool enabled;        // false の場合 TriggerManager はトリガーをスキップする
}

[Serializable]
public class LLMSettings
{
    public string provider = "none"; // "none" | "cloud" | "local"
    public string endpointUrl = "";  // クラウド: APIエンドポイント, ローカル: OllamaのURL
    public string modelName = "";
    // NOTE: APIキーはここには保存しない。ICredentialStorage を使う（§3参照）。
}
```

### 2.2 パッケージ内 package.json の構造

```json
{
  "id": "com.example.my-package",
  "name": "サンプルパッケージ",
  "version": "1.0.0",
  "author": "作者名",
  "description": "説明文",
  "minAppVersion": "0.1.0"
}
```

### 2.3 パッケージ内 triggers.json の構造

```json
{
  "triggers": [
    {
      "type": "TimeTrigger",
      "params": { "hour": 9, "minute": 0, "days": ["Mon","Tue","Wed","Thu","Fri"] },
      "script": "scripts/morning_greeting.daihon"
    },
    {
      "type": "ClickTrigger",
      "params": {},
      "script": "scripts/click_reaction.daihon"
    }
  ]
}
```

`script` フィールドのパスは **パッケージの `installPath` からの相対パス** とする。
パスの結合には必ず `Path.Combine` を使用すること（文字列連結禁止。§17参照）。

### 2.4 テーマ構造（ThemeSettings）

```csharp
// ThemeSettings.cs は既実装済み。以下は参照用。
// ThemeSettings
//   └─ DialogueWindowSettings  (backgroundImage, backgroundBorder, tailImage,
//                               textPadding, fontColorHtml, fontSize)
//   └─ ButtonSettings
//   └─ ProgressBarSettings
//   └─ InputFieldSettings
//   └─ PopupSettingsCollection
//        └─ PopupThemeSettings × 5 (general/info/warning/error/glitch)
//             └─ CloseButtonSettings
```

---

## 3. プラットフォーム抽象化レイヤー

OS固有の処理は本章で定義するインターフェースで抽象化する。
Unity本体（MonoBehaviour群）は具体実装クラスに直接依存しない。

### 3.1 ICredentialStorage — 認証情報の安全な保存

APIキーなどの機密情報を OS のキーチェーン/資格情報マネージャーに保存する。

```csharp
/// <summary>
/// OSのセキュアストアへのアクセスを抽象化するインターフェース。
/// 実装クラスは CredentialStorageFactory で生成する（§3.3参照）。
/// </summary>
public interface ICredentialStorage
{
    /// <summary>指定キーで文字列を保存する</summary>
    void Save(string key, string value);

    /// <summary>指定キーの文字列を取得する。存在しない場合は null を返す</summary>
    string Load(string key);

    /// <summary>指定キーのエントリを削除する。存在しない場合は何もしない</summary>
    void Delete(string key);
}
```

#### WindowsCredentialStorage（`UNITY_STANDALONE_WIN`）

```csharp
#if UNITY_STANDALONE_WIN
/// <summary>Windows Credential Manager (CRED_TYPE_GENERIC) を使用する実装</summary>
public class WindowsCredentialStorage : ICredentialStorage
{
    // P/Invoke: advapi32.dll の CredWriteW / CredReadW / CredDeleteW を使用する。
    // targetName は呼び出し元が渡した key をそのまま使う。
    // credentialType: CRED_TYPE_GENERIC (1)
    // CredReadW で取得した IntPtr は CredFree で解放すること。
}
#endif
```

#### DummyCredentialStorage（その他OS）

```csharp
/// <summary>
/// 非Windows環境向けのフォールバック実装。
/// APIキーをメモリ上の Dictionary にのみ保持し、永続化しない。
/// TODO: macOS は Keychain、Android は Keystore を使った実装に置き換える。
/// </summary>
public class DummyCredentialStorage : ICredentialStorage { ... }
```

### 3.2 IAppIntegration — OS固有のアプリ統合機能

システムトレイ・タスクバー制御・マルチモニター対応などのOS統合機能を抽象化する。

```csharp
public interface IAppIntegration : IDisposable
{
    /// <summary>
    /// システムトレイアイコンを初期化する。
    /// 対応しないOSでは何もしない。
    /// </summary>
    void InitializeTray(Action onSettingsRequested, Action onQuitRequested);

    /// <summary>
    /// タスクバーおよびAlt+Tabからウィンドウを非表示にする。
    /// UniWindowControllerで代替できない場合のみWin32 APIを使う（§3.4参照）。
    /// </summary>
    void HideFromTaskbar();

    /// <summary>
    /// 仮想スクリーン全体にウィンドウを広げる（マルチモニター対応）。
    /// </summary>
    void SetupMultiMonitor();

    /// <summary>
    /// メインスレッドからUpdate()内で呼ぶ。
    /// STAスレッドからのコールバックをメインスレッドで処理するためのポンプ。
    /// </summary>
    void Tick();
}
```

#### WindowsAppIntegration（`UNITY_STANDALONE_WIN`）

```csharp
#if UNITY_STANDALONE_WIN
/// <summary>Windows固有の実装。SystemTrayManager相当のロジックをここに集約する</summary>
public class WindowsAppIntegration : IAppIntegration
{
    // InitializeTray: STAスレッドでSystem.Windows.Forms.NotifyIconを起動
    // HideFromTaskbar: Win32 SetWindowLong で WS_EX_TOOLWINDOW を付与（§14.2参照）
    // SetupMultiMonitor: SM_XVIRTUALSCREEN 等で仮想スクリーンサイズを取得し SetWindowPos（§14.3参照）
    // Tick: ConcurrentQueue<Action> をドレインしてメインスレッドで実行
}
#endif
```

#### DummyAppIntegration（その他OS）

```csharp
/// <summary>非Windows環境向けの空実装。すべてのメソッドが何もしない</summary>
public class DummyAppIntegration : IAppIntegration { ... }
```

### 3.3 CredentialStorageFactory / AppIntegrationFactory

```csharp
public static class PlatformServiceFactory
{
    public static ICredentialStorage CreateCredentialStorage()
    {
#if UNITY_STANDALONE_WIN
        return new WindowsCredentialStorage();
#else
        return new DummyCredentialStorage();
#endif
    }

    public static IAppIntegration CreateAppIntegration()
    {
#if UNITY_STANDALONE_WIN
        return new WindowsAppIntegration();
#else
        return new DummyAppIntegration();
#endif
    }
}
```

### 3.4 UniWindowController との役割分担

本プロジェクトには `UniWindowController` アセットが導入済みである。
ウィンドウ制御のうち以下は UniWindowController に委譲し、Win32 APIを直接叩かない。

| 機能 | 担当 |
|---|---|
| 透過ウィンドウ（クリックスルー含む） | `UniWindowController` |
| 常時最前面表示 | `UniWindowController.isTopmost = true` |
| ファイルドロップ受付 | `UniWindowController.OnDropFiles` |
| タスクバー非表示 | `IAppIntegration.HideFromTaskbar()`（Win32直接。UniWCで対応不可のため） |
| マルチモニター展開 | `IAppIntegration.SetupMultiMonitor()`（Win32直接。UniWCで対応不可のため） |

**禁止:** UniWindowControllerで対応できる機能に対して独自のWin32 P/Invokeを実装しないこと。

---

## 4. ConfigManager

### 4.1 責務

- `settings.json` の読み書きを **唯一** 担うシングルトン。
- 他のコンポーネントは直接ファイルI/Oを行わない。必ずConfigManager経由で読み書きする。
- `DontDestroyOnLoad` で永続化する。

### 4.2 公開API

```csharp
public class ConfigManager : MonoBehaviour
{
    public static ConfigManager Instance { get; private set; }

    /// <summary>設定の初回ロード完了時に発火</summary>
    public event Action OnConfigLoaded;

    /// <summary>currentCharacterIdが変更された時に発火。引数は新しいID</summary>
    public event Action<string> OnCharacterChanged;

    public AppSettings Settings { get; private set; }

    public void SaveSettings();

    /// <summary>キャラクターを登録し即座にSaveする</summary>
    public void AddCharacter(string name, string filePath);

    /// <summary>
    /// currentCharacterIdを変更してSaveし、OnCharacterChangedを発火する。
    /// 同じIDが渡された場合は何もしない。
    /// </summary>
    public void SetCurrentCharacter(string characterId);

    /// <summary>パッケージを登録してSaveする</summary>
    public void AddPackage(PackageInfo info);

    /// <summary>パッケージを削除してSaveする</summary>
    public void RemovePackage(string packageId);
}
```

### 4.3 実装規則

- `Awake` でロードし、ロード完了後に `OnConfigLoaded` を発火する。
- `settings.json` が存在しない場合は `new AppSettings()` を使用する（エラーにしない）。
- JSONのデシリアライズ失敗時は `new AppSettings()` にフォールバックし、`Debug.LogError` を出す。
- **禁止:** `SaveSettings` を `Update` など毎フレーム呼ぶこと。変更が発生した時のみ呼ぶ。

---

## 5. ThemeManager

### 5.1 責務

- テーマZIPの展開と `theme.json` のパース。
- `OnThemeLoaded` イベントで各UIコンポーネントに通知する。
- テーマの展開先は `Path.Combine(persistentDataPath, "Themes", zipFileNameWithoutExtension)`。
- 同名ディレクトリが存在する場合は削除してから上書き展開する。

### 5.2 公開API

```csharp
public class ThemeManager : MonoBehaviour
{
    public static ThemeManager Instance { get; private set; }

    /// <summary>
    /// テーマロード完了時に発火。
    /// 引数1: パースされたThemeSettings
    /// 引数2: 画像ファイルが存在するディレクトリの絶対パス
    /// </summary>
    public event Action<ThemeSettings, string> OnThemeLoaded;

    /// <summary>ZIPファイルを展開してテーマを適用する</summary>
    public UniTask LoadThemeFromZipAsync(string zipFilePath);

    /// <summary>
    /// 既に展開済みのディレクトリからテーマを適用する。
    /// PackageManagerがパッケージ内 theme/ を適用する際に使う。
    /// </summary>
    public UniTask LoadThemeFromDirectoryAsync(string dirPath);
}
```

### 5.3 フォールバック規則

各UIコンポーネントは `OnThemeLoaded` を受け取った際に以下の順で処理する：

```
1. CleanupDynamicAssets()    // 前回の動的生成スプライトをDestroy
2. 各設定フィールドがnullまたは空文字 → プレハブのデフォルト値を使う
3. 画像パスが存在する → テクスチャを非同期ロードしてスプライト生成（§5.4参照）
4. 画像パスが存在しない → プレハブのデフォルトスプライトを使う
```

**禁止:** `_defaultBgSprite` などUnityが管理するデフォルトスプライトを `Destroy` しないこと。

### 5.4 画像の非同期ロード規則

テーマ画像のロードは **必ず非同期** で行うこと。

```csharp
// ✅ 正しい実装例
using var req = UnityWebRequestTexture.GetTexture("file://" + absolutePath);
await req.SendWebRequest();
if (req.result == UnityWebRequest.Result.Success)
{
    var tex = DownloadHandlerTexture.GetContent(req);
    // スプライト生成...
}

// ❌ 禁止: File.ReadAllBytes → new Texture2D → LoadImage はメインスレッドをブロックする
```

**禁止:** `File.ReadAllBytes` / `File.ReadAllBytesAsync` + `Texture2D.LoadImage` のパターン（メインスレッドで `LoadImage` を呼ぶとブロックが発生する）。

---

## 6. PackageManager

### 6.1 責務

- パッケージ（`.yuupkg` = ZIP）のインストール・アンインストール・一覧管理。
- インストール後に ThemeManager / TriggerManager / PluginLoader に委譲する。
- `ConfigManager.Settings.installedPackages` に永続化する。

### 6.2 パッケージファイル構造

```
{packageId}.yuupkg  (実体はZIP)
├─ package.json         # 必須
├─ theme/               # オプション
│  ├─ theme.json
│  └─ images/
├─ scripts/             # オプション
│  ├─ triggers.json
│  └─ *.daihon
└─ plugins/             # オプション（UNITY_STANDALONE 専用。他プラットフォームでは無視）
   └─ *.dll
```

### 6.3 公開API

```csharp
public class PackageManager : MonoBehaviour
{
    public static PackageManager Instance { get; private set; }

    /// <summary>
    /// パッケージをインストールする。
    /// 同じIDのパッケージが既にインストール済みの場合はアンインストールしてから再インストールする。
    /// </summary>
    public async UniTask InstallAsync(string packageFilePath);

    /// <summary>パッケージをアンインストールする</summary>
    public void Uninstall(string packageId);

    /// <summary>インストール済みパッケージ一覧を返す</summary>
    public IReadOnlyList<PackageInfo> GetInstalledPackages();
}
```

### 6.4 InstallAsync の処理順序

```
1. ZIPを Path.Combine(persistentDataPath, "Packages", id) に展開
2. package.jsonをパースしてPackageInfoを生成
3. ConfigManager.AddPackage(info) でSettings更新
4. Path.Combine(installPath, "theme") が存在する
   → ThemeManager.LoadThemeFromDirectoryAsync(path) を await
5. Path.Combine(installPath, "scripts", "triggers.json") が存在する
   → TriggerManager.RegisterPackageTriggers(packageId, installPath)
6. Path.Combine(installPath, "plugins") が存在する
   → #if UNITY_STANDALONE: PluginLoader.LoadFromDirectory(path, packageId)
   → それ以外: Debug.Log("プラグインはPC版専用のためスキップ")
```

### 6.5 Uninstall の処理順序

```
1. TriggerManager.UnregisterPackageTriggers(packageId)
2. #if UNITY_STANDALONE: PluginLoader.MarkForUnloadOnRestart(packageId)（§7.4参照）
3. Directory.Delete(installPath, recursive: true)
4. ConfigManager.RemovePackage(packageId)
```

---

## 7. PluginLoader

> **適用プラットフォーム:** `#if UNITY_STANDALONE` で囲むこと。AOT環境（Android / iOS）では
> DLLの動的ロードが不可能なため、このクラス自体をコンパイル対象から除外する。

### 7.1 責務

- `Yuukei.Plugin.Contracts.dll` で定義されたインターフェースを実装したDLLを動的ロードする。
- ロードしたプラグインを TriggerManager / DaihonFunctionRegistry に登録する。

### 7.2 Yuukei.Plugin.Contracts で定義するインターフェース

```csharp
// ── トリガー拡張 ────────────────────────────────────────────────────────
public interface ITriggerPlugin : IDisposable
{
    /// <summary>一意な識別子。triggers.jsonの "type" フィールドと一致させる</summary>
    string TriggerName { get; }

    void Initialize(ITriggerContext context);

    event Action<TriggerPayload> OnFired;
}

public interface ITriggerContext
{
    void ExecuteScript(string scriptAbsolutePath);
    IVariableStore VariableStore { get; }
}

public class TriggerPayload
{
    public string TriggerName { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
}

// ── 台本DSL関数拡張 ─────────────────────────────────────────────────────
public interface IDaihonFunction
{
    string FunctionName { get; }
    UniTask<DaihonValue> CallAsync(
        IReadOnlyList<DaihonValue> positionalArgs,
        IReadOnlyDictionary<string, DaihonValue> namedArgs);
}
```

### 7.3 公開API

```csharp
#if UNITY_STANDALONE
public class PluginLoader : MonoBehaviour
{
    /// <summary>
    /// ディレクトリ内の全DLLをスキャンしてロードする。
    /// ITriggerPlugin実装はTriggerManagerに、IDaihonFunction実装はDaihonFunctionRegistryに自動登録する。
    /// ロード失敗したDLLはDebug.LogErrorを出してスキップし、残りのDLLの処理を続行する。
    /// </summary>
    public void LoadFromDirectory(string dirPath, string packageId = null);

    /// <summary>
    /// 指定パッケージのプラグインを「次回起動時にロードしない」対象として記録する。
    /// アンロードは即座には行わない（§7.4参照）。
    /// </summary>
    public void MarkForUnloadOnRestart(string packageId);
}
#endif
```

### 7.4 プラグインのアンロード制限

**UnityのMonoバックエンドでは `AssemblyLoadContext` によるランタイムアンロードは非推奨・サポート対象外である。**

- プラグインの切り替え（追加・削除）は **次回アプリ起動時** に適用される。
- `MarkForUnloadOnRestart` はアンロード対象のパッケージIDを `persistentDataPath/pending_unload.json` に書き出すだけにとどめる。
- 次回起動時の `PluginLoader.Start()` でこのリストを読み込み、対象パッケージのDLLをロードしない。
- **禁止:** `AssemblyLoadContext.Unload()` を実装すること。GCの不確定性によりUnity本体のクラッシュを引き起こす可能性がある。

アンインストール後にUIへ表示するメッセージ:
`「プラグインの変更はアプリを再起動後に反映されます」`

### 7.5 セキュリティ警告UI

- `plugins/` を含むZIPをインストールしようとした場合、**インストール実行前に** モーダルダイアログを表示する。
- ダイアログテキスト: `「このパッケージにはプラグイン(DLL)が含まれています。信頼できる配布元からのみインストールしてください。」`
- ユーザーが「キャンセル」を選んだ場合は処理を中断する。

---

## 8. TriggerManager

### 8.1 責務

- 全トリガーの登録・監視・発火を一元管理する。
- 組み込みトリガーは `ITriggerPlugin` と同じインターフェースで実装する。
- トリガー発火時に対応する台本ファイルを解決し `DaihonRunner.RunAsync` を呼ぶ。

### 8.2 組み込みトリガー

以下を `ITriggerPlugin` として内部実装する。triggers.json の `"type"` フィールドで参照される。

| type 文字列 | 発火条件 | params フィールド |
|---|---|---|
| `TimeTrigger` | 指定時刻に一致 | `hour`(int), `minute`(int), `days`(string[]) |
| `ClickTrigger` | キャラクターへの左クリック | なし |
| `SystemEventTrigger` | CPU使用率・バッテリー残量の閾値超過 | `cpuThreshold`(float), `batteryThreshold`(float) |
| `FileDropTrigger` | ウィンドウへのファイルドロップ | `extensions`(string[]) |
| `ExplorerItemTrigger` | Explorerアイテム座標とキャラ位置の重なり | `targetName`(string) |

### 8.3 公開API

```csharp
public class TriggerManager : MonoBehaviour
{
    public static TriggerManager Instance { get; private set; }

    public void RegisterPlugin(ITriggerPlugin plugin);
    public void RegisterPackageTriggers(string packageId, string packageInstallPath);
    public void UnregisterPackageTriggers(string packageId);
    public void StartMonitoring();
    public void StopMonitoring();
}
```

### 8.4 トリガー発火時の処理と多重実行制御

```
TriggerPayload を受け取る
  ↓
_isScriptRunning == true → 即座に破棄（キューに積まない）
  ↓
対応するTriggerBinding（packageId + scriptRelativePath）を解決する
  ↓
PackageInfo.enabled == false → スキップ
  ↓
scriptPath = Path.Combine(installPath, scriptRelativePath)
File.Exists(scriptPath) == false → Debug.LogWarning してスキップ
  ↓
_isScriptRunning = true
DaihonRunner.RunAsync(scriptText, actionHandler, variableStore) を UniTask で実行
完了時: _isScriptRunning = false
```

**多重実行制御の仕様:**
- `_isScriptRunning` フラグはTriggerManager全体でグローバルに1つだけ持つ。
- 台本実行中に発火したトリガーは **種別を問わず** すべて破棄する。
- キューへの積み上げは行わない。
- この仕様はデザイン上の意図（「キャラクターは一度に一つのことしかしない」）であり、変更しないこと。

---

## 9. Daihon System

### 9.1 概要

Daihon.dllは既実装済み。UnityからはDaihonRunnerファサードを通じて使用する。
本セクションは「どう呼ぶか」と「IUniTaskActionHandler の実装仕様」を定義する。

### 9.2 台本DSL仕様の要点

台本DSLの完全な仕様は別途「台本（スクリプト）言語仕様書」を参照すること。
以下はIUniTaskActionHandlerの実装に必要な最低限の知識をまとめる。

#### 組み込み関数一覧

台本から呼び出されるアプリ実装必須の関数：

| 関数名 | 位置引数 | 名前付き引数 | 戻り値 | 説明 |
|---|---|---|---|---|
| `表情` | 表情名(string) | - | None | VRM表情を設定する |
| `動作` | モーション名(string) | - | None | VRMAモーションを再生/停止する |
| `待つ` | 秒数(number) | - | None | 指定秒数待機する |
| `w` | 秒数(number) | - | None | `待つ` の短縮形 |
| `ランダム` | 最小値(number), 最大値(number) | - | number | 乱数を返す |
| `現在の天気` | - | - | string | `「晴れ」`\|`「曇り」`\|`「雨」`\|`「雪」` |
| `LLM` | プロンプト(string) | - | string | LLMBridgeを呼び出し返答を返す |
| `選択肢表示` | 選択肢...(string) | - | None | 選択UIを表示する |

`動作` の引数が `「停止」` → `VrmaPlayer.StopAnimation()`
`動作` のその他引数 → `VrmaPlayer.PlayAnimation(引数)`

#### 変数ストア

TriggerManagerは **パッケージIDをスコープとした** `SimpleVariableStore` インスタンスを保持する。
同一パッケージの台本間で変数は共有される。パッケージをまたいで変数を共有しない。

### 9.3 SimpleVariableStore の動的Getter

`DateTime.Now` のようにアクセスするたびに値が変わる組み込み変数は、通常のDictionaryとは別に `Func<DaihonValue>` を格納するレジストリで管理する。

```csharp
public class SimpleVariableStore : IVariableStore
{
    // 通常変数（永続・一時）
    private readonly Dictionary<string, DaihonValue> _store = new();

    // 動的変数。GetValue 呼び出し時に毎回 Func を評価する。
    // SetValue による上書きは禁止（後述）。
    private readonly Dictionary<string, Func<DaihonValue>> _dynamicGetters = new();

    /// <summary>
    /// 動的Getterを登録する。
    /// TriggerManager.Start() で組み込み時間変数を登録する際に使用する。
    /// </summary>
    public void RegisterDynamicGetter(string name, Func<DaihonValue> getter)
    {
        _dynamicGetters[name] = getter;
    }

    public DaihonValue GetValue(string name)
    {
        // 動的Getterが登録されていれば優先して評価する
        if (_dynamicGetters.TryGetValue(name, out var getter))
            return getter();
        if (_store.TryGetValue(name, out var value))
            return value;
        throw new UndefinedVariableException(name);
    }

    public void SetValue(string name, DaihonValue value)
    {
        // 動的Getter登録済みの変数への上書きは禁止
        if (_dynamicGetters.ContainsKey(name))
            throw new InvalidOperationException($"動的変数 '{name}' には代入できません");
        _store[name] = value;
    }
}
```

#### 組み込み時間変数の登録（TriggerManager.Start内）

```csharp
store.RegisterDynamicGetter("年",   () => DaihonValue.FromNumber(DateTime.Now.Year));
store.RegisterDynamicGetter("月",   () => DaihonValue.FromNumber(DateTime.Now.Month));
store.RegisterDynamicGetter("日",   () => DaihonValue.FromNumber(DateTime.Now.Day));
store.RegisterDynamicGetter("時",   () => DaihonValue.FromNumber(DateTime.Now.Hour));
store.RegisterDynamicGetter("分",   () => DaihonValue.FromNumber(DateTime.Now.Minute));
store.RegisterDynamicGetter("秒",   () => DaihonValue.FromNumber(DateTime.Now.Second));
store.RegisterDynamicGetter("ミリ秒", () => DaihonValue.FromNumber(DateTime.Now.Millisecond));
store.RegisterDynamicGetter("曜日", () => DaihonValue.FromString(
    new[]{"日","月","火","水","木","金","土"}[(int)DateTime.Now.DayOfWeek]));
store.RegisterDynamicGetter("週",   () => DaihonValue.FromNumber(
    System.Globalization.CultureInfo.CurrentCulture.Calendar
        .GetWeekOfYear(DateTime.Now,
            System.Globalization.CalendarWeekRule.FirstDay,
            DayOfWeek.Monday)));
```

### 9.4 YuukeiActionHandler

```csharp
public class YuukeiActionHandler : MonoBehaviour, IUniTaskActionHandler
{
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private VRMLipSync lipSync;
    [SerializeField] private VrmaPlayer vrmaPlayer;
    [SerializeField] private SpeechBubbleController speechBubble;
    [SerializeField] private LLMBridge llmBridge;

    /// <summary>セリフを表示し口パクを再生する。口パク完了まで await する</summary>
    public async UniTask ShowDialogueAsync(string text);

    /// <summary>
    /// 関数名に応じてディスパッチする。
    /// 組み込み関数に該当しない場合は DaihonFunctionRegistry を参照する。
    /// 未知の関数名は Debug.LogWarning を出して DaihonValue.None を返す（例外を投げない）。
    /// </summary>
    public UniTask<DaihonValue> CallFunctionAsync(
        string functionName,
        IReadOnlyList<DaihonValue> positionalArgs,
        IReadOnlyDictionary<string, DaihonValue> namedArgs);
}
```

### 9.5 DaihonFunctionRegistry

プラグインが追加するカスタム関数を管理するシングルトン。

```csharp
public static class DaihonFunctionRegistry
{
    public static void Register(IDaihonFunction function);
    public static void Unregister(string functionName);
    public static bool TryGet(string functionName, out IDaihonFunction function);
}
```

---

## 10. LLMBridge

### 10.1 責務

- クラウドAPI / ローカルLLMの差異を吸収する統一インターフェースを提供する。
- APIキーの保存・取得は `ICredentialStorage` に委譲する（直接P/Invokeしない）。

### 10.2 公開API

```csharp
public class LLMBridge : MonoBehaviour
{
    public static LLMBridge Instance { get; private set; }

    // ICredentialStorage は PlatformServiceFactory.CreateCredentialStorage() で生成する
    private ICredentialStorage _credentialStorage;

    private const string ApiKeyStorageKey = "Yuukei/LLMApiKey";

    /// <summary>
    /// テキストを送信してLLMの返答を受け取る。
    /// LLMSettings.provider == "none" の場合は "" を返す（例外を投げない）。
    /// </summary>
    public async UniTask<string> ChatAsync(string userMessage, string systemPrompt = "");

    public void SaveApiKey(string apiKey)
        => _credentialStorage.Save(ApiKeyStorageKey, apiKey);

    public string LoadApiKey()
        => _credentialStorage.Load(ApiKeyStorageKey);
}
```

### 10.3 プロバイダー実装規則

```
LLMSettings.provider の値で分岐する：

"cloud"
  → POST {endpointUrl}
  → Headers: Authorization: Bearer {LoadApiKey()}
  → Body: OpenAI Chat Completions API 形式 (messages配列)
  → model: LLMSettings.modelName

"local"
  → POST {endpointUrl}/api/chat  (Ollama形式)
  → Body: { "model": modelName, "messages": [...] }
  → APIキーは不要

"none"
  → 即座に "" を返す
```

**禁止:** `LLMBridge` 内に P/Invoke を直接記述すること。APIキーの永続化は `ICredentialStorage` 経由のみ。

---

## 11. CharacterManager

### 11.1 責務

- VRMのロード・破棄・切り替え。
- `CurrentVrmInstance` を他コンポーネントに公開する。

### 11.2 公開API

```csharp
public class CharacterManager : MonoBehaviour
{
    [SerializeField] private Transform characterRoot;

    public Vrm10Instance CurrentVrmInstance { get; private set; }

    /// <summary>
    /// 指定IDのキャラクターをロードする。
    /// 既存キャラが存在する場合はDestroyしてから新規ロードする。
    /// </summary>
    public async UniTask LoadCharacterAsync(string characterId);
}
```

### 11.3 ロード後の処理順序

```
1. 既存 _currentVrmInstance != null → Destroy → null に設定
2. await Vrm10.LoadPathAsync(path, canLoadVrm0X: true, showMeshes: true)
3. transform.SetParent(characterRoot, false)
4. ApplyOutlineRenderingLayer() → renderingLayerMask |= 256
5. characterRoot.GetComponent<ObjectToBottomRight>()?.PositionAtBottomRight()
6. VRMLipSync の _cachedVrm を null にリセットしてキャッシュを無効化する
```

### 11.4 イベント購読

```
ConfigManager.OnConfigLoaded  → HandleConfigLoaded()
  └─ savedCharacters.Count == 0 → SettingsUIManager.ShowCharacterTab()
  └─ savedCharacters.Count > 0  → await LoadCharacterAsync(currentCharacterId)

ConfigManager.OnCharacterChanged → await LoadCharacterAsync(newId)
```

---

## 12. SpeechBubbleSystem

### 12.1 構成コンポーネント

```
TalkCanvas (Canvas, ScreenSpaceCamera)
└─ SpeechBubble (RectTransform)
   ├─ Background (Image, 9-slice)
   ├─ Tail (Image)
   └─ MessageText (TextMeshProUGUI)

SpeechBubblePositioner  // 頭部追従・クランプ・しっぽ制御
SpeechBubbleAutoResizer // テキスト量に応じた背景リサイズ
DialogueWindowUI        // テーマ適用
SpeechBubbleController  // YuukeiActionHandlerからの公開API（新設）
```

### 12.2 SpeechBubblePositioner の処理（LateUpdate）

```
1. CharacterManager.CurrentVrmInstance == null → return
2. VRMが変わっていれば Animator.GetBoneTransform(HumanBodyBones.Head) を再取得
3. headBone.position + worldOffset → WorldToScreenPoint → スクリーン座標
4. screenPoint.z < 0 → return（カメラ背面）
5. ScreenPointToLocalPointInRectangle(canvasRect) → localPoint
6. avoidCharacter == true → AvoidCharacterHorizontal(ref localPoint)
7. enableClamping == true → ClampToCanvas(ref localPoint)
8. targetBubble.localPosition = localPoint
9. しっぽ位置補正（shiftX に基づいてtailRect.localPosition.x を調整）
10. autoFlipTail == true → キャラクター方向に応じて tailRect.localScale.x を反転
```

### 12.3 SpeechBubbleController（新設）

```csharp
public class SpeechBubbleController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;

    public void SetText(string text);
    public void Show();
    public void Hide();
}
```

---

## 13. SettingsUI

### 13.1 フレームワーク

- Unity UI Toolkit（UIElements）を使用する。
- `SettingsPanel` は `PanelDragManipulator` でドラッグ移動可能にする。
- 初回表示時（`_isPositionInitialized == false`）は `GeometryChangedEvent` で画面右下に配置する。

### 13.2 タブ定義

| TabID | 表示名 | 実装優先度 | 内容 |
|---|---|---|---|
| `Tab_General` | 一般設定 | High | 言語設定, LLM設定（プロバイダー選択/APIキー/エンドポイント/モデル名）, 起動設定 |
| `Tab_Character` | キャラクターの選択 | High | VRM一覧・選択・追加（FilePanel経由） |
| `Tab_Theme` | テーマの選択 | High | インストール済みテーマ一覧・適用ボタン |
| `Tab_Package` | パッケージの管理 | High | インストール済みパッケージ一覧・有効化/無効化/アンインストール |
| `Tab_Market` | マーケットプレイス | Medium | REST APIから一覧取得・インストールボタン |
| `Tab_Script` | 台本の選択 | Medium | アクティブパッケージのトリガー・台本対応一覧（閲覧のみ） |
| `Tab_Prop` | 小物の選択 | Low | TODO |
| `Tab_Mod` | MODの管理 | Low | パッケージ非経由DLLの管理（UNITY_STANDALONE のみ表示） |
| `Tab_About` | アプリについて | Low | バージョン・ライセンス |

### 13.3 Tab_General（LLM設定）の実装規則

```
プロバイダー選択 DropdownField: ["使用しない", "クラウドAPI", "ローカルLLM"]

"使用しない" 選択時:
  → LLMSettings.provider = "none"
  → APIキー/エンドポイント/モデル名フィールドを非表示

"クラウドAPI" 選択時:
  → LLMSettings.provider = "cloud"
  → エンドポイントURL入力欄を表示
  → APIキー入力欄を表示（保存ボタン押下で LLMBridge.SaveApiKey()）
  → モデル名入力欄を表示

"ローカルLLM" 選択時:
  → LLMSettings.provider = "local"
  → サーバーURL入力欄を表示（デフォルト: "http://localhost:11434"）
  → モデル名入力欄を表示
  → APIキー入力欄は非表示
```

### 13.4 Tab_Character の実装規則

- 「+ キャラクターを追加」ボタン → `Kirurobo.FilePanel.OpenFilePanel` で `.vrm`, `.bundle` を選択
- 複数選択許可（`Flag.AllowMultipleSelection`）
- 選択後: `ConfigManager.AddCharacter(fileName, path)` を呼び、`RenderCharacterTab()` で再描画
- 各行に「選択」ボタン → `ConfigManager.SetCurrentCharacter(id)` → `RenderCharacterTab()`
- 現在選択中の行は `★ {name}` でラベルを黄色表示し、「選択」ボタンを無効化

---

## 14. OS統合

### 14.1 AppIntegrationManagerの責務

`IAppIntegration` を保持し、Unityのライフサイクルに橋渡しするMonoBehaviour。

```csharp
public class AppIntegrationManager : MonoBehaviour
{
    private IAppIntegration _integration;

    private void Awake()
    {
        // Application.isEditorのチェックはここで行う
        if (Application.isEditor)
        {
            _integration = new DummyAppIntegration();
            return;
        }
        _integration = PlatformServiceFactory.CreateAppIntegration();
    }

    private void Start()
    {
        _integration.InitializeTray(
            onSettingsRequested: () => SettingsUIManager.Instance.ShowSettings(),
            onQuitRequested:     () => Application.Quit());
        _integration.HideFromTaskbar();
        _integration.SetupMultiMonitor();
    }

    private void Update() => _integration.Tick();

    private void OnDestroy() => _integration.Dispose();
}
```

### 14.2 HideFromTaskbar の実装詳細（WindowsAppIntegration内）

```
WaitForEndOfFrame() + WaitForSeconds(0.1f) 待機  ← ウィンドウハンドル確定を待つ
FindWindow(null, Application.productName) でhWnd取得
失敗時は GetActiveWindow() でフォールバック
GetWindowLong(GWL_EXSTYLE):
  WS_EX_APPWINDOW ビットをクリア
  WS_EX_TOOLWINDOW ビットをセット
SetWindowPos(SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED)
```

実装メモ: この処理は UniWindowController では対応不可のため Win32 API を使用する（§3.4参照）。

### 14.3 SetupMultiMonitor の実装詳細（WindowsAppIntegration内）

```
SM_XVIRTUALSCREEN, SM_YVIRTUALSCREEN で仮想スクリーン原点を取得
SM_CXVIRTUALSCREEN, SM_CYVIRTUALSCREEN で全体サイズを取得
Process.GetCurrentProcess().MainWindowHandle でhWnd取得
SetWindowPos(hWnd, Zero, x, y, w, h, SWP_NOZORDER)
```

実装メモ: この処理は UniWindowController では対応不可のため Win32 API を使用する（§3.4参照）。

### 14.4 ExplorerItemScanner との通信プロトコル

- 外部プロセス: `Path.Combine(Application.streamingAssetsPath, "ExplorerItemScanner", "ExplorerItemScanner.exe")`
- 起動引数: `"targetName"`（検索対象のファイル/フォルダ名）
- 標準出力の形式:
  - 成功: `SUCCESS:{left},{top},{width},{height}`（floatのCSV）
  - 失敗/情報: それ以外の文字列 → `Debug.LogWarning`
- 終了方法: 標準入力に `"exit\n"` を書き込む → WaitForExit(1000ms) → タイムアウト時はKill
- **実装メモ:** ExplorerItemScannerは `#if UNITY_STANDALONE_WIN` で囲むこと。

### 14.5 FileDropRouter のルーティング規則

UniWindowControllerの `OnDropFiles` から呼ばれる。

```
拡張子で分岐:
  ".vrm" / ".bundle"
    → ConfigManager.AddCharacter(Path.GetFileNameWithoutExtension(path), path)
    → 即座に SetCurrentCharacter で選択状態にする

  ".zip" / ".yuupkg"
    → ZIPのルートに "package.json" が存在する
        → PluginLoader: plugins/ を含む場合はセキュリティ警告UI表示（§7.5参照）
        → await PackageManager.InstallAsync(path)
    → ZIPのルートに "theme.json" が存在する
        → await ThemeManager.LoadThemeFromZipAsync(path)
    → どちらも存在しない → Debug.LogWarning("パッケージでもテーマでもないZIPです")

  ".daihon" → TODO: 単体台本のインポート
  その他    → Debug.LogWarning("未対応のファイル形式: " + ext)
```

---

## 15. ファイルレイアウト

### 15.1 persistentDataPath 以下

```
{persistentDataPath}/
├─ settings.json
├─ pending_unload.json       # PluginLoader が管理する次回起動時アンロード対象リスト
├─ Themes/
│  └─ {zipFileNameWithoutExtension}/
│     ├─ theme.json
│     └─ images/
└─ Packages/
   └─ {packageId}/
      ├─ package.json
      ├─ theme/
      ├─ scripts/
      │  ├─ triggers.json
      │  └─ *.daihon
      └─ plugins/            # UNITY_STANDALONE のみロード対象
         └─ *.dll
```

### 15.2 StreamingAssets 以下

```
StreamingAssets/
├─ ExplorerItemScanner/      # UNITY_STANDALONE_WIN のみ使用
│  └─ ExplorerItemScanner.exe
└─ tray_icon.ico             # TODO
```

### 15.3 スクリプトフォルダ構成

```
Assets/Scripts/
├─ Platform/
│  ├─ ICredentialStorage.cs          ← 新設
│  ├─ IAppIntegration.cs             ← 新設
│  ├─ PlatformServiceFactory.cs      ← 新設
│  ├─ DummyCredentialStorage.cs      ← 新設
│  ├─ DummyAppIntegration.cs         ← 新設
│  └─ Windows/                       # UNITY_STANDALONE_WIN
│     ├─ WindowsCredentialStorage.cs ← 新設
│     └─ WindowsAppIntegration.cs    ← 新設（旧: SystemTrayManager + TaskbarIconHider + MultiMonitorEnabler）
├─ AppIntegrationManager.cs          ← 新設
├─ ConfigManager.cs
├─ ThemeManager.cs
├─ ThemeSettings.cs
├─ CharacterManager.cs
├─ SpeechBubblePositioner.cs
├─ SpeechBubbleAutoResizer.cs
├─ SpeechBubbleController.cs         ← 新設
├─ DialogueWindowUI.cs
├─ ObjectToBottomRight.cs
├─ FileDropRouter.cs
├─ PopupManager.cs
├─ PopupController.cs
├─ SpriteFactory.cs
├─ TextureLoader.cs
├─ UILayoutUtility.cs
├─ PanelDragManipulator.cs
├─ SettingsUIManager.cs
├─ Daihon/
│  ├─ DaihonRunner.cs
│  ├─ UniTaskActionAdapter.cs
│  ├─ SimpleVariableStore.cs
│  └─ YuukeiActionHandler.cs         ← 新設
├─ Package/
│  └─ PackageManager.cs              ← 新設
├─ Plugin/                           # UNITY_STANDALONE
│  └─ PluginLoader.cs                ← 新設
├─ Trigger/
│  ├─ TriggerManager.cs              ← 新設
│  ├─ Triggers/
│  │  ├─ TimeTrigger.cs
│  │  ├─ ClickTrigger.cs
│  │  ├─ SystemEventTrigger.cs
│  │  ├─ FileDropTrigger.cs
│  │  └─ ExplorerItemTrigger.cs      # UNITY_STANDALONE_WIN
│  └─ DaihonFunctionRegistry.cs      ← 新設
├─ LLM/
│  └─ LLMBridge.cs                   ← 新設
└─ Windows/
   └─ ExplorerItemScanner.cs         # UNITY_STANDALONE_WIN（通信プロトコル部分のみ残す）
```

**削除するファイル（役割を Platform/ 層に移管）:**

| 旧ファイル | 移管先 |
|---|---|
| `Windows/SystemTrayManager.cs` | `WindowsAppIntegration.InitializeTray()` |
| `Windows/TaskbarIconHider.cs` | `WindowsAppIntegration.HideFromTaskbar()` |
| `Windows/MultiMonitorEnabler.cs` | `WindowsAppIntegration.SetupMultiMonitor()` |
| `Windows/WindowsActions.cs` | `WindowsAppIntegration` に統合 |

---

## 16. 起動シーケンス

以下の順序で初期化を行うこと。順序を変えると依存関係の欠如によりNullReferenceExceptionが発生する。

```
[Awake フェーズ]
1. ConfigManager.Awake()           → settings.json ロード → OnConfigLoaded 発火
2. ThemeManager.Awake()            → シングルトン設定のみ
3. PackageManager.Awake()          → シングルトン設定のみ
4. PluginLoader.Awake()            → シングルトン設定のみ（UNITY_STANDALONE のみ）
5. TriggerManager.Awake()          → シングルトン設定のみ
6. LLMBridge.Awake()               → PlatformServiceFactory.CreateCredentialStorage() で初期化
7. PopupManager.Awake()            → シングルトン設定のみ
8. AppIntegrationManager.Awake()   → PlatformServiceFactory.CreateAppIntegration() で初期化

[Start フェーズ]
9.  PluginLoader.Start()           ← UNITY_STANDALONE のみ
    → pending_unload.json を読み込みアンロード対象を除外リストに追加
    → Path.Combine(persistentDataPath, "Plugins") のDLLをロード

10. PackageManager.Start()
    → installedPackages を順番にリストア
    → 各パッケージの theme/ scripts/ plugins/ を再適用

11. CharacterManager.Start()
    → ConfigManager.OnConfigLoaded を購読
    → savedCharacters.Count > 0 → await LoadCharacterAsync(currentCharacterId)
    → Count == 0 → SettingsUIManager.ShowCharacterTab()

12. TriggerManager.Start()
    → 組み込み時間変数を RegisterDynamicGetter で登録（§9.3参照）
    → StartMonitoring()

13. AppIntegrationManager.Start()
    → _integration.InitializeTray(...)
    → _integration.HideFromTaskbar()
    → _integration.SetupMultiMonitor()
```

---

## 17. エラー処理規則

### 17.1 原則

| 状況 | 対応 |
|---|---|
| ファイルが存在しない | `Debug.LogWarning` を出して処理をスキップ。例外を投げない |
| JSONのデシリアライズ失敗 | `Debug.LogError` → デフォルト値にフォールバック |
| VRMロード失敗 | `Debug.LogError` → `_currentVrmInstance = null` のまま |
| DLLロード失敗（ReflectionTypeLoadException等） | `Debug.LogError` → そのDLLをスキップ。他のDLLの処理を続行 |
| 台本パースエラー | `DaihonRunner` がログを出力し、そのファイルの実行をスキップ |
| LLM通信失敗 | `Debug.LogWarning` → `""` を返す（YuukeiActionHandlerがフォールバックセリフを表示） |
| 未知の関数名 | `Debug.LogWarning` → `DaihonValue.None` を返す。実行を継続 |
| 動的変数への代入（§9.3） | `InvalidOperationException` をスローする（台本の記述ミスを明示するため） |

### 17.2 禁止事項

- **禁止:** ユーザーへの影響範囲を特定できないまま `Application.Quit` を呼ぶこと
- **禁止:** `catch (Exception)` で握りつぶしてログも出さないこと
- **禁止:** UnityのメインスレッドでLLMやファイルI/OなどブロッキングI/Oを実行すること。必ず `UniTask` / `UniTask.RunOnThreadPool` を使う
- **禁止:** ファイルパスをスラッシュや文字列連結で組み立てること。必ず `Path.Combine` を使う
- **禁止:** `Task` を直接 `return` / `await` すること。`UniTask` に統一する（`Task` を返す外部APIは `AsUniTask()` 拡張で変換する）
- **禁止:** `File.ReadAllBytes` + `Texture2D.LoadImage` をメインスレッドで呼ぶこと。テクスチャロードは `UnityWebRequestTexture` を使う（§5.4参照）
- **禁止:** `AssemblyLoadContext.Unload()` を実装すること（§7.4参照）
- **禁止:** `ICredentialStorage` を経由せずにAPIキーを `LLMSettings` や `settings.json` に保存すること
- **禁止:** イベントハンドラ等から非同期メソッドを呼ぶ場合は`async void`を使用せず、`.Forget()`を使用して呼び出すこと（例: `LoadCharacterAsync(id).Forget();`）

---

## 付録A: Daihon DSL 組み込み変数一覧

`SimpleVariableStore.RegisterDynamicGetter` で登録する読み取り専用変数。

| 変数名 | 型 | 評価式 |
|---|---|---|
| `年` | number | `DateTime.Now.Year` |
| `月` | number | `DateTime.Now.Month` |
| `日` | number | `DateTime.Now.Day` |
| `曜日` | string | `"日月火水木金土"[(int)DateTime.Now.DayOfWeek]` → `「日」`〜`「土」` |
| `週` | number | `CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(...)` |
| `時` | number | `DateTime.Now.Hour` |
| `分` | number | `DateTime.Now.Minute` |
| `秒` | number | `DateTime.Now.Second` |
| `ミリ秒` | number | `DateTime.Now.Millisecond` |

---

## 付録B: 未実装・TODO一覧

| 項目 | 優先度 | 理由 |
|---|---|---|
| マーケットプレイスAPIエンドポイント | Medium | 外部Webサービス側と合わせて決定 |
| カスタムトレイアイコン（tray_icon.ico） | Low | アセット未作成 |
| 小物（Prop）システム | Low | 設計未確定 |
| 複数キャラクター同時表示 | Low | CharacterManagerの設計変更が必要 |
| 台本のホットリロード | Low | 開発時の利便性機能 |
| TTS（音声合成）連携 | Low | ニーズ未確認 |
| macOS向けKeychain実装 | Low | DummyCredentialStorageで代替中 |
| Android向けKeystore実装 | Low | DummyCredentialStorageで代替中 |
| `.daihon` 単体ドロップのインポート | Low | FileDropRouter内のTODO |
