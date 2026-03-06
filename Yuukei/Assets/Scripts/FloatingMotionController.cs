// ==========================================================================
// FloatingMotionController.cs
// CharacterRoot にアタッチするプロシージャルモーション制御コンポーネント。
// VrmaPlayer（ボーンポーズ制御）とは衝突しない - CharacterRoot の transform のみを操作する。
// VRM スプリングボーン（髪・服）は自動で親の動きに追従するため、追加実装不要でふわふわ感が増す。
// ==========================================================================

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public class FloatingMotionController : MonoBehaviour
{
    [Header("Idle Floating")]
    [SerializeField] float _floatAmplitudeY  = 0.06f;   // 上下振れ幅（m）
    [SerializeField] float _floatFrequency1  = 0.55f;   // 主波 Hz（ゆったりした浮遊）
    [SerializeField] float _floatFrequency2  = 1.10f;   // 倍波 Hz（有機的な揺らぎを加える）
    [SerializeField] float _floatFrequency3  = 0.28f;   // 遅波 Hz（大きなうねり）
    [SerializeField] float _floatAmplitudeX  = 0.018f;  // 横方向のわずかな揺れ（m）
    [SerializeField] float _tiltAmplitudeDeg = 1.8f;    // Z軸傾き最大角度（度）
    [SerializeField] float _tiltFrequency    = 0.40f;   // 傾き周期 Hz（浮遊とは少しずらす）

    [Header("Movement")]
    [SerializeField] float _windUpDuration    = 0.25f;  // 予備動作フェーズ（秒）
    [SerializeField] float _travelDuration    = 0.60f;  // 移動本体フェーズ（秒）
    [SerializeField] float _settleDuration    = 0.35f;  // 着地・姿勢戻しフェーズ（秒）
    [SerializeField] float _maxLeanDegrees    = 18f;    // 移動方向への最大傾き（度）
    [SerializeField] float _maxBodyTwistDeg   = 35f;    // 体ねじり最大角（Y軸回転、度）
    [SerializeField] float _overshootFraction = 0.06f;  // 移動距離に対するオーバーシュート割合
    [SerializeField] AnimationCurve _travelCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // ── Runtime State ──────────────────────────────────────────────────────
    private Vector3    _anchorPosition;              // 浮遊のベース位置（ObjectToBottomRight が設定）
    private bool       _anchorSet;
    private Quaternion _leanRotation = Quaternion.identity; // 移動由来のねじり・傾き
    private bool       _isMoving;
    private float      _floatTime;

    // 起動時ランダム位相：複数インスタンスやリプレイで同期しない
    private float _phase1, _phase2, _phase3, _phaseTilt;

    private CancellationTokenSource _moveCts;

    // ── Public API ─────────────────────────────────────────────────────────

    public Vector3 AnchorPosition => _anchorPosition;
    public bool    IsMoving       => _isMoving;

    /// <summary>
    /// 浮遊のベース位置を設定する。ObjectToBottomRight.PositionAtBottomRight() の末尾から呼ぶ。
    /// </summary>
    public void SetAnchorPosition(Vector3 worldPos)
    {
        _anchorPosition = worldPos;
        _anchorSet = true;
    }

    /// <summary>
    /// 指定ワールド座標へ3フェーズ（予備動作→移動→着地）で優雅に移動する。
    /// await することで完了を待機できる。
    /// </summary>
    public async UniTask MoveToAsync(Vector3 worldTarget,
        CancellationToken externalCt = default)
    {
        // 前の移動タスクをキャンセルして新しく開始
        _moveCts?.Cancel();
        _moveCts?.Dispose();
        _moveCts = new CancellationTokenSource();

        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _moveCts.Token,
            externalCt.Equals(default) ? this.GetCancellationTokenOnDestroy() : externalCt
        );
        var ct = linkedCts.Token;

        _isMoving = true;
        Vector3 startAnchor = _anchorPosition;

        // 移動方向ベクトル（Z無視・XY平面のみ）
        Vector3 dir = worldTarget - startAnchor;
        dir.z = 0f;
        float dist = dir.magnitude;
        Vector3 dirN = dist > 0.001f ? dir / dist : Vector3.right;

        // ねじり・傾きの符号
        float twistSign = Mathf.Sign(dirN.x); // 右移動→正（Y回転で右を向く）
        float leanSign  = -twistSign;          // 右移動→右傾き（Z軸負方向）

        try
        {
            // ── Phase 1: 予備動作（WindUp）──
            // 移動方向へ体を少し向け始め、視覚的な予備動作を作る
            float elapsed = 0f;
            while (elapsed < _windUpDuration)
            {
                ct.ThrowIfCancellationRequested();
                float t  = elapsed / _windUpDuration;
                float te = t * t; // EaseIn
                _leanRotation = Quaternion.Euler(
                    0f,
                    twistSign * _maxBodyTwistDeg * 0.4f * te,
                    leanSign  * _maxLeanDegrees  * 0.3f * te
                );
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            }

            // ── Phase 2: 移動（Travel）──
            // アンカー位置を目標へ動かし、体はねじれ・傾きながら移動
            elapsed = 0f;
            Vector3 overshootTarget = worldTarget + dirN * (dist * _overshootFraction);
            while (elapsed < _travelDuration)
            {
                ct.ThrowIfCancellationRequested();
                float t  = elapsed / _travelDuration;
                float te = _travelCurve.Evaluate(t);

                _anchorPosition = Vector3.LerpUnclamped(startAnchor, overshootTarget, te);

                // 傾きは移動の中間でピーク（ベル型）、ねじりは終盤に少し解消
                float bell  = Mathf.Sin(t * Mathf.PI); // 0→1→0
                float twist = 1f - t * t * 0.3f;
                _leanRotation = Quaternion.Euler(
                    0f,
                    twistSign * _maxBodyTwistDeg * twist,
                    leanSign  * _maxLeanDegrees  * bell
                );

                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            }
            _anchorPosition = overshootTarget;

            // ── Phase 3: 着地・姿勢戻し（Settle）──
            // オーバーシュートから目標へバネのように戻り、体もゆっくり正面に向き直す
            elapsed = 0f;
            Vector3 settleStart = overshootTarget;
            while (elapsed < _settleDuration)
            {
                ct.ThrowIfCancellationRequested();
                float t      = elapsed / _settleDuration;
                float te     = 1f - (1f - t) * (1f - t); // EaseOut
                float remain = 1f - te;
                _anchorPosition = Vector3.Lerp(settleStart, worldTarget, te);
                _leanRotation   = Quaternion.Euler(
                    0f,
                    twistSign * _maxBodyTwistDeg * 0.15f * remain,
                    leanSign  * _maxLeanDegrees  * 0.10f * remain
                );
                elapsed += Time.deltaTime;
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate, ct);
            }
            _anchorPosition = worldTarget;
            _leanRotation   = Quaternion.identity;
        }
        catch (System.OperationCanceledException)
        {
            // キャンセル時も姿勢をリセットして不自然な角度で止まらないようにする
            _leanRotation = Quaternion.identity;
        }
        finally
        {
            _isMoving = false;
            linkedCts.Dispose();
        }
    }

    // ── Unity Lifecycle ────────────────────────────────────────────────────

    private void Start()
    {
        _phase1    = Random.Range(0f, Mathf.PI * 2f);
        _phase2    = Random.Range(0f, Mathf.PI * 2f);
        _phase3    = Random.Range(0f, Mathf.PI * 2f);
        _phaseTilt = Random.Range(0f, Mathf.PI * 2f);

        // SetAnchorPosition が Start より前に呼ばれていない場合は現在位置を使う
        if (!_anchorSet)
        {
            _anchorPosition = transform.position;
            _anchorSet = true;
        }
    }

    private void LateUpdate()
    {
        if (!_anchorSet) return;

        _floatTime += Time.deltaTime;

        // Y浮遊：3つの正弦波を重ねて有機的な揺れを作る
        // 係数 0.55 / 0.30 / 0.15 で主波を主役に保ちつつ変化を加える
        float y = _floatAmplitudeY * (
            0.55f * Mathf.Sin(2f * Mathf.PI * _floatFrequency1 * _floatTime + _phase1) +
            0.30f * Mathf.Sin(2f * Mathf.PI * _floatFrequency2 * _floatTime + _phase2) +
            0.15f * Mathf.Sin(2f * Mathf.PI * _floatFrequency3 * _floatTime + _phase3)
        );

        // X横揺れ：独立した位相でわずかに漂う
        float x = _floatAmplitudeX *
            Mathf.Sin(2f * Mathf.PI * _floatFrequency3 * _floatTime + _phase1 + 1.2f);

        // Z傾き：浮遊とは異なる周期でゆっくり揺れる
        float tiltZ = _tiltAmplitudeDeg *
            Mathf.Sin(2f * Mathf.PI * _tiltFrequency * _floatTime + _phaseTilt);

        // 移動由来のねじり（_leanRotation）× アイドル傾きを合成
        transform.rotation = _leanRotation * Quaternion.Euler(0f, 0f, tiltZ);
        transform.position = _anchorPosition + new Vector3(x, y, 0f);
    }

    private void OnDestroy()
    {
        _moveCts?.Cancel();
        _moveCts?.Dispose();
    }
}
