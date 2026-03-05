using UnityEngine;

public class ObjectToBottomRight : MonoBehaviour
{
    [SerializeField] private float distanceFromCamera = 10f; // カメラからの奥行き
    [SerializeField] private Vector2 margin = new Vector2(1f, 1f); // 画面端からのワールド空間での余白

    private void Start()
    {
        PositionAtBottomRight();
    }


    /// <summary>
    /// 画面右下に再配置する。キャラクターのロード完了後などに外部から呼ぶ。
    /// </summary>
    [ContextMenu("Position At Bottom Right")]
    public void PositionAtBottomRight()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Viewportの右下(X:1, Y:0)をワールド座標に変換
        // Z値にはカメラからの距離(nearClipPlaneより大きい値)を指定
        Vector3 viewportBottomRight = new Vector3(1f, 0f, distanceFromCamera);
        Vector3 worldBottomRight = cam.ViewportToWorldPoint(viewportBottomRight);

        // 【重要】親（自身）のスケールを考慮してマージンを調整する
        // スケールが 2.3 倍なら、マージンも 2.3 倍しないと見た目上の位置がズレるため
        float currentScale = transform.localScale.x; 
        Vector3 adjustedMargin = new Vector3(margin.x * currentScale, margin.y * currentScale, 0f);

        // マージン分を内側にオフセットして配置
        Vector3 finalPosition = worldBottomRight + new Vector3(-adjustedMargin.x, adjustedMargin.y, 0f);
        
        transform.position = finalPosition;
    }
}