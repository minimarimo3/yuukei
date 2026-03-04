using UnityEngine;
using UnityEngine.UIElements;

public class PanelInitialPositioner : MonoBehaviour
{
    private VisualElement _targetPanel;

    private void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        _targetPanel = root.Q<VisualElement>("RootContainer"); // 対象のパネル名

        // レイアウト（サイズ）が確定した瞬間に位置を計算する
        _targetPanel.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
    }

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // 1回実行したら解除
        // _targetPanel.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

        // 仮想スクリーン全体の解像度は Screen.width / Screen.height に格納されている
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        _targetPanel.style.position = Position.Absolute;
        
        // 画面の右下へ配置（必要に応じてマージンを引く）
        _targetPanel.style.left = screenWidth - _targetPanel.layout.width;
        _targetPanel.style.top = screenHeight - _targetPanel.layout.height;
    }
}