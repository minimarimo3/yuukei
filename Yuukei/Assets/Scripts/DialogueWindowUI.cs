using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshProを使用

public class DialogueWindowUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _tailImage;
    [SerializeField] private TextMeshProUGUI _messageText;

    // 動的に生成したスプライトの参照を保持（メモリ解放用）
    private Sprite _dynamicBgSprite;
    private Sprite _dynamicTailSprite;

    private void Start()
    {
        // テーマ変更イベントへの登録
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeLoaded += ApplyTheme;
            
            // ※起動時に既にテーマがロード済みの場合を考慮し、
            // 現在のテーマを即座に適用する処理をここに挟むのも推奨されます。
        }
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時のイベント解除とメモリ解放
        if (ThemeManager.Instance != null)
        {
            ThemeManager.Instance.OnThemeLoaded -= ApplyTheme;
        }
        CleanupDynamicAssets();
    }

    /// <summary>
    /// テーマデータを受信してUIに適用します。
    /// </summary>
    private void ApplyTheme(ThemeSettings theme, string dirPath)
    {
        var settings = theme.dialogueWindow;
        if (settings == null) return;

        // 古いテクスチャ/スプライトがメモリに残るのを防ぐ
        CleanupDynamicAssets();

        // 1. 背景画像のロードと9スライス適用
        if (!string.IsNullOrEmpty(settings.backgroundImage))
        {
            string bgPath = Path.Combine(dirPath, settings.backgroundImage);
            Texture2D bgTex = TextureLoader.LoadFromFile(bgPath);
            if (bgTex != null)
            {
                _dynamicBgSprite = SpriteFactory.Create9SliceSprite(bgTex, settings.backgroundBorder);
                _backgroundImage.sprite = _dynamicBgSprite;
                _backgroundImage.type = Image.Type.Sliced; // 確実に9スライスを有効化
            }
        }

        // 2. しっぽ画像のロード
        if (!string.IsNullOrEmpty(settings.tailImage))
        {
            string tailPath = Path.Combine(dirPath, settings.tailImage);
            Texture2D tailTex = TextureLoader.LoadFromFile(tailPath);
            if (tailTex != null)
            {
                // しっぽには9スライス不要なためボーダー設定はnullで生成
                _dynamicTailSprite = SpriteFactory.Create9SliceSprite(tailTex, null);
                _tailImage.sprite = _dynamicTailSprite;
                _tailImage.type = Image.Type.Simple;
                _tailImage.SetNativeSize(); // 画像本来のサイズにリセット
            }
        }

        // 3. テキスト領域のパディング適用
        if (settings.textPadding != null && _messageText != null)
        {
            UILayoutUtility.ApplyPadding(_messageText.rectTransform, settings.textPadding);
        }

        // 4. フォントスタイルの適用
        if (_messageText != null)
        {
            // HTMLカラーコード（"#333333" など）をUnityのColorに変換
            if (ColorUtility.TryParseHtmlString(settings.fontColorHtml, out Color fontColor))
            {
                _messageText.color = fontColor;
            }
            if (settings.fontSize > 0)
            {
                _messageText.fontSize = settings.fontSize;
            }
        }
    }

    /// <summary>
    /// 動的に生成したテクスチャとスプライトを明示的に破棄します。
    /// </summary>
    private void CleanupDynamicAssets()
    {
        if (_dynamicBgSprite != null)
        {
            if (_dynamicBgSprite.texture != null) Destroy(_dynamicBgSprite.texture);
            Destroy(_dynamicBgSprite);
            _dynamicBgSprite = null;
        }

        if (_dynamicTailSprite != null)
        {
            if (_dynamicTailSprite.texture != null) Destroy(_dynamicTailSprite.texture);
            Destroy(_dynamicTailSprite);
            _dynamicTailSprite = null;
        }
    }
}