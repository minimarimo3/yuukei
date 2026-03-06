using UnityEngine;

public class ObjectToBottomRight : MonoBehaviour
{
    [SerializeField] private float distanceFromCamera = 10f; // カメラからの奥行き
    [SerializeField] private Vector2 margin = new Vector2(1f, 1f); // 画面端からのワールド空間での余白

    private void Start()
    {
        // 起動時の初期化は BootSequenceCoordinator(PlatformBootstrapper) に委譲
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

        // まずZ座標を目的の奥行きに合わせる
        Vector3 pos = transform.position;
        pos.z = worldBottomRight.z;
        transform.position = pos;

        // 画面右下からマージン分内側に入ったワールド座標の目標位置
        Vector3 targetPos = worldBottomRight + new Vector3(-margin.x, margin.y, 0f);

        // 全ての子RendererからBounds（AABB）を計算して、見た目の右下座標を取得する
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // 現在のBoundsの右下 (XY平面上)
            Vector3 currentBottomRight = new Vector3(bounds.max.x, bounds.min.y, pos.z);

            // Boundsの右下が目標位置に一致するように移動
            Vector3 shift = targetPos - currentBottomRight;
            shift.z = 0; // Zはすでに合わせているので0
            
            transform.position += shift;
        }
        else
        {
            // Rendererが無い場合はオブジェクトの原点を使って配置
            transform.position = targetPos;
        }
    }
}