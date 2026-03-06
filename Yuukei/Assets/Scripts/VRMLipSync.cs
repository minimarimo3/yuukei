// ==========================================================================
// VRMLipSync.cs
// テキストの各文字を日本語の母音にマッピングし、VRM の口形状を
// アニメーションさせるテキスト送り式リップシンク。
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UniVRM10;
using UnityEngine;

/// <summary>
/// VRM キャラクターのテキストベース口パクコンポーネント。
/// ShowDialogueAsync 等からテキストを渡すと、1文字ずつ母音に変換して
/// VRM Expression (Aa/Ih/Ou/Ee/Oh) をアニメーションさせます。
/// </summary>
public class VRMLipSync : MonoBehaviour
{
    [Header("依存コンポーネント")]
    [SerializeField, Tooltip("VRM をロードする CharacterManager")]
    private CharacterManager characterManager;

    [Header("口パク設定")]
    [SerializeField, Tooltip("1文字あたりの口パク時間（秒）")]
    private float _secondsPerChar = 0.08f;

    [SerializeField, Tooltip("口の開閉補間速度（大きいほど素早く開閉）")]
    private float _lerpSpeed = 20f;

    [SerializeField, Tooltip("口を開く最大 weight (0〜1)")]
    private float _maxWeight = 1.0f;

    // 現在の各母音ターゲット weight
    private float _targetAa, _targetIh, _targetOu, _targetEe, _targetOh;
    // 現在の各母音 weight（補間用）
    private float _currentAa, _currentIh, _currentOu, _currentEe, _currentOh;

    private Vrm10Instance _cachedVrm;
    private bool _isSpeaking;

    // ================================================================
    // 母音マッピング（ひらがな・カタカナ → 母音インデックス）
    // 0=あ, 1=い, 2=う, 3=え, 4=お, -1=無音(口を閉じる)
    // ================================================================

    private static readonly Dictionary<char, int> VowelMap = BuildVowelMap();

