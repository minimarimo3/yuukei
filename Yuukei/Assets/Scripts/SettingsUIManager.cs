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

    private bool _isPositionInitialized = false;

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

        if (_closeButton != null) _closeButton.clicked += HideSettings;
        if (_dragHandle != null && _settingsPanel != null)
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

    private void OnGeometryChanged(GeometryChangedEvent evt)
    {
        // 1回実行したら解除し、フラグを立てる
        _settingsPanel.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        _isPositionInitialized = true;

        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        // Flexboxの中央揃え制約から切り離す
        _settingsPanel.style.position = Position.Absolute;
        
        // SettingsPanel のサイズを基準に右下へ配置
        // ※画面端にぴったりくっつくと見栄えが悪いため、少しマージン(-20など)を取るのが一般的です
        _settingsPanel.style.left = screenWidth - _settingsPanel.layout.width - 20;
        _settingsPanel.style.top = screenHeight - _settingsPanel.layout.height - 40; // Windowsタスクバーを考慮して下部は少し多めに確保
    }

    public void ShowSettings()
    {
        if (_rootContainer == null) return;
        _rootContainer.style.display = DisplayStyle.Flex;
        if (!_isPositionInitialized) {
            _settingsPanel.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
        // 開いたときは常に「一般設定」を表示する
        SwitchPage("Tab_General", _tabTitleMap["Tab_General"]);
    }

    /// <summary>
    /// 設定画面を開き、キャラクタータブを直接表示する
    /// </summary>
    public void ShowCharacterTab()
    {
        if (_rootContainer == null) return;
        _rootContainer.style.display = DisplayStyle.Flex;
        if (!_isPositionInitialized) {
            _settingsPanel.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }
        SwitchPage("Tab_Character", _tabTitleMap["Tab_Character"]);
    }

    public void HideSettings()
    {
        if (_rootContainer == null) return;
        _rootContainer.style.display = DisplayStyle.None;
    }

private void SwitchPage(string tabId, string pageTitle)
    {
        _pageTitle.text = pageTitle;
        _activePageContent.Clear();

        if (tabId == "Tab_Character")
        {
            RenderCharacterTab();
        }
        else
        {
            // 他のタブのプレースホルダー
            Label infoLabel = new Label($"現在のページ: {pageTitle}\n\n※このページの内容は現在準備中です。");
            infoLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            infoLabel.style.marginTop = 10;
            _activePageContent.Add(infoLabel);
        }
    }

    // キャラクタータブのUI構築ロジック
    private void RenderCharacterTab()
    {
        _activePageContent.Clear();

        // 1. キャラ追加ボタンの生成
        Button addButton = new Button(() => 
        {
            // Kirurobo.FilePanel の設定を構築
            Kirurobo.FilePanel.Settings fileSettings = new Kirurobo.FilePanel.Settings();
            fileSettings.title = "キャラクターモデルを選択";
            fileSettings.filters = new Kirurobo.FilePanel.Filter[] 
            {
                new Kirurobo.FilePanel.Filter("モデルファイル (*.vrm, *.bundle)", "vrm", "bundle"),
                new Kirurobo.FilePanel.Filter("すべてのファイル (*.*)", "*")
            };
            
            // 既存ファイルのみ選択可能 ＆ 複数選択を許可
            fileSettings.flags = Kirurobo.FilePanel.Flag.FileMustExist | Kirurobo.FilePanel.Flag.AllowMultipleSelection;

            // ダイアログを展開し、コールバックでファイルを受け取る
            Kirurobo.FilePanel.OpenFilePanel(fileSettings, (files) => 
            {
                if (files == null || files.Length == 0) return;

                foreach (string path in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                    ConfigManager.Instance.AddCharacter(fileName, path);
                }

                // データの追加が完了したら、現在のタブを再描画してリストを更新する
                RenderCharacterTab();
            });
        });
        addButton.text = "+ キャラクターを追加 (ファイルを選択)";
        addButton.style.height = 30;
        addButton.style.marginBottom = 15;
        addButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.6f, 0.2f)); // 緑色
        addButton.style.color = new StyleColor(Color.white);
        _activePageContent.Add(addButton);

        // 2. 登録済みキャラクターの一覧表示
        var settings = ConfigManager.Instance.Settings;
        if (settings.savedCharacters.Count == 0)
        {
            _activePageContent.Add(new Label("登録されているキャラクターがいません。"));
            return;
        }

        foreach (var character in settings.savedCharacters)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = 5;
            row.style.paddingBottom = 5;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));

            // 現在選択中のキャラならハイライトする
            bool isCurrent = (character.id == settings.currentCharacterId);
            
            Label nameLabel = new Label(isCurrent ? $"★ {character.name}" : character.name);
            nameLabel.style.flexGrow = 1;
            nameLabel.style.color = isCurrent ? new StyleColor(Color.yellow) : new StyleColor(Color.white);
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            Button selectButton = new Button(() => 
            {
                ConfigManager.Instance.SetCurrentCharacter(character.id);
                // UIを再描画してハイライトを更新
                RenderCharacterTab(); 
            });
            selectButton.text = "選択";
            selectButton.SetEnabled(!isCurrent); // 選択済みの場合はボタンを無効化

            row.Add(nameLabel);
            row.Add(selectButton);
            _activePageContent.Add(row);
        }
    }

}