using UnityEngine;
using UnityEngine.UIElements;

public class PanelDragManipulator : PointerManipulator
{
    private readonly VisualElement _targetPanel;
    private bool _isDragging;
    private Vector2 _pointerStartPosition;
    private Vector2 _panelStartPosition;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="dragHandle">クリックを受け付ける要素（ヘッダー）</param>
    /// <param name="targetPanel">実際に動かす要素（ウィンドウ全体）</param>
    public PanelDragManipulator(VisualElement dragHandle, VisualElement targetPanel)
    {
        this.target = dragHandle; // PointerManipulatorのターゲット
        _targetPanel = targetPanel;
    }

    protected override void RegisterCallbacksOnTarget()
    {
        target.RegisterCallback<PointerDownEvent>(OnPointerDown);
        target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        target.RegisterCallback<PointerUpEvent>(OnPointerUp);
        target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    protected override void UnregisterCallbacksFromTarget()
    {
        target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
        target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
        target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
        target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
    }

    private void OnPointerDown(PointerDownEvent evt)
    {
        // 左クリックのみ反応させる
        if (_isDragging || evt.button != 0) return;

        _isDragging = true;
        _pointerStartPosition = evt.position;

        // Flexレイアウト下であっても、left/topを直接操作できるように
        // positionをAbsoluteに切り替える（初回のみ）
        if (_targetPanel.style.position != Position.Absolute)
        {
            _targetPanel.style.position = Position.Absolute;
            _targetPanel.style.left = _targetPanel.layout.x;
            _targetPanel.style.top = _targetPanel.layout.y;
        }

        _panelStartPosition = new Vector2(_targetPanel.layout.x, _targetPanel.layout.y);
        
        // ポインタをキャプチャし、カーソルが高速で動いてもイベントを取りこぼさないようにする
        target.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerMove(PointerMoveEvent evt)
    {
        if (!_isDragging || !target.HasPointerCapture(evt.pointerId)) return;

        Vector2 pointerDelta = new Vector2(evt.position.x, evt.position.y) - _pointerStartPosition;

        _targetPanel.style.left = _panelStartPosition.x + pointerDelta.x;
        _targetPanel.style.top = _panelStartPosition.y + pointerDelta.y;

        evt.StopPropagation();
    }

    private void OnPointerUp(PointerUpEvent evt)
    {
        if (!_isDragging || !target.HasPointerCapture(evt.pointerId)) return;

        _isDragging = false;
        target.ReleasePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnPointerCaptureOut(PointerCaptureOutEvent evt)
    {
        _isDragging = false;
    }
}