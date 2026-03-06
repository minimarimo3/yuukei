using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 吹き出しのテキスト量に応じて背景サイズを自動調整し、
/// 一定の長さを超えた場合は将来的にスクロールするための制御基盤スクリプト。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[ExecuteAlways]
public class SpeechBubbleAutoResizer : MonoBehaviour
{
    [Header("UI設定")]
    [SerializeField, Tooltip("表示するテキスト")]
    private TextMeshProUGUI _textComponent;

    [SerializeField, Tooltip("サイズ調整対象（背景）。未指定ならこのスクリプトがついたオブジェクト")]
    private RectTransform _backgroundRect;

    [Header("サイズ調整設定")]
    [SerializeField, Tooltip("テキストの上の余白（パディング）")]
    private float _paddingTop = 20f;

    [SerializeField, Tooltip("テキストの下の余白（パディング）")]
    private float _paddingBottom = 20f;

    [SerializeField, Tooltip("吹き出しの最小の高さ")]
    private float _minHeight = 100f;

    [SerializeField, Tooltip("吹き出しの最大の高さ（これを超えたらスクロールで対処）")]
    private float _maxHeight = 300f;

    [Header("UI連携（オプション）")]
    [SerializeField, Tooltip("吹き出しのしっぽ（Tail）。背景が伸びた際に一緒に下へ移動させたい場合に指定")]
    private RectTransform _tailRect;

    [Header("スクロール対応（将来用・今回は未設定でOKです）")]
    [SerializeField, Tooltip("テキストが長すぎる場合にスクロールさせるためのScrollRect")]
    private ScrollRect _scrollRect;

    private float _initialTailLocalY;
    private float _initialBackgroundHeight;

    private void Reset()
    {
        _backgroundRect = GetComponent<RectTransform>();
        _textComponent = GetComponentInChildren<TextMeshProUGUI>();
        _scrollRect = GetComponentInChildren<ScrollRect>();
        if (transform.parent != null)
        {
            var tail = transform.parent.Find("Tail");
            if (tail != null) _tailRect = tail.GetComponent<RectTransform>();
        }
    }

    private void Start()
    {
        // 最初のしっぽの「初期位置（Y）」と「初期の背景の高さ」を記憶しておく
        if (_tailRect != null && _backgroundRect != null)
        {
            _initialTailLocalY = _tailRect.localPosition.y;
            _initialBackgroundHeight = _backgroundRect.rect.height;
        }
    }

