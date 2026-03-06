// ==========================================================================
// SampleDaihonHandler.cs
// IUniTaskActionHandler を実装するサンプル MonoBehaviour。
// Unity の Console にセリフや関数呼び出しのログを出力するデモです。
// ==========================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Daihon;
using Daihon.Unity;
using UnityEngine;

namespace Daihon.Samples
{
    /// <summary>
    /// DaihonScript の動作確認用サンプル MonoBehaviour。
    /// Inspector で台本テキストを設定するか、デフォルトのサンプル台本を使用します。
    /// </summary>
    public class SampleDaihonHandler : MonoBehaviour, IUniTaskActionHandler
    {
        [Header("台本テキスト（空欄の場合はサンプルを使用）")]
        [SerializeField, TextArea(10, 30)]
        private string _scriptText = "";

        [Header("セリフ表示の待機時間（秒）")]
        [SerializeField]
        private float _dialogueWaitSeconds = 1.5f;

        [Header("口パク")]
        [SerializeField, Tooltip("VRM 口パクコンポーネント（任意）")]
        private VRMLipSync _lipSync;

        [Header("ふきだしテキスト")]
        [SerializeField, Tooltip("TalkCanvasのテキスト")]
        private TMPro.TextMeshProUGUI _speechBubbleText;

        [Header("モーション")]
        [SerializeField, Tooltip("VRMAアニメーション再生用コンポーネント")]
        private VrmaPlayer _vrmaPlayer;

        private void Start()
        {
            RunSampleAsync().Forget();
        }

        private async UniTaskVoid RunSampleAsync()
        {
            var script = string.IsNullOrWhiteSpace(_scriptText) ? GetSampleScript() : _scriptText;
            var store = new SimpleVariableStore();

            Debug.Log("<color=cyan>[Daihon] ===== 台本の実行を開始します =====</color>");
            await DaihonRunner.RunAsync(script, this, store);
            Debug.Log("<color=cyan>[Daihon] ===== 台本の実行が完了しました =====</color>");
        }

        // ================================================================
        // IUniTaskActionHandler の実装
        // ================================================================

        /// <summary>セリフを表示し、口パク再生（または待機）する。</summary>
        public async UniTask ShowDialogueAsync(string text)
        {
            Debug.Log($"<color=yellow>[セリフ]</color> {text}");

            if (_speechBubbleText != null)
            {
                _speechBubbleText.text = text;
            }

            var ct = this.GetCancellationTokenOnDestroy();

            if (_lipSync != null)
            {
                // 口パクでセリフを再生（テキスト長に応じた自然な待機）
                await _lipSync.SpeakAsync(text, ct);
            }
            else
            {
                // 口パク未設定時は従来どおり固定秒数だけ待機
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(_dialogueWaitSeconds),
                    cancellationToken: ct);
            }
        }

        /// <summary>関数呼び出しをログ出力する。</summary>
        public UniTask<DaihonValue> CallFunctionAsync(
            string functionName,
            IReadOnlyList<DaihonValue> positionalArgs,
            IReadOnlyDictionary<string, DaihonValue> namedArgs)
        {
            // 引数を文字列に整形
            var args = new List<string>();
            for (int i = 0; i < positionalArgs.Count; i++)
                args.Add(positionalArgs[i].ToDisplayString());
            foreach (var kv in namedArgs)
                args.Add($"{kv.Key}={kv.Value.ToDisplayString()}");

            var argsStr = args.Count > 0 ? string.Join(", ", args) : "なし";
            Debug.Log($"<color=green>[関数呼出]</color> ＜{functionName}＞ 引数: {argsStr}");

            // 実際のプロジェクトでは、関数名に応じて
            // アニメーション再生やSE再生などを行います。
            // 例:
            // switch (functionName)
            // {
            //     case "表情":
            //         SetExpression(positionalArgs[0].AsString());
            //         break;
            //     case "待つ":
            //         await UniTask.Delay(TimeSpan.FromSeconds(positionalArgs[0].AsNumber()));
            //         break;
            // }

            switch(functionName) {
                case "動作":
                    if (argsStr.Contains("歩行") ) {
                        if (_vrmaPlayer != null) {
                            _vrmaPlayer.PlayAnimation();
                        } else {
                            Debug.LogWarning("[SampleDaihonHandler] VrmaPlayerへの参照が設定されていません。");
                        }
                    }
                    else if (argsStr.Contains("停止")) {
                        if (_vrmaPlayer != null) {
                            _vrmaPlayer.StopAnimation();
                        }
                    }
                    break;
                default:
                    Debug.LogWarning($"[Daihon] 未定義の関数呼び出し: {functionName}");
                    break;
            }

            return UniTask.FromResult(DaihonValue.None);
        }

        // ================================================================
        // サンプル台本
        // ================================================================

        private static string GetSampleScript()
        {
            return @"## あいさつイベント

### メイン
「こんにちは！」
＜表情 笑顔＞
「今日もいい天気ですね。」
「一緒に遊びましょう！」
";
        }
    }
}
