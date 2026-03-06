using System;
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

    [Header("Confirm/Cancel Buttons (Optional)")]
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;

    // 動的生成スプライトのキャッシュ（メモリ解放用）
    private Sprite _dynamicBgSprite;
    private Sprite _dynamicIconSprite;
    private Sprite _closeNormalSprite;
    private Sprite _closeHoverSprite;
    private Sprite _closePressedSprite;

    private Action _onConfirm;
    private Action _onCancel;

    private void Awake()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(ClosePopup);
        }
        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(OnConfirmClicked);
        }
        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(OnCancelClicked);
        }
    }

    public void Setup(PopupThemeSettings theme, string message, string dirPath)
    {
        if (_contentText != null) _contentText.text = message;
        if (theme == null) return; // テーマデータが丸ごと無ければプレハブのまま表示

        // 1. 背景画像の適用 (指定がなければプレハブ画像のまま)
        if (!string.IsNullOrEmpty(theme.backgroundImage))
        {
            string bgPath = Path.Combine(dirPath, theme.backgroundImage);
            if (File.Exists(bgPath))
            {
                Texture2D bgTex = TextureLoader.LoadFromFile(bgPath);
                if (bgTex != null)
                {
                    if (theme.backgroundDrawMode == "Simple")
                    {
                        _dynamicBgSprite = SpriteFactory.Create9SliceSprite(bgTex, null);
                        _backgroundImage.sprite = _dynamicBgSprite;
                        _backgroundImage.type = Image.Type.Simple;
                        _backgroundImage.preserveAspect = true;
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
        }

        // 2. アイコンの設定
        if (_iconImage != null && !string.IsNullOrEmpty(theme.iconImage))
        {
            string iconPath = Path.Combine(dirPath, theme.iconImage);
            if (File.Exists(iconPath))
            {
                Texture2D iconTex = TextureLoader.LoadFromFile(iconPath);
                if (iconTex != null)
                {
                    _dynamicIconSprite = SpriteFactory.Create9SliceSprite(iconTex, null);
                    _iconImage.sprite = _dynamicIconSprite;
                    _iconImage.gameObject.SetActive(true);
                }
            }
        }
        // JSONでアイコン名が明示的に空文字指定されている場合は非表示にする
        else if (theme.iconImage == "") 
        {
            if (_iconImage != null) _iconImage.gameObject.SetActive(false);
        }

        // 3. パディングとフォントカラーの適用
        if (theme.contentPadding != null && _contentText != null)
        {
            UILayoutUtility.ApplyPadding(_contentText.rectTransform, theme.contentPadding);
        }
        if (!string.IsNullOrEmpty(theme.fontColorHtml) && _contentText != null)
        {
            if (ColorUtility.TryParseHtmlString(theme.fontColorHtml, out Color fontColor))
            {
                _contentText.color = fontColor;
            }
        }

        // 4. 閉じるボタンの設定
        if (theme.closeButton != null && _closeButton != null)
        {
            SetupCloseButton(theme.closeButton, dirPath);
        }
    }

    /// <summary>テーマなしで最小限のメッセージ表示を行う（§7.5セキュリティ警告用）。</summary>
    public void SetupSimple(string message)
    {
        if (_contentText != null) _contentText.text = message;
    }

    /// <summary>確認・キャンセルコールバックを設定する（§7.5参照）。</summary>
    public void SetConfirmCallbacks(Action onConfirm, Action onCancel)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;
    }

    private void SetupCloseButton(CloseButtonSettings btnSettings, string dirPath)
    {
        _closeButton.transition = Selectable.Transition.SpriteSwap;

        // Normal画像
        if (!string.IsNullOrEmpty(btnSettings.normalImage))
        {
            string path = Path.Combine(dirPath, btnSettings.normalImage);
            if (File.Exists(path))
            {
                Texture2D tex = TextureLoader.LoadFromFile(path);
                if (tex != null)
                {
                    _closeNormalSprite = SpriteFactory.Create9SliceSprite(tex, null);
                    _closeButtonImage.sprite = _closeNormalSprite;
                }
            }
        }

        // Hover / Pressed 画像
        SpriteState spriteState = _closeButton.spriteState; // 現在の状態（プレハブの状態）をベースにする
        if (!string.IsNullOrEmpty(btnSettings.hoverImage))
        {
            string path = Path.Combine(dirPath, btnSettings.hoverImage);
            if (File.Exists(path))
            {
                Texture2D tex = TextureLoader.LoadFromFile(path);
                if (tex != null) _closeHoverSprite = SpriteFactory.Create9SliceSprite(tex, null);
                spriteState.highlightedSprite = _closeHoverSprite;
            }
        }
        if (!string.IsNullOrEmpty(btnSettings.pressedImage))
        {
            string path = Path.Combine(dirPath, btnSettings.pressedImage);
            if (File.Exists(path))
            {
                Texture2D tex = TextureLoader.LoadFromFile(path);
                if (tex != null) _closePressedSprite = SpriteFactory.Create9SliceSprite(tex, null);
                spriteState.pressedSprite = _closePressedSprite;
            }
        }
        _closeButton.spriteState = spriteState;

        // サイズと位置の適用
        if (btnSettings.size != null && btnSettings.size.width > 0)
            _closeButtonRect.sizeDelta = new Vector2(btnSettings.size.width, btnSettings.size.height);

        _closeButtonRect.anchorMin = new Vector2(1, 1);
        _closeButtonRect.anchorMax = new Vector2(1, 1);
        _closeButtonRect.pivot = new Vector2(1, 1);
        
        if (btnSettings.offset != null)
            _closeButtonRect.anchoredPosition = new Vector2(btnSettings.offset.x, btnSettings.offset.y);
    }

    private void OnConfirmClicked()
    {
        _onConfirm?.Invoke();
        Destroy(gameObject);
    }

    private void OnCancelClicked()
    {
        _onCancel?.Invoke();
        Destroy(gameObject);
    }

    private void ClosePopup()
    {
        // 閉じるボタンはキャンセルとして扱う
        _onCancel?.Invoke();
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_dynamicBgSprite != null) { if (_dynamicBgSprite.texture != null) Destroy(_dynamicBgSprite.texture); Destroy(_dynamicBgSprite); }
        if (_dynamicIconSprite != null) { if (_dynamicIconSprite.texture != null) Destroy(_dynamicIconSprite.texture); Destroy(_dynamicIconSprite); }
        if (_closeNormalSprite != null) { if (_closeNormalSprite.texture != null) Destroy(_closeNormalSprite.texture); Destroy(_closeNormalSprite); }
        if (_closeHoverSprite != null) { if (_closeHoverSprite.texture != null) Destroy(_closeHoverSprite.texture); Destroy(_closeHoverSprite); }
        if (_closePressedSprite != null) { if (_closePressedSprite.texture != null) Destroy(_closePressedSprite.texture); Destroy(_closePressedSprite); }
    }
}