    private void LateUpdate()
    {
        if (_textComponent == null) return;
        if (_backgroundRect == null) _backgroundRect = GetComponent<RectTransform>();

        // 1. テキストの理想的な高さを取得（現在の横幅で改行された場合の高さ）
        float textPreferredHeight = _textComponent.preferredHeight;

        // 2. 背景の目標高さを計算（最大・最小サイズでクランプ）
        float targetHeight = textPreferredHeight + _paddingTop + _paddingBottom;
        float clampedHeight = Mathf.Clamp(targetHeight, _minHeight, _maxHeight);

        // 3. 背景の高さを更新（横幅はそのまま維持）
        if (Mathf.Abs(_backgroundRect.sizeDelta.y - clampedHeight) > 0.01f)
        {
            _backgroundRect.sizeDelta = new Vector2(_backgroundRect.sizeDelta.x, clampedHeight);
        }

        // 4. テキストの縦位置を背景の上端（Top）に自動的に合わせる処理
        if (_scrollRect == null)
        {
            RectTransform textRect = _textComponent.rectTransform;
            if (textRect != null)
            {
                // Y軸のアンカーとピボットを強制的に「上（Top = 1.0）」に固定する
                if (textRect.anchorMin.y != 1f || textRect.anchorMax.y != 1f || textRect.pivot.y != 1f)
                {
                    textRect.anchorMin = new Vector2(textRect.anchorMin.x, 1f);
                    textRect.anchorMax = new Vector2(textRect.anchorMax.x, 1f);
                    textRect.pivot = new Vector2(textRect.pivot.x, 1f);
                }

                // テキスト枠自身の高さを理想的な高さに合わせる
                if (Mathf.Abs(textRect.sizeDelta.y - textPreferredHeight) > 0.01f)
                {
                    textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textPreferredHeight);
                }

                // 背景の上端から _paddingTop 分だけ下にずらした位置を維持する
                if (Mathf.Abs(textRect.anchoredPosition.y - (-_paddingTop)) > 0.01f)
                {
                    textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, -_paddingTop);
                }
            }
        }
        else
        {
            // --- 将来的なスクロールバー対応設計 ---
            
            // 1. ScrollView自体の位置とサイズを背景に合わせる
            RectTransform scrollRectTransform = _scrollRect.GetComponent<RectTransform>();
            if (scrollRectTransform != null)
            {
                // ScrollViewを上揃えに固定
                if (scrollRectTransform.anchorMin.y != 1f || scrollRectTransform.anchorMax.y != 1f || scrollRectTransform.pivot.y != 1f)
                {
                    scrollRectTransform.anchorMin = new Vector2(scrollRectTransform.anchorMin.x, 1f);
                    scrollRectTransform.anchorMax = new Vector2(scrollRectTransform.anchorMax.x, 1f);
                    scrollRectTransform.pivot = new Vector2(scrollRectTransform.pivot.x, 1f);
                }

                // ScrollViewの高さを「背景の枠 − 上下パディング」に制限
                float scrollHeight = clampedHeight - _paddingTop - _paddingBottom;
                if (Mathf.Abs(scrollRectTransform.sizeDelta.y - scrollHeight) > 0.01f)
                {
                    scrollRectTransform.sizeDelta = new Vector2(scrollRectTransform.sizeDelta.x, scrollHeight);
                }

                // ScrollViewのY位置を上パディングの分だけ下げる
                if (Mathf.Abs(scrollRectTransform.anchoredPosition.y - (-_paddingTop)) > 0.01f)
                {
                    scrollRectTransform.anchoredPosition = new Vector2(scrollRectTransform.anchoredPosition.x, -_paddingTop);
                }
            }
            
            // 2. ContentとTextの高さをテキストの理想的な高さに合わせる
            RectTransform contentRect = _scrollRect.content;
            if (contentRect != null)
            {
                // Contentを上揃えに固定（スクロールの起点）
                if (contentRect.anchorMin.y != 1f || contentRect.anchorMax.y != 1f || contentRect.pivot.y != 1f)
                {
                    contentRect.anchorMin = new Vector2(contentRect.anchorMin.x, 1f);
                    contentRect.anchorMax = new Vector2(contentRect.anchorMax.x, 1f);
                    contentRect.pivot = new Vector2(contentRect.pivot.x, 1f);
                }

                if (Mathf.Abs(contentRect.sizeDelta.y - textPreferredHeight) > 0.01f)
                {
                    contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, textPreferredHeight);
                }
            }

            RectTransform textRect = _textComponent.rectTransform;
            if (textRect != null)
            {
                // Textを上揃えに固定し、Contentの一番上に配置
                if (textRect.anchorMin.y != 1f || textRect.anchorMax.y != 1f || textRect.pivot.y != 1f)
                {
                    textRect.anchorMin = new Vector2(textRect.anchorMin.x, 1f);
                    textRect.anchorMax = new Vector2(textRect.anchorMax.x, 1f);
                    textRect.pivot = new Vector2(textRect.pivot.x, 1f);
                    textRect.anchoredPosition = new Vector2(textRect.anchoredPosition.x, 0f);
                }

                if (Mathf.Abs(textRect.sizeDelta.y - textPreferredHeight) > 0.01f)
                {
                    textRect.sizeDelta = new Vector2(textRect.sizeDelta.x, textPreferredHeight);
                }
            }

            // 3. 最大高さを超えたときだけ縦スクロールを有効にする
            bool isOverMaxHeight = targetHeight > _maxHeight;
            _scrollRect.vertical = isOverMaxHeight;
        }

        // 5. Tail（しっぽ）の連動移動処理
        if (_tailRect != null && _initialBackgroundHeight > 0f)
        {
            // 背景が初期状態からどれだけ上下に伸びたか（差分）の「半分」だけ下に移動させる
            // Pivotが通常Center(0.5)と仮定したとき、高さが伸びると下端は「伸びた分の半分」だけ下がるため
            float heightDiff = clampedHeight - _initialBackgroundHeight;
            float targetTailY = _initialTailLocalY - (heightDiff * 0.5f);
            
            if (Mathf.Abs(_tailRect.localPosition.y - targetTailY) > 0.01f)
            {
                _tailRect.localPosition = new Vector3(_tailRect.localPosition.x, targetTailY, _tailRect.localPosition.z);
            }
        }
    }
}
