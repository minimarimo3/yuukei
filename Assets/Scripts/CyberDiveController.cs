using System.Collections;
using UnityEngine;

public class CyberDiveController : MonoBehaviour
{
    [Header("吸い込み時に発生させるパーティクル（任意）")]
    public ParticleSystem digitalNoiseParticle;

    [Header("吸い込みにかかる時間（秒）")]
    public float diveDuration = 0.8f;

    // 吸い込みアニメーションを開始する
    public void StartDive(Vector3 targetWorldPosition, System.Action onComplete = null)
    {
        StartCoroutine(DiveRoutine(targetWorldPosition, onComplete));
    }

    private IEnumerator DiveRoutine(Vector3 targetPos, System.Action onComplete)
    {
        Vector3 startPos = transform.position;
        Vector3 originalScale = transform.localScale;

        // パーティクルが設定されていれば再生
        if (digitalNoiseParticle != null)
        {
            digitalNoiseParticle.Play();
        }

        float timer = 0f;
        while (timer < diveDuration)
        {
            timer += Time.deltaTime;
            // 0.0 ～ 1.0 の進行度
            float t = Mathf.Clamp01(timer / diveDuration);
            
            // イージング（最初はゆっくり、後から加速して吸い込まれるような動き）
            float easeIn = t * t * t;

            // 1. 移動（現在地からターゲット座標へ）
            transform.position = Vector3.Lerp(startPos, targetPos, easeIn);

            // 2. 回転（Y軸を中心に高速スピン）
            transform.Rotate(0, 1500f * Time.deltaTime, 0);

            // 3. 伸縮（全体が徐々に小さくなりながら吸い込まれる）
            // 横幅（X, Z）は早く細くなり、縦（Y）は少し粘ってから縮むことで吸い込み感を出す
            float currentScaleXZ = Mathf.Lerp(originalScale.x, 0f, easeIn);
            float currentScaleY = Mathf.Lerp(originalScale.y, 0f, easeIn * easeIn); 
            
            transform.localScale = new Vector3(currentScaleXZ, currentScaleY, currentScaleXZ);

            yield return null;
        }

        // 最終的に完全に見えなくする
        transform.localScale = Vector3.zero;
        transform.position = targetPos;

        // パーティクルの発生を止める
        if (digitalNoiseParticle != null)
        {
            digitalNoiseParticle.Stop();
        }

        // 完了時のコールバック（フォルダを開くなどの処理をここで呼ぶ）
        onComplete?.Invoke();
    }

    // 元の姿に戻すためのリセット関数
    public void ResetCharacter(Vector3 respawnPosition)
    {
        transform.position = respawnPosition;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
    }
}