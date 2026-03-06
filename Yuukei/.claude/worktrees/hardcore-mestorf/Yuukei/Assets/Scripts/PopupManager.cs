using UnityEngine;

public enum PopupType
{
    General,
    Info,
    Warning,
    Error,
    Glitch
}

public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] private GameObject _popupPrefab; // PopupControllerがアタッチされたプレハブ
    [SerializeField] private RectTransform _popupContainer; // キャンバス内のポップアップ生成親オブジェクト

    private PopupSettingsCollection _currentPopupsTheme;
    private string _currentThemeDirPath;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // テーマロードのイベントをリッスン
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeLoaded += CachePopupTheme;
        }
    }

    private void OnDestroy()
    {
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeLoaded -= CachePopupTheme;
        }
    }

    private void CachePopupTheme(ThemeSettings theme, string dirPath)
    {
        _currentPopupsTheme = theme.popups;
        _currentThemeDirPath = dirPath;
    }

    /// <summary>
    /// 指定されたタイプとメッセージでポップアップを生成・表示します。
    /// 例: PopupManager.Instance.ShowPopup(PopupType.Warning, "バッテリー残量が低下しています");
    /// </summary>
    public void ShowPopup(PopupType type, string message)
    {
        if (_popupPrefab == null || _currentPopupsTheme == null || string.IsNullOrEmpty(_currentThemeDirPath))
        {
            Debug.LogWarning("ポップアップのプレハブ、またはテーマが未設定です。");
            return;
        }

        // タイプに応じて使用する設定を振り分け
        PopupThemeSettings settings = type switch
        {
            PopupType.Info => _currentPopupsTheme.info,
            PopupType.Warning => _currentPopupsTheme.warning,
            PopupType.Error => _currentPopupsTheme.error,
            PopupType.Glitch => _currentPopupsTheme.glitch,
            _ => _currentPopupsTheme.general
        };

        if (settings == null)
        {
            Debug.LogWarning($"{type} のテーマ設定がJSONに存在しません。");
            return;
        }

        // プレハブを生成して初期化
        GameObject popupObj = Instantiate(_popupPrefab, _popupContainer);
        PopupController controller = popupObj.GetComponent<PopupController>();
        
        if (controller != null)
        {
            controller.Setup(settings, message, _currentThemeDirPath);

            // TODO: Glitchタイプの場合、必要であればここでImageのマテリアルを
            // グリッチシェーダー付きのものに差し替える等の特殊演出を追加できます。
        }
    }
}