using System.Collections;
using System.Collections.Generic; // Queueを使用するために追加
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MascotChatController : MonoBehaviour
{
    [Header("UI参照")]
    public TextMeshProUGUI chatText;
    public Button nextButton;

    [Header("会話設定")]
    public float typingSpeed = 0.05f;

    private Coroutine typingCoroutine;
    private bool isTyping = false;
    private string currentFullText = "";
    
    // 追加：メッセージの順番待ちキュー
    private Queue<string> messageQueue = new Queue<string>();

    private void Start()
    {
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextButtonClicked);
        }
        
        // 起動時はテキストを空にしておく
        chatText.text = "";
    }

    /// <summary>
    /// 外部（EneScriptRunner等）からメッセージの表示を予約するメソッド
    /// </summary>
    public void EnqueueMessage(string message)
    {
        messageQueue.Enqueue(message);

        // 現在何も表示しておらず、待機状態であれば即座に再生を開始する
        if (!isTyping && chatText.text == currentFullText)
        {
            ShowNextMessage();
        }
    }

    private void ShowNextMessage()
    {
        if (messageQueue.Count > 0)
        {
            string nextMessage = messageQueue.Dequeue();
            
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
            }
            typingCoroutine = StartCoroutine(TypeText(nextMessage));
        }
        else
        {
            // キューが空になった（全セリフ終了）時の処理
            chatText.text = ""; // 最後にテキストをクリアする
        }
    }

    private IEnumerator TypeText(string message)
    {
        isTyping = true;
        currentFullText = message;
        chatText.text = "";

        foreach (char c in message)
        {
            chatText.text += c;
            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
    }

    private void OnNextButtonClicked()
    {
        if (isTyping)
        {
            // 文字送りの途中でクリックされた場合は、即座に全文字を表示
            if (typingCoroutine != null) StopCoroutine(typingCoroutine);
            chatText.text = currentFullText;
            isTyping = false;
        }
        else
        {
            // 全文字表示済みであれば、次のメッセージへ進む
            ShowNextMessage();
        }
    }
}