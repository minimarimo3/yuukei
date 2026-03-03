using UnityEngine;
using UnityEngine.UIElements;
using Kirurobo;

[RequireComponent(typeof(UIDocument))]
public class SettingsUIManager : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _rootContainer;
    private VisualElement _settingsPanel;
    private VisualElement _dragHandle;
    private Button _closeButton;
    private ScrollView _contentArea;

    // TODO: ご使用のUniWindowControllerのコンポーネントを参照してください
    [SerializeField] private UniWindowController _windowController;

    private PanelDragManipulator _dragManipulator;

    public VisualElement ContentArea => _contentArea;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        _rootContainer = _uiDocument.rootVisualElement.Q<VisualElement>("RootContainer");
        _settingsPanel = _uiDocument.rootVisualElement.Q<VisualElement>("SettingsPanel");
        _dragHandle = _uiDocument.rootVisualElement.Q<VisualElement>("DragHandle");
        _closeButton = _uiDocument.rootVisualElement.Q<Button>("CloseButton");
        _contentArea = _uiDocument.rootVisualElement.Q<ScrollView>("ContentArea");

        _closeButton.clicked += HideSettings;

        // ドラッグ操作のバインド
        _dragManipulator = new PanelDragManipulator(_dragHandle, _settingsPanel);

        HideSettings();
    }

    public void ShowSettings()
    {
        _rootContainer.style.display = DisplayStyle.Flex;
    }

    public void HideSettings()
    {
        _rootContainer.style.display = DisplayStyle.None;
    }
}
