using UnityEngine;
using UniVRM10;

/// <summary>
/// CharacterManager からロードされた VRM の頭部を追従して、吹き出し（UI）の位置を動的に計算するコンポーネント。
/// </summary>
public class SpeechBubblePositioner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField, Tooltip("VRMをロード・管理しているマネージャー")] 
    private CharacterManager characterManager;
    
    [SerializeField, Tooltip("移動対象となる吹き出しの RectTransform (未指定なら自身のアタッチ先)")] 
    private RectTransform targetBubble;
    
    [SerializeField, Tooltip("吹き出しを収める境界となる Canvas 等の RectTransform (通常は TalkCanvas)")] 
    private RectTransform canvasRect;
    
    [SerializeField, Tooltip("描画に使用しているカメラ (未指定なら Camera.main)")] 
    private Camera targetCamera;

    [Header("Positioning Settings")]
    [SerializeField, Tooltip("キャラクターの頭からのワールド座標オフセット (例: Yを少し上げて頭上に配置)")]
    private Vector3 worldOffset = new Vector3(0f, 0.4f, 0f);

    [SerializeField, Tooltip("スクリーン座標上での追加オフセット")]
    private Vector2 screenOffset = new Vector2(0f, 0f);

    [Header("Clamping Settings")]
    [SerializeField, Tooltip("画面端（canvasRect内）に収まるように制限するかどうか")]
    private bool enableClamping = true;

    [SerializeField, Tooltip("画面端からの余白（パディング）")]
    private Vector2 screenPadding = new Vector2(20f, 20f);

    [Header("UI References (Optional)")]
    [SerializeField, Tooltip("背景のRectTransform (自動で子から検索します)")]
    private RectTransform backgroundRect;

    [SerializeField, Tooltip("しっぽのRectTransform (自動で子から検索します)")]
    private RectTransform tailRect;

    private float _initialTailLocalX;

    private Transform _headBone;
    private Vrm10Instance _lastInstance;
    private Canvas _parentCanvas;

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetBubble == null)
        {
            targetBubble = GetComponent<RectTransform>();
        }

        if (canvasRect != null)
        {
            _parentCanvas = canvasRect.GetComponentInParent<Canvas>();
        }

        if (backgroundRect == null && targetBubble != null)
        {
            var bg = targetBubble.Find("Background");
            if (bg != null) backgroundRect = bg.GetComponent<RectTransform>();
        }

        if (tailRect == null && targetBubble != null)
        {
            var tail = targetBubble.Find("Tail");
            if (tail != null) 
            {
                tailRect = tail.GetComponent<RectTransform>();
                _initialTailLocalX = tailRect.localPosition.x;
            }
        }
        else if (tailRect != null)
        {
            _initialTailLocalX = tailRect.localPosition.x;
        }
    }

    private void LateUpdate()
    {
        if (characterManager == null || targetBubble == null || canvasRect == null || targetCamera == null)
            return;

        var vrm = characterManager.CurrentVrmInstance;
        if (vrm == null)
        {
            // VRMが表示されていない場合は処理しない。あるいは非表示にする等の制御を追加可能
            return;
        }

        // リファレンスが変わった、またはボーンが未取得の場合の初期化
        if (vrm != _lastInstance || _headBone == null)
        {
            _lastInstance = vrm;
            var animator = vrm.GetComponent<Animator>();
            if (animator != null)
            {
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        if (_headBone == null) return;

        // 1. 頭のワールド座標にオフセットを加えてスクリーン座標へ変換
        Vector3 targetWorldPos = _headBone.position + worldOffset;
        Vector3 screenPoint = targetCamera.WorldToScreenPoint(targetWorldPos);

        // カメラ背面側にいる場合は暫定的に無視
        if (screenPoint.z < 0) return;

        screenPoint.x += screenOffset.x;
        screenPoint.y += screenOffset.y;

        // Canvasのモードに応じてCameraを渡すかnullにするかを決定
        Camera eventCamera = null;
        if (_parentCanvas != null && (_parentCanvas.renderMode == RenderMode.ScreenSpaceCamera || _parentCanvas.renderMode == RenderMode.WorldSpace))
        {
            eventCamera = _parentCanvas.worldCamera ?? targetCamera;
        }

        // 2. スクリーン座標を Canvas内 (canvasRect) のローカル座標に変換
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            Vector2 originalLocalPoint = localPoint;

            // 3. UIが画面外に出ないようにクランプ (Clamping)
            if (enableClamping)
            {
                ClampToCanvas(ref localPoint);
            }

            // 4. 座標を適用
            targetBubble.localPosition = localPoint;

            // 5. しっぽの位置補正 (端に寄ったときにしっぽがキャラを指し示すように逆方向へシフト)
            if (tailRect != null)
            {
                float shiftX = localPoint.x - originalLocalPoint.x;
                float targetTailX = _initialTailLocalX - shiftX; // 逆方向に動かす
                
                if (backgroundRect != null)
                {
                    float halfBgW = backgroundRect.rect.width * 0.5f;
                    float halfTailW = tailRect.rect.width * 0.5f;
                    float maxOffset = Mathf.Max(0, halfBgW - halfTailW - 10f); // margin
                    targetTailX = Mathf.Clamp(targetTailX, -maxOffset, maxOffset);
                }

                if (Mathf.Abs(tailRect.localPosition.x - targetTailX) > 0.01f)
                {
                    tailRect.localPosition = new Vector3(targetTailX, tailRect.localPosition.y, tailRect.localPosition.z);
                }
            }
        }
    }

    /// <summary>
    /// 計算されたローカル座標が canvasRect の領域外にはみ出ないように Clamp します。
    /// </summary>
    private void ClampToCanvas(ref Vector2 localPoint)
    {
        // 制限領域 (コンテナ) のサイズ情報
        Rect cRect = canvasRect.rect;
        
        // 吹き出しの実質的なサイズ（Backgroundがあればそちらを優先）
        Rect bRect = (backgroundRect != null) ? backgroundRect.rect : targetBubble.rect;
        Vector2 pivot = targetBubble.pivot;

        // はみ出さないための限界座標（Min / Max）をピボットと「スケール(localScale)」を考慮して計算
        float scaledWidth = bRect.width * targetBubble.localScale.x;
        float scaledHeight = bRect.height * targetBubble.localScale.y;

        float minX = cRect.min.x + (scaledWidth * pivot.x) + screenPadding.x;
        float maxX = cRect.max.x - (scaledWidth * (1f - pivot.x)) - screenPadding.x;

        float minY = cRect.min.y + (scaledHeight * pivot.y) + screenPadding.y;
        float maxY = cRect.max.y - (scaledHeight * (1f - pivot.y)) - screenPadding.y;

        // もし画面が小さすぎて min > max になってしまう場合は中央をとる
        if (minX > maxX)
        {
            float expectedMidX = (minX + maxX) * 0.5f;
            minX = expectedMidX;
            maxX = expectedMidX;
        }
        if (minY > maxY)
        {
            float expectedMidY = (minY + maxY) * 0.5f;
            minY = expectedMidY;
            maxY = expectedMidY;
        }

        // Clamp
        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);
    }
}
