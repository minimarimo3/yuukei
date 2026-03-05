using UnityEngine;
using UniVRM10;

public class VrmaPlayer : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private CharacterManager characterManager;
    
    [Header("Animation")]
    [SerializeField, Tooltip("Assets/Motion/motion003 などのVRMAプレハブをアタッチしてください")]
    private Vrm10AnimationInstance vrmaPrefab;

    private Vrm10AnimationInstance _playingInstance;

    private void Start()
    {
        if (characterManager == null)
        {
            characterManager = FindObjectOfType<CharacterManager>();
        }
    }

    /// <summary>
    /// 設定されたVRMAアニメーションを現在ロード中のVRMに適用して再生します
    /// </summary>
    [ContextMenu("Play VRMA Animation")]
    public void PlayAnimation()
    {
        if (characterManager == null || characterManager.CurrentVrmInstance == null)
        {
            Debug.LogWarning("[VrmaPlayer] VRMキャラクターがまだロードされていません。");
            return;
        }

        if (vrmaPrefab == null)
        {
            Debug.LogWarning("[VrmaPlayer] VRMAプレハブがInspectorに設定されていません。");
            return;
        }

        // 既存のアニメーションが再生中であれば一度破棄する
        if (_playingInstance != null)
        {
            Destroy(_playingInstance.gameObject);
        }

        Debug.Log("[VrmaPlayer] VRMAアニメーションを適用します。");

        // 一度無効な状態でプレハブを生成し、Animatorが初期ポーズを上書きする前にT-Poseを取得できるようにする
        var wasActive = vrmaPrefab.gameObject.activeSelf;
        vrmaPrefab.gameObject.SetActive(false);
        _playingInstance = Instantiate(vrmaPrefab);
        vrmaPrefab.gameObject.SetActive(wasActive);
        
        _playingInstance.transform.SetParent(transform, false);
        _playingInstance.transform.localPosition = Vector3.zero;
        _playingInstance.transform.localRotation = Quaternion.identity;
        _playingInstance.transform.localScale = Vector3.one;
        
        // UniVRM10の機能でRuntimeのVrmAnimationプロパティに適用することで連動開始
        var currentVrm = characterManager.CurrentVrmInstance;
        
        // 適用先のVRMを強制的にTポーズの基準へ持っていく (これがズレていると腕が上がったままになることがある)
        if (currentVrm.Runtime.ControlRig != null)
        {
            currentVrm.transform.localPosition = Vector3.zero;
            currentVrm.transform.localRotation = Quaternion.identity;
        }

        // Vrm10AnimationInstance の ControlRig が初期化されていない場合は初期化を試みる
        if (_playingInstance.ControlRig.Item1 == null || _playingInstance.ControlRig.Item2 == null)
        {
            var humanoid = _playingInstance.GetComponent<UniHumanoid.Humanoid>();
            if (humanoid == null)
            {
                // Vrm10AnimationInstance.Initialize() が呼ばれていない場合、ここで初期化することもできるが
                // 手動でHumanoidコンポーネントを追加してセットアップする
                humanoid = _playingInstance.gameObject.AddComponent<UniHumanoid.Humanoid>();
                humanoid.AssignBonesFromAnimator();
            }

            if (humanoid != null)
            {
                var provider = new UniVRM10.InitRotationPoseProvider(_playingInstance.transform, humanoid);
                _playingInstance.ControlRig = (provider, provider);
            }
        }
        
        // デバッグ用のグレーのメッシュ(BoxMan)を再度表示して、VRMA自体のアニメーション基準がズレているか確認する
        if (_playingInstance.BoxMan != null)
        {
            _playingInstance.ShowBoxMan(true);
        }

        currentVrm.Runtime.VrmAnimation = _playingInstance;

        // Vrm10AnimationInstance は大抵 Animation コンポーネントで動いているので再生
        var anim = _playingInstance.GetComponent<Animation>();
        if (anim != null)
        {
            _playingInstance.gameObject.SetActive(true);
            anim.Play();
        }
        else
        {
            // Animatorを使用しているケースへのフォールバック
            var animator = _playingInstance.GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
                _playingInstance.gameObject.SetActive(true);
                animator.enabled = true; // デフォルトステートを再生
                animator.Update(0f);
            }
            else
            {
                _playingInstance.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// 現在再生中のアニメーションを停止してリセットします
    /// </summary>
    [ContextMenu("Stop VRMA Animation")]
    public void StopAnimation()
    {
        if (_playingInstance != null)
        {
            Destroy(_playingInstance.gameObject);
            _playingInstance = null;
        }

        if (characterManager != null && characterManager.CurrentVrmInstance != null)
        {
            // アニメーションの参照を解除してTポーズ等に戻す
            characterManager.CurrentVrmInstance.Runtime.VrmAnimation = null;
        }
    }

    private void OnDestroy()
    {
        // スクリプト破棄時にアニメーション状態もクリアする
        StopAnimation();
    }
}
