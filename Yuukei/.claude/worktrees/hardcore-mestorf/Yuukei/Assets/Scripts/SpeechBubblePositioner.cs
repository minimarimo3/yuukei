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

    [Header("Clamping & Avoidance Settings")]
    [SerializeField, Tooltip("キャラクターと被らないように左右に自動配置（回避）するかどうか")]
    private bool avoidCharacter = true;

    [SerializeField, Tooltip("キャラクターの頭部から左右に離す距離（マージン）")]
    private float characterMarginX = 160f;

    [SerializeField, Tooltip("画面端（canvasRect内）に収まるように制限するかどうか")]
    private bool enableClamping = true;

    [SerializeField, Tooltip("画面端からの余白（パディング）")]
    private Vector2 screenPadding = new Vector2(20f, 20f);

    [Header("UI References (Optional)")]
    [SerializeField, Tooltip("背景のRectTransform (自動で子から検索します)")]
    private RectTransform backgroundRect;

    [SerializeField, Tooltip("しっぽのRectTransform (自動で子から検索します)")]
    private RectTransform tailRect;

    [Header("Tail Settings")]
    [SerializeField, Tooltip("しっぽの向きをキャラクターの方向へ自動で反転（Scale Xの反転）させるかどうか")]
    private bool autoFlipTail = true;

    [SerializeField, Tooltip("元のしっぽの画像が『右向き』の場合はチェック（通常『左向き』想定ならオフ）")]
    private bool isTailOriginallyFacingRight = false;

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

            // 3. キャラ被りを回避するため、左右どちらかにシフト
            if (avoidCharacter)
            {
                AvoidCharacterHorizontal(ref localPoint, originalLocalPoint);
            }

            // 4. UIが画面外に出ないようにクランプ (Clamping)
            if (enableClamping)
            {
                ClampToCanvas(ref localPoint);
            }

            // 5. 座標を適用
            targetBubble.localPosition = localPoint;

            // 6. しっぽの位置補正 (端に寄ったときにしっぽがキャラを指し示すように逆方向へシフト)
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

                // しっぽの向きをキャラクターの方へ自動で向ける (Scale.x の反転)
                if (autoFlipTail)
                {
                    // shiftX > 0 => 吹き出しは右側、キャラは左側 => しっぽは左を向くべき
                    // shiftX < 0 => 吹き出しは左側、キャラは右側 => しっぽは右を向くべき
                    float currentDirection = shiftX >= 0 ? 1f : -1f; // 1:左向き、-1:右向き (標準を左向きとした場合)
                    
                    if (isTailOriginallyFacingRight)
                    {
                        currentDirection *= -1f;
                    }

                    Vector3 tailScale = tailRect.localScale;
                    float targetScaleX = Mathf.Abs(tailScale.x) * currentDirection;
                    
                    if (Mathf.Abs(tailScale.x - targetScaleX) > 0.001f)
                    {
                        tailScale.x = targetScaleX;
                        tailRect.localScale = tailScale;
                    }
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

    /// <summary>
    /// キャラクター（頭部）と吹き出し本体が被らないように、左右へオフセットさせます。
    /// 右側に余裕があれば右、なければ左、どちらも厳しければ広い方へ配置します。
    /// </summary>
    private void AvoidCharacterHorizontal(ref Vector2 localPoint, Vector2 headPos)
    {
        Rect cRect = canvasRect.rect;
        Rect bRect = (backgroundRect != null) ? backgroundRect.rect : targetBubble.rect;
        Vector2 pivot = targetBubble.pivot;

        float scaledWidth = bRect.width * targetBubble.localScale.x;

        // 右側・左側それぞれのターゲットX座標
        float rightTargetX = headPos.x + (scaledWidth * pivot.x) + characterMarginX;
        float leftTargetX = headPos.x - (scaledWidth * (1f - pivot.x)) - characterMarginX;

        // 画面内に収めるための限界X座標
        float minX = cRect.min.x + (scaledWidth * pivot.x) + screenPadding.x;
        float maxX = cRect.max.x - (scaledWidth * (1f - pivot.x)) - screenPadding.x;

        if (rightTargetX <= maxX)
        {
            // 右側に収まる
            localPoint.x = rightTargetX;
        }
        else if (leftTargetX >= minX)
        {
            // 左側に収まる
            localPoint.x = leftTargetX;
        }
        else
        {
            // どちらも収まらない（画面幅が狭い、または吹き出しが大きい）場合
            // スペースが広い側に配置する
            float rightSpace = cRect.max.x - headPos.x;
            float leftSpace = headPos.x - cRect.min.x;
            if (rightSpace >= leftSpace)
            {
                localPoint.x = rightTargetX;
            }
            else
            {
                localPoint.x = leftTargetX;
            }
        }
    }
}
