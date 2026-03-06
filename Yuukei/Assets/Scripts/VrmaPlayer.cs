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

    [ContextMenu("Play VRMA Animation")]
    public void PlayAnimation(Vrm10AnimationInstance overridePrefab = null)
    {
        if (characterManager == null || characterManager.CurrentVrmInstance == null) return;
        var prefab = overridePrefab ?? vrmaPrefab;
        if (prefab == null) return;

        if (_playingInstance != null) Destroy(_playingInstance.gameObject);

        var wasActive = prefab.gameObject.activeSelf;
        prefab.gameObject.SetActive(false);
        _playingInstance = Instantiate(prefab);
        prefab.gameObject.SetActive(wasActive);
        
        var currentVrm = characterManager.CurrentVrmInstance;

        _playingInstance.transform.SetParent(currentVrm.transform.parent, false);
        _playingInstance.transform.localPosition = currentVrm.transform.localPosition;
        _playingInstance.transform.localRotation = currentVrm.transform.localRotation;
        
        // 【修正】ローカルスケールは 1.0 に戻す（親の CharacterRoot のスケールが適用されるため）
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

        // BoxMan（箱人間メッシュ）が存在する場合のみ非表示処理を行うように安全対策を追加
        if (_playingInstance.BoxMan != null)
        {
            _playingInstance.ShowBoxMan(false);
        }

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
                
                // コントローラーがアタッチされていれば最初から再生させる
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Rebind();
                    animator.Update(0f);
                }
                else
                {
                    // Controllerがセットされていない場合は親切に警告を出す
                    Debug.LogWarning($"[VrmaPlayer] {_playingInstance.name} のAnimatorに Controller が割り当てられていないため、アニメーションが再生されません。");
                }
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