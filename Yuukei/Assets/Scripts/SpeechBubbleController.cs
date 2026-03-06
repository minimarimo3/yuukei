// ==========================================================================
// SpeechBubbleController.cs
// YuukeiActionHandler からの吹き出し操作APIを提供する（§12.3）。
// ==========================================================================

using TMPro;
using UnityEngine;

/// <summary>
/// 吹き出しのテキスト設定・表示・非表示を制御するコンポーネント（§12.3）。
/// YuukeiActionHandler の ShowDialogueAsync から呼ばれる。
/// </summary>
public class SpeechBubbleController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI messageText;

    /// <summary>吹き出しのテキストを設定する。</summary>
    public void SetText(string text)
    {
        if (messageText != null)
            messageText.text = text;
    }

    /// <summary>吹き出しを表示する。</summary>
    public void Show()
    {
        gameObject.SetActive(true);
    }

    /// <summary>吹き出しを非表示にする。</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
