using UnityEngine;
using UniVRM10;
using System.Threading.Tasks;
using System.Collections;

public class SimpleVRMController : MonoBehaviour
{
    private Vrm10Instance currentVrm;

    [Header("ダイブ時に発生させるパーティクルのプレハブ")]
    public ParticleSystem digitalNoisePrefab;

    [Header("スキャナー（ロード時にターゲットを更新するため）")]
    public ExplorerItemScanner explorerScanner;

    public async Task LoadVRM(string path)
    {
        if (currentVrm != null)
        {
            Destroy(currentVrm.gameObject);
        }

        try
        {
            Debug.Log($"VRMロード開始: {path}");
            currentVrm = await Vrm10.LoadPathAsync(path);
            Debug.Log("VRMロード完了！");

            // --- 追加実装：ロード完了後の動的セットアップ ---
            SetupCyberDive(currentVrm.gameObject);

        }
        catch (System.Exception e)
        {
            Debug.LogError($"VRMロード失敗...: {e}");
        }
    }

    private void SetupCyberDive(GameObject vrmObject)
    {
        // 1. スクリプトを動的に追加
        var diveController = vrmObject.AddComponent<CyberDiveController>();

        // 2. パーティクルプレハブが設定されていれば生成して割り当て
        if (digitalNoisePrefab != null)
        {
            // VRMの子オブジェクトとして生成
            var particle = Instantiate(digitalNoisePrefab, vrmObject.transform);
            
            // キャラクターの中心（腰のあたりなど）にエフェクトを配置
            particle.transform.localPosition = new Vector3(0, 1.0f, 0); 
            
            diveController.digitalNoiseParticle = particle;
        }

        // 3. スキャナーのターゲットをこのVRMに設定
        if (explorerScanner != null)
        {
            explorerScanner.targetObject = vrmObject.transform;
        }
    }

    // 指定された時間だけ口パクする
    public void Speak(string text)
    {
        // 既存のコードそのまま
        if (currentVrm == null) return;
        float duration = text.Length * 0.15f;
        StopAllCoroutines();
        StartCoroutine(LipSyncRoutine(duration));
    }

    private IEnumerator LipSyncRoutine(float duration)
    {
        // 既存のコードそのまま
        var expression = currentVrm.Runtime.Expression;
        var key = ExpressionKey.CreateFromPreset(ExpressionPreset.aa);
        float timer = 0;
        while (timer < duration)
        {
            float weight = Mathf.Abs(Mathf.Sin(timer * 20f));
            expression.SetWeight(key, weight);
            timer += Time.deltaTime;
            yield return null;
        }
        expression.SetWeight(key, 0f);
    }
}