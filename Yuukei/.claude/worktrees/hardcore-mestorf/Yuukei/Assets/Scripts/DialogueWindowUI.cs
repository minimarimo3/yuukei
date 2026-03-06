using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueWindowUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _tailImage;
    [SerializeField] private TextMeshProUGUI _messageText;

    // --- プレハブの初期状態（デフォルト値）のキャッシュ ---
    private Sprite _defaultBgSprite;
    private Sprite _defaultTailSprite;
    private Vector2 _defaultTextOffsetMin;
    private Vector2 _defaultTextOffsetMax;
    private Color _defaultFontColor;
    private float _defaultFontSize;

    // --- 動的生成スプライト（メモリ解放用） ---
    private Sprite _dynamicBgSprite;
    private Sprite _dynamicTailSprite;

    private void Awake()
    {
        // 起動時（プレハブの状態）の各種パラメータをデフォルトとして記憶
        if (_backgroundImage != null) _defaultBgSprite = _backgroundImage.sprite;
        if (_tailImage != null) _defaultTailSprite = _tailImage.sprite;
        
        if (_messageText != null)
        {
            _defaultTextOffsetMin = _messageText.rectTransform.offsetMin;
            _defaultTextOffsetMax = _messageText.rectTransform.offsetMax;
            _defaultFontColor = _messageText.color;
            _defaultFontSize = _messageText.fontSize;
        }
    }

    private void Start()
    {
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeLoaded += ApplyTheme;
        }
    }

    private void OnDestroy()
    {
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeLoaded -= ApplyTheme;
        }
        CleanupDynamicAssets();
    }

    private void ApplyTheme(ThemeSettings theme, string dirPath)
    {
        var settings = theme.dialogueWindow;
        if (settings == null) return;

        CleanupDynamicAssets(); // 以前の動的生成画像をメモリから消去

        // 1. 背景画像の適用とフォールバック
        string bgPath = !string.IsNullOrEmpty(settings.backgroundImage) ? Path.Combine(dirPath, settings.backgroundImage) : null;
        if (bgPath != null && File.Exists(bgPath))
        {
            Texture2D bgTex = TextureLoader.LoadFromFile(bgPath);
            _dynamicBgSprite = SpriteFactory.Create9SliceSprite(bgTex, settings.backgroundBorder);
            _backgroundImage.sprite = _dynamicBgSprite;
            _backgroundImage.type = Image.Type.Sliced;
        }
        else
        {
            _backgroundImage.sprite = _defaultBgSprite;
            _backgroundImage.type = Image.Type.Sliced;
        }

        // 2. しっぽ画像の適用とフォールバック
        string tailPath = !string.IsNullOrEmpty(settings.tailImage) ? Path.Combine(dirPath, settings.tailImage) : null;
        if (tailPath != null && File.Exists(tailPath))
        {
            Texture2D tailTex = TextureLoader.LoadFromFile(tailPath);
            _dynamicTailSprite = SpriteFactory.Create9SliceSprite(tailTex, null);
            _tailImage.sprite = _dynamicTailSprite;
            _tailImage.type = Image.Type.Simple;
            _tailImage.SetNativeSize();
        }
        else
        {
            _tailImage.sprite = _defaultTailSprite;
            _tailImage.type = Image.Type.Simple;
            _tailImage.SetNativeSize(); // デフォルト画像本来のサイズに戻す
        }

        // 3. テキスト領域のパディング適用とフォールバック
        if (_messageText != null)
        {
            if (settings.textPadding != null)
            {
                UILayoutUtility.ApplyPadding(_messageText.rectTransform, settings.textPadding);
            }
            else
            {
                // パディングの指定がない場合は、覚えている初期のRectに戻す
                _messageText.rectTransform.offsetMin = _defaultTextOffsetMin;
                _messageText.rectTransform.offsetMax = _defaultTextOffsetMax;
            }

            // 4. フォントカラーとサイズの適用とフォールバック
            if (!string.IsNullOrEmpty(settings.fontColorHtml) && ColorUtility.TryParseHtmlString(settings.fontColorHtml, out Color fontColor))
                _messageText.color = fontColor;
            else
                _messageText.color = _defaultFontColor;

            if (settings.fontSize > 0)
                _messageText.fontSize = settings.fontSize;
            else
                _messageText.fontSize = _defaultFontSize;
        }
    }

    private void CleanupDynamicAssets()
    {
        // _defaultBgSprite 等はUnityの管理下にあるため絶対にDestroyしないこと
        if (_dynamicBgSprite != null) { if (_dynamicBgSprite.texture != null) Destroy(_dynamicBgSprite.texture); Destroy(_dynamicBgSprite); _dynamicBgSprite = null; }
        if (_dynamicTailSprite != null) { if (_dynamicTailSprite.texture != null) Destroy(_dynamicTailSprite.texture); Destroy(_dynamicTailSprite); _dynamicTailSprite = null; }
    }
}