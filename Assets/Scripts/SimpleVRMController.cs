using UnityEngine;
using UniVRM10; // VRM 1.0用
using System.Threading.Tasks;
using System.Collections;

public class SimpleVRMController : MonoBehaviour
{
    private Vrm10Instance currentVrm;

    // 非同期でVRMをロードする
    public async Task LoadVRM(string path)
    {
        // すでにモデルがいたら消す
        if (currentVrm != null)
        {
            Destroy(currentVrm.gameObject);
        }

        try
        {
            Debug.Log($"VRMロード開始: {path}");
            // VRM 1.0 のロードメソッド
            currentVrm = await Vrm10.LoadPathAsync(path);
            Debug.Log("VRMロード完了！");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"VRMロード失敗...: {e}");
        }
    }

    // 指定された時間だけ口パクする
    public void Speak(string text)
    {
        if (currentVrm == null) return;

        // 文字数から喋る時間をざっくり計算（1文字0.15秒くらい）
        float duration = text.Length * 0.15f;
        
        // 前の口パクがあれば止めて、新しいのを開始
        StopAllCoroutines();
        StartCoroutine(LipSyncRoutine(duration));
    }

    // 口をパクパクさせるコルーチン
    private IEnumerator LipSyncRoutine(float duration)
    {
        // VRM 1.0 の表情制御（Expression）を取得
        var expression = currentVrm.Runtime.Expression;
        // 「あ(Aa)」の表情キーを作成
        var key = ExpressionKey.CreateFromPreset(ExpressionPreset.aa);

        float timer = 0;
        while (timer < duration)
        {
            // サイン波を使って口を開け閉めする（0.0 〜 1.0 の間を行き来）
            float weight = Mathf.Abs(Mathf.Sin(timer * 20f)); // 20fはパクパクする速さ
            
            // 表情の重みをセット
            expression.SetWeight(key, weight);

            timer += Time.deltaTime;
            yield return null;
        }

        // 終わったら口を閉じる
        expression.SetWeight(key, 0f);
    }
}