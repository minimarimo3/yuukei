using UnityEngine;

public class ObjectToBottomRight : MonoBehaviour
{
    [SerializeField] private float distanceFromCamera = 10f; // カメラからの奥行き
    [SerializeField] private Vector2 margin = new Vector2(1f, 1f); // 画面端からのワールド空間での余白

    private void Start()
    {
        PositionAtBottomRight();
    }

    private void PositionAtBottomRight()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Viewportの右下(X:1, Y:0)をワールド座標に変換
        // Z値にはカメラからの距離(nearClipPlaneより大きい値)を指定
        Vector3 viewportBottomRight = new Vector3(1f, 0f, distanceFromCamera);
        Vector3 worldBottomRight = cam.ViewportToWorldPoint(viewportBottomRight);

        // マージン分を内側にオフセットして配置
        Vector3 finalPosition = worldBottomRight + new Vector3(-margin.x, margin.y, 0f);
        
        transform.position = finalPosition;
    }
}