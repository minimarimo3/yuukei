using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PopupController : MonoBehaviour
{
    [Header("Main UI References")]
    [SerializeField] private RectTransform _windowRect;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _contentText;
    
    [Header("Close Button References")]
    [SerializeField] private Button _closeButton;
    [SerializeField] private Image _closeButtonImage;
    [SerializeField] private RectTransform _closeButtonRect;

    // 動的生成スプライトのキャッシュ（メモリ解放用）
    private Sprite _dynamicBgSprite;
    private Sprite _dynamicIconSprite;
    private Sprite _closeNormalSprite;
    private Sprite _closeHoverSprite;
    private Sprite _closePressedSprite;

    private void Awake()
    {
        _closeButton.onClick.AddListener(ClosePopup);
    }

    /// <summary>
    /// PopupManagerから呼ばれ、テーマと内容を適用します
    /// </summary>
    public void Setup(PopupThemeSettings theme, string message, string dirPath)
    {
        _contentText.text = message;

        // 1. 背景画像の適用と描画モードの切り替え
        if (!string.IsNullOrEmpty(theme.backgroundImage))
        {
            Texture2D bgTex = TextureLoader.LoadFromFile(Path.Combine(dirPath, theme.backgroundImage));
            if (bgTex != null)
            {
                if (theme.backgroundDrawMode == "Simple")
                {
                    _dynamicBgSprite = SpriteFactory.Create9SliceSprite(bgTex, null);
                    _backgroundImage.sprite = _dynamicBgSprite;
                    _backgroundImage.type = Image.Type.Simple;
                    _backgroundImage.preserveAspect = true;
                    // Nativeサイズに合わせる
                    _windowRect.sizeDelta = new Vector2(bgTex.width, bgTex.height);
                }
                else
                {
                    _dynamicBgSprite = SpriteFactory.Create9SliceSprite(bgTex, theme.backgroundBorder);
                    _backgroundImage.sprite = _dynamicBgSprite;
                    _backgroundImage.type = Image.Type.Sliced;
                }
            }
        }

        // 2. アイコンの設定（InfoやErrorなど）
        if (_iconImage != null)
        {
            if (!string.IsNullOrEmpty(theme.iconImage))
            {
                Texture2D iconTex = TextureLoader.LoadFromFile(Path.Combine(dirPath, theme.iconImage));
                if (iconTex != null)
                {
                    _dynamicIconSprite = SpriteFactory.Create9SliceSprite(iconTex, null);
                    _iconImage.sprite = _dynamicIconSprite;
                    _iconImage.gameObject.SetActive(true);
                }
            }
            else
            {
                _iconImage.gameObject.SetActive(false);
            }
        }

        // 3. パディングとフォントカラーの適用
        if (theme.contentPadding != null)
        {
            UILayoutUtility.ApplyPadding(_contentText.rectTransform, theme.contentPadding);
        }
        if (ColorUtility.TryParseHtmlString(theme.fontColorHtml, out Color fontColor))
        {
            _contentText.color = fontColor;
        }

        // 4. 閉じるボタンの設定
        if (theme.closeButton != null && _closeButton != null)
        {
            SetupCloseButton(theme.closeButton, dirPath);
        }
    }

    private void SetupCloseButton(CloseButtonSettings btnSettings, string dirPath)
    {
        // TransitionをSpriteSwapに強制
        _closeButton.transition = Selectable.Transition.SpriteSwap;

        // Normal画像 (Imageコンポーネント自体のSpriteに設定)
        if (!string.IsNullOrEmpty(btnSettings.normalImage))
        {
            Texture2D tex = TextureLoader.LoadFromFile(Path.Combine(dirPath, btnSettings.normalImage));
            if (tex != null)
            {
                _closeNormalSprite = SpriteFactory.Create9SliceSprite(tex, null);
                _closeButtonImage.sprite = _closeNormalSprite;
            }
        }

        // Hover / Pressed 画像 (SpriteStateに設定してButtonに渡す)
        SpriteState spriteState = new SpriteState();
        if (!string.IsNullOrEmpty(btnSettings.hoverImage))
        {
            Texture2D tex = TextureLoader.LoadFromFile(Path.Combine(dirPath, btnSettings.hoverImage));
            if (tex != null) _closeHoverSprite = SpriteFactory.Create9SliceSprite(tex, null);
            spriteState.highlightedSprite = _closeHoverSprite;
        }
        if (!string.IsNullOrEmpty(btnSettings.pressedImage))
        {
            Texture2D tex = TextureLoader.LoadFromFile(Path.Combine(dirPath, btnSettings.pressedImage));
            if (tex != null) _closePressedSprite = SpriteFactory.Create9SliceSprite(tex, null);
            spriteState.pressedSprite = _closePressedSprite;
        }
        _closeButton.spriteState = spriteState;

        // サイズと位置の適用 (右上Anchor)
        if (btnSettings.size != null)
            _closeButtonRect.sizeDelta = new Vector2(btnSettings.size.width, btnSettings.size.height);

        _closeButtonRect.anchorMin = new Vector2(1, 1);
        _closeButtonRect.anchorMax = new Vector2(1, 1);
        _closeButtonRect.pivot = new Vector2(1, 1);
        
        if (btnSettings.offset != null)
            _closeButtonRect.anchoredPosition = new Vector2(btnSettings.offset.x, btnSettings.offset.y);
    }

    private void ClosePopup()
    {
        // 将来的にフェードアウトなどのアニメーションを挟む場合はここを改修します
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        // ガベージコレクションに頼らず、アンマネージドなテクスチャメモリを確実に解放
        if (_dynamicBgSprite != null) { if (_dynamicBgSprite.texture != null) Destroy(_dynamicBgSprite.texture); Destroy(_dynamicBgSprite); }
        if (_dynamicIconSprite != null) { if (_dynamicIconSprite.texture != null) Destroy(_dynamicIconSprite.texture); Destroy(_dynamicIconSprite); }
        if (_closeNormalSprite != null) { if (_closeNormalSprite.texture != null) Destroy(_closeNormalSprite.texture); Destroy(_closeNormalSprite); }
        if (_closeHoverSprite != null) { if (_closeHoverSprite.texture != null) Destroy(_closeHoverSprite.texture); Destroy(_closeHoverSprite); }
        if (_closePressedSprite != null) { if (_closePressedSprite.texture != null) Destroy(_closePressedSprite.texture); Destroy(_closePressedSprite); }
    }
}