using UnityEngine;
using UnityEngine.EventSystems;

// IDragHandler, IBeginDragHandlerはUIのドラッグを検知するためのインターフェースです
public class WindowController : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    [Header("設定項目")]
    [Tooltip("動かす対象のウィンドウ全体（WindowBase）")]
    public RectTransform windowRectTransform;
    
    [Tooltip("最小化で隠すメインコンテンツ（MainContent）")]
    public GameObject mainContent;

    private Canvas canvas;

    private void Start()
    {
        // UIの移動スケールを正確にするため、親のCanvasを取得します
        canvas = GetComponentInParent<Canvas>();
    }

    // ドラッグを開始した瞬間に呼ばれる処理
    public void OnBeginDrag(PointerEventData eventData)
    {
        // ウィンドウをクリックした時に、他のUIよりも手前（最前面）に表示させます
        windowRectTransform.SetAsLastSibling();
    }

    // ドラッグ中に毎フレーム呼ばれる処理
    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null) return;

        // マウスの移動量（delta）をCanvasのスケールで割り、ウィンドウの位置に足し合わせます
        windowRectTransform.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    // 最小化ボタンを押した時に呼ばれる処理
    public void ToggleMinimize()
    {
        if (mainContent != null)
        {
            // mainContentの「表示/非表示」のON/OFFを反転させます
            mainContent.SetActive(!mainContent.activeSelf);
        }
    }
}