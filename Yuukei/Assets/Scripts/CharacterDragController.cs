// ==========================================================================
// CharacterDragController.cs
// キャラクターを長押し＋ドラッグした際にアニメーションを再生し、
// マウスを離した位置へキャラクターを移動させる。
// ==========================================================================

using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UniVRM10;

[DisallowMultipleComponent]
public class CharacterDragController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CharacterManager characterManager;
    [SerializeField] private FloatingMotionController floatingMotion;
    [SerializeField] private VrmaPlayer vrmaPlayer;

    [Header("Drag Animation")]
    [SerializeField, Tooltip("ドラッグ中に再生するVRMAプレハブ（例: X Bot@Female Dynamic Pose）")]
    private Vrm10AnimationInstance dragAnimPrefab;

    [Header("Settings")]
    [SerializeField, Tooltip("長押し判定時間（秒）")]
    private float longPressDuration = 0.25f;
    [SerializeField, Tooltip("ドラッグ開始と判定するカーソル移動距離（ピクセル）")]
    private float dragThreshold = 8f;

    // ── Runtime State ──────────────────────────────────────────────────────
    private bool _isPressing;
    private bool _isDragging;
    private bool _dragAnimationStarted;
    private float _pressTimer;
    private Vector2 _pressStartPos;

    // 追加: ドラッグ開始時の座標を記憶する変数
    private Vector3 _dragStartAnchorPos;
    private Vector3 _dragStartMouseWorldPos;

    private CancellationTokenSource _moveCts;

    private void Start()
    {
        if (characterManager == null) characterManager = FindObjectOfType<CharacterManager>();
        if (floatingMotion == null) floatingMotion = FindObjectOfType<FloatingMotionController>();
        if (vrmaPlayer == null) vrmaPlayer = FindObjectOfType<VrmaPlayer>();
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mousePos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            if (IsMouseOverCharacter(mousePos))
            {
                _isPressing    = true;
                _isDragging    = false;
                _pressTimer    = 0f;
                _pressStartPos = mousePos;
            }
        }

        if (!_isPressing) return;

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            if (_isDragging)
                EndDrag(mousePos);

            _isPressing = false;
            _isDragging = false;
            return;
        }

        if (!_isDragging)
        {
            _pressTimer += Time.deltaTime;

            Vector2 delta = mousePos - _pressStartPos;
            bool moved = delta.magnitude > dragThreshold;

            if (_pressTimer >= longPressDuration || moved)
                StartDrag(mousePos); // 引数を追加してマウス位置を渡す
        }
    }

    private void OnDestroy()
    {
        _moveCts?.Cancel();
        _moveCts?.Dispose();
    }

    private bool IsMouseOverCharacter(Vector2 screenPos)
    {
        if (characterManager?.CurrentVrmInstance == null) return false;
        if (Camera.main == null) return false;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        var renderers = characterManager.CurrentVrmInstance.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r.bounds.IntersectRay(ray)) return true;
        }
        return false;
    }

    private void StartDrag(Vector2 screenPos)
    {
        // 進行中の移動をキャンセルして AnchorPosition を安定させる
        _moveCts?.Cancel();
        _moveCts?.Dispose();
        _moveCts = null;

        _isDragging = true;
        _dragAnimationStarted = false;

        // ドラッグ開始時のキャラクター位置とマウスのワールド位置を記憶
        _dragStartAnchorPos = floatingMotion.AnchorPosition;
        _dragStartMouseWorldPos = GetMouseWorldPos(screenPos);

        if (dragAnimPrefab == null) return;
        vrmaPlayer?.PlayAnimation(dragAnimPrefab);
        _dragAnimationStarted = true;
    }

    private void EndDrag(Vector2 screenPos)
    {
        // ドラッグアニメーションを開始した場合のみ停止・デフォルト復元
        if (_dragAnimationStarted)
        {
            vrmaPlayer?.StopAnimation();
            vrmaPlayer?.PlayAnimation(); // vrmaPrefab を復元（未設定なら何もしない）
            _dragAnimationStarted = false;
        }

        if (floatingMotion == null || Camera.main == null) return;

        // ドラッグした差分（距離）を計算する
        Vector3 currentMouseWorldPos = GetMouseWorldPos(screenPos);
        Vector3 deltaWorld = currentMouseWorldPos - _dragStartMouseWorldPos;

        // ほとんど移動していない（長押ししてそのまま離しただけ）場合は処理をキャンセル
        if (deltaWorld.sqrMagnitude < 0.001f) return;

        // 元の位置 ＋ 動かした距離 を目標地点とする
        Vector3 targetPos = _dragStartAnchorPos + deltaWorld;

        _moveCts?.Cancel();
        _moveCts?.Dispose();
        _moveCts = new CancellationTokenSource();
        floatingMotion.MoveToAsync(targetPos, _moveCts.Token).Forget();
    }

    // マウスのスクリーン座標から、キャラクターと同じ奥行きのワールド座標を取得するヘルパー
    private Vector3 GetMouseWorldPos(Vector2 screenPos)
    {
        if (floatingMotion == null || Camera.main == null) return Vector3.zero;
        float dist = Mathf.Abs(Camera.main.transform.position.z - floatingMotion.AnchorPosition.z);
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, dist));
        worldPos.z = floatingMotion.AnchorPosition.z;
        return worldPos;
    }
}