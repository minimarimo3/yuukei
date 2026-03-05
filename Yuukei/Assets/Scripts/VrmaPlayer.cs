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

        if (_playingInstance != null)
        {
            Destroy(_playingInstance.gameObject);
        }

        Debug.Log("[VrmaPlayer] VRMAアニメーションを適用します。");

        var wasActive = vrmaPrefab.gameObject.activeSelf;
        vrmaPrefab.gameObject.SetActive(false);
        _playingInstance = Instantiate(vrmaPrefab);
        vrmaPrefab.gameObject.SetActive(wasActive);
        
        var currentVrm = characterManager.CurrentVrmInstance;

        // 対象VRMのローカル座標を一度リセットして基準位置のズレ（ワープ）を防ぐ
        currentVrm.transform.localPosition = Vector3.zero;
        currentVrm.transform.localRotation = Quaternion.identity;
        
        // 【重要】アニメーションソースはVRMの「兄弟オブジェクト」として配置する
        // VRMの子にしてしまうとリターゲティング時の移動計算がループしてテレポートする原因になります。
        _playingInstance.transform.SetParent(currentVrm.transform.parent, false);
        _playingInstance.transform.localPosition = currentVrm.transform.localPosition;
        _playingInstance.transform.localRotation = currentVrm.transform.localRotation;
        _playingInstance.transform.localScale = Vector3.one;

        if (_playingInstance.ControlRig.Item1 == null || _playingInstance.ControlRig.Item2 == null)
        {
            var humanoid = _playingInstance.GetComponent<UniHumanoid.Humanoid>();
            if (humanoid == null)
            {
                humanoid = _playingInstance.gameObject.AddComponent<UniHumanoid.Humanoid>();
                humanoid.AssignBonesFromAnimator();
            }

            if (humanoid != null)
            {
                var provider = new UniVRM10.InitRotationPoseProvider(_playingInstance.transform, humanoid);
                _playingInstance.ControlRig = (provider, provider);
            }
        }

        // デバッグ用のグレーのメッシュ(BoxMan)は非表示にする
        _playingInstance.ShowBoxMan(false);

        currentVrm.Runtime.VrmAnimation = _playingInstance;

        var anim = _playingInstance.GetComponent<Animation>();
        if (anim != null)
        {
            _playingInstance.gameObject.SetActive(true);
            anim.Play();
        }
        else
        {
            var animator = _playingInstance.GetComponent<Animator>();
            if (animator != null)
            {
                _playingInstance.gameObject.SetActive(true);
                animator.Play(0);
            }
            else
            {
                _playingInstance.gameObject.SetActive(true);
            }
        }
    }

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
            characterManager.CurrentVrmInstance.Runtime.VrmAnimation = null;
        }
    }

    private void OnDestroy()
    {
        StopAnimation();
    }
}