    private static Dictionary<char, int> BuildVowelMap()
    {
        var map = new Dictionary<char, int>();

        // ひらがな あ段(0) い段(1) う段(2) え段(3) お段(4)
        // あ行
        map['あ'] = 0; map['い'] = 1; map['う'] = 2; map['え'] = 3; map['お'] = 4;
        // か行
        map['か'] = 0; map['き'] = 1; map['く'] = 2; map['け'] = 3; map['こ'] = 4;
        map['が'] = 0; map['ぎ'] = 1; map['ぐ'] = 2; map['げ'] = 3; map['ご'] = 4;
        // さ行
        map['さ'] = 0; map['し'] = 1; map['す'] = 2; map['せ'] = 3; map['そ'] = 4;
        map['ざ'] = 0; map['じ'] = 1; map['ず'] = 2; map['ぜ'] = 3; map['ぞ'] = 4;
        // た行
        map['た'] = 0; map['ち'] = 1; map['つ'] = 2; map['て'] = 3; map['と'] = 4;
        map['だ'] = 0; map['ぢ'] = 1; map['づ'] = 2; map['で'] = 3; map['ど'] = 4;
        // な行
        map['な'] = 0; map['に'] = 1; map['ぬ'] = 2; map['ね'] = 3; map['の'] = 4;
        // は行
        map['は'] = 0; map['ひ'] = 1; map['ふ'] = 2; map['へ'] = 3; map['ほ'] = 4;
        map['ば'] = 0; map['び'] = 1; map['ぶ'] = 2; map['べ'] = 3; map['ぼ'] = 4;
        map['ぱ'] = 0; map['ぴ'] = 1; map['ぷ'] = 2; map['ぺ'] = 3; map['ぽ'] = 4;
        // ま行
        map['ま'] = 0; map['み'] = 1; map['む'] = 2; map['め'] = 3; map['も'] = 4;
        // や行
        map['や'] = 0; map['ゆ'] = 2; map['よ'] = 4;
        // ら行
        map['ら'] = 0; map['り'] = 1; map['る'] = 2; map['れ'] = 3; map['ろ'] = 4;
        // わ行
        map['わ'] = 0; map['ゐ'] = 1; map['ゑ'] = 3; map['を'] = 4;
        // ん — 軽く口を閉じる
        map['ん'] = -1;
        // 小文字
        map['ぁ'] = 0; map['ぃ'] = 1; map['ぅ'] = 2; map['ぇ'] = 3; map['ぉ'] = 4;
        map['ゃ'] = 0; map['ゅ'] = 2; map['ょ'] = 4;
        map['っ'] = -1;

        // カタカナ ア段(0) イ段(1) ウ段(2) エ段(3) オ段(4)
        map['ア'] = 0; map['イ'] = 1; map['ウ'] = 2; map['エ'] = 3; map['オ'] = 4;
        map['カ'] = 0; map['キ'] = 1; map['ク'] = 2; map['ケ'] = 3; map['コ'] = 4;
        map['ガ'] = 0; map['ギ'] = 1; map['グ'] = 2; map['ゲ'] = 3; map['ゴ'] = 4;
        map['サ'] = 0; map['シ'] = 1; map['ス'] = 2; map['セ'] = 3; map['ソ'] = 4;
        map['ザ'] = 0; map['ジ'] = 1; map['ズ'] = 2; map['ゼ'] = 3; map['ゾ'] = 4;
        map['タ'] = 0; map['チ'] = 1; map['ツ'] = 2; map['テ'] = 3; map['ト'] = 4;
        map['ダ'] = 0; map['ヂ'] = 1; map['ヅ'] = 2; map['デ'] = 3; map['ド'] = 4;
        map['ナ'] = 0; map['ニ'] = 1; map['ヌ'] = 2; map['ネ'] = 3; map['ノ'] = 4;
        map['ハ'] = 0; map['ヒ'] = 1; map['フ'] = 2; map['ヘ'] = 3; map['ホ'] = 4;
        map['バ'] = 0; map['ビ'] = 1; map['ブ'] = 2; map['ベ'] = 3; map['ボ'] = 4;
        map['パ'] = 0; map['ピ'] = 1; map['プ'] = 2; map['ペ'] = 3; map['ポ'] = 4;
        map['マ'] = 0; map['ミ'] = 1; map['ム'] = 2; map['メ'] = 3; map['モ'] = 4;
        map['ヤ'] = 0; map['ユ'] = 2; map['ヨ'] = 4;
        map['ラ'] = 0; map['リ'] = 1; map['ル'] = 2; map['レ'] = 3; map['ロ'] = 4;
        map['ワ'] = 0; map['ヰ'] = 1; map['ヱ'] = 3; map['ヲ'] = 4;
        map['ン'] = -1;
        // 小文字カタカナ
        map['ァ'] = 0; map['ィ'] = 1; map['ゥ'] = 2; map['ェ'] = 3; map['ォ'] = 4;
        map['ャ'] = 0; map['ュ'] = 2; map['ョ'] = 4;
        map['ッ'] = -1;
        // 長音
        map['ー'] = -2; // 特殊: 前の母音を継続

        return map;
    }

    // ================================================================
    // Update — 補間で滑らかに口形状を変化させる
    // ================================================================

    private void Update()
    {
        if (!_isSpeaking) return;

        var vrm = GetVrmInstance();
        if (vrm == null) return;

        float dt = Time.deltaTime * _lerpSpeed;

        _currentAa = Mathf.Lerp(_currentAa, _targetAa, dt);
        _currentIh = Mathf.Lerp(_currentIh, _targetIh, dt);
        _currentOu = Mathf.Lerp(_currentOu, _targetOu, dt);
        _currentEe = Mathf.Lerp(_currentEe, _targetEe, dt);
        _currentOh = Mathf.Lerp(_currentOh, _targetOh, dt);

        var expr = vrm.Runtime.Expression;
        expr.SetWeight(ExpressionKey.Aa, _currentAa);
        expr.SetWeight(ExpressionKey.Ih, _currentIh);
        expr.SetWeight(ExpressionKey.Ou, _currentOu);
        expr.SetWeight(ExpressionKey.Ee, _currentEe);
        expr.SetWeight(ExpressionKey.Oh, _currentOh);
    }

    // ================================================================
    // Public API
    // ================================================================

