using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SettingsUIManager : MonoBehaviour
{
    private UIDocument _uiDocument;
    private VisualElement _rootContainer;
    private VisualElement _settingsPanel;
    private VisualElement _dragHandle;
    private Button _closeButton;
    
    // コンテンツエリア
    private Label _pageTitle;
    private ScrollView _activePageContent;
    private Label _placeholderText;

    private PanelDragManipulator _dragManipulator;

    // タブ名とページタイトルのマッピング
    private readonly Dictionary<string, string> _tabTitleMap = new Dictionary<string, string>()
    {
        { "Tab_General", "一般設定" },
        { "Tab_Character", "キャラクターの選択" },
        { "Tab_Script", "台本の選択" },
        { "Tab_Prop", "小物（オブジェクト）の選択" },
        { "Tab_Theme", "ウィンドウ・テーマの選択" },
        { "Tab_Mod", "MOD（プラグイン）の管理" },
        { "Tab_Package", "パッケージの管理" },
        { "Tab_Market", "マーケットプレイス" },
        { "Tab_About", "アプリについて (About)" }
    };

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();
        var root = _uiDocument.rootVisualElement;

        _rootContainer = root.Q<VisualElement>("RootContainer");
        _settingsPanel = root.Q<VisualElement>("SettingsPanel");
        _dragHandle = root.Q<VisualElement>("DragHandle");
        _closeButton = root.Q<Button>("CloseButton");
        
        _pageTitle = root.Q<Label>("PageTitle");
        _activePageContent = root.Q<ScrollView>("ActivePageContent");
        _placeholderText = root.Q<Label>("PlaceholderText");

        _closeButton.clicked += HideSettings;
        _dragManipulator = new PanelDragManipulator(_dragHandle, _settingsPanel);

        // タブボタンのイベント登録
        foreach (var kvp in _tabTitleMap)
        {
            var tabButton = root.Q<Button>(kvp.Key);
            if (tabButton != null)
            {
                // クリックされたら SwitchPage メソッドを呼ぶ
                tabButton.clicked += () => SwitchPage(kvp.Key, kvp.Value);
            }
        }

        HideSettings();
    }

    public void ShowSettings()
    {
        _rootContainer.style.display = DisplayStyle.Flex;
        // 開いたときは常に「一般設定」を表示する
        SwitchPage("Tab_General", _tabTitleMap["Tab_General"]);
    }

    public void HideSettings()
    {
        _rootContainer.style.display = DisplayStyle.None;
    }

    private void SwitchPage(string tabId, string pageTitle)
    {
        // タイトルを更新
        _pageTitle.text = pageTitle;

        // 一旦中身をクリアする（将来的に動的生成したUI要素を入れるため）
        _activePageContent.Clear();

        // TODO: ここで選択されたタブに応じた固有のUI（UXML）をロードしてアタッチする処理を書く
        // 今回はプレースホルダーのテキストを書き換えて追加しておく
        Label infoLabel = new Label($"現在のページ: {pageTitle}\n\n※このページの内容は現在準備中です。");
        infoLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
        infoLabel.style.marginTop = 10;
        
        _activePageContent.Add(infoLabel);
    }
}