    /// <summary>
    /// テキスト全体を口パクで再生します。
    /// 完了するまで await できます。
    /// </summary>
    public async UniTask SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(text)) return;

        var vrm = GetVrmInstance();
        if (vrm == null)
        {
            Debug.LogWarning("[VRMLipSync] VRM インスタンスが見つかりません。口パクをスキップします。");
            // VRM が無くても待機時間は確保する
            await UniTask.Delay(
                TimeSpan.FromSeconds(text.Length * _secondsPerChar),
                cancellationToken: ct);
            return;
        }

        _isSpeaking = true;
        int prevVowel = -1;

        try
        {
            foreach (char c in text)
            {
                if (ct.IsCancellationRequested) break;

                int vowel = CharToVowel(c, prevVowel);

                if (vowel == -1)
                {
                    // 無音 — 口を閉じる
                    SetTargetVowel(-1);
                }
                else if (vowel >= 0 && vowel <= 4)
                {
                    SetTargetVowel(vowel);
                    prevVowel = vowel;
                }
                else
                {
                    // 記号等 — 口を閉じる
                    SetTargetVowel(-1);
                }

                await UniTask.Delay(
                    TimeSpan.FromSeconds(_secondsPerChar),
                    cancellationToken: ct);
            }
        }
        finally
        {
            // 終了時は口を閉じる
            SetTargetVowel(-1);
            // 補間が収束するまで少し待つ
            await UniTask.Delay(TimeSpan.FromSeconds(0.15f), cancellationToken: ct);
            _isSpeaking = false;
            ResetWeights(vrm);
        }
    }

    // ================================================================
    // Internal helpers
    // ================================================================

    /// <summary>
    /// キャッシュされた VRM インスタンスを無効化する。
    /// CharacterManager がキャラクターを切り替えた後に呼ぶ（§11.3参照）。
    /// </summary>
    public void InvalidateCache()
    {
        _cachedVrm = null;
    }

    private Vrm10Instance GetVrmInstance()
    {
        if (_cachedVrm != null) return _cachedVrm;

        if (characterManager != null)
        {
            _cachedVrm = characterManager.CurrentVrmInstance;
        }

        return _cachedVrm;
    }

    /// <summary>
    /// 文字を母音インデックスに変換する。
    /// -1 = 無音, 0=あ, 1=い, 2=う, 3=え, 4=お
    /// </summary>
    private int CharToVowel(char c, int prevVowel)
    {
        // マッピング済みの文字を検索
        if (VowelMap.TryGetValue(c, out int vowel))
        {
            if (vowel == -2)
            {
                // 長音符 — 前の母音を継続
                return prevVowel >= 0 ? prevVowel : 0;
            }
            return vowel;
        }

        // 句読点・記号・空白 → 口を閉じる
        if (char.IsPunctuation(c) || char.IsWhiteSpace(c) || char.IsSymbol(c)
            || c == '「' || c == '」' || c == '！' || c == '？' || c == '。' || c == '、')
        {
            return -1;
        }

        // 漢字・英字など判別できないもの → デフォルトで「あ」
        return 0;
    }

    /// <summary>
    /// ターゲット母音を設定する。-1 で全て 0（口を閉じる）。
    /// </summary>
    private void SetTargetVowel(int vowelIndex)
    {
        _targetAa = 0f;
        _targetIh = 0f;
        _targetOu = 0f;
        _targetEe = 0f;
        _targetOh = 0f;

        switch (vowelIndex)
        {
            case 0: _targetAa = _maxWeight; break;
            case 1: _targetIh = _maxWeight; break;
            case 2: _targetOu = _maxWeight; break;
            case 3: _targetEe = _maxWeight; break;
            case 4: _targetOh = _maxWeight; break;
            // -1 or default: all zero (mouth closed)
        }
    }

    /// <summary>
    /// 全ての口形状 weight をリセットする。
    /// </summary>
    private void ResetWeights(Vrm10Instance vrm)
    {
        _currentAa = _currentIh = _currentOu = _currentEe = _currentOh = 0f;
        _targetAa = _targetIh = _targetOu = _targetEe = _targetOh = 0f;

        if (vrm != null)
        {
            var expr = vrm.Runtime.Expression;
            expr.SetWeight(ExpressionKey.Aa, 0f);
            expr.SetWeight(ExpressionKey.Ih, 0f);
            expr.SetWeight(ExpressionKey.Ou, 0f);
            expr.SetWeight(ExpressionKey.Ee, 0f);
            expr.SetWeight(ExpressionKey.Oh, 0f);
        }
    }
}
