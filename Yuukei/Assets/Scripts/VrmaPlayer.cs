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
    public void PlayAnimation()
    {
        if (characterManager == null || characterManager.CurrentVrmInstance == null) return;
        if (vrmaPrefab == null) return;

        if (_playingInstance != null) Destroy(_playingInstance.gameObject);

        var wasActive = vrmaPrefab.gameObject.activeSelf;
        vrmaPrefab.gameObject.SetActive(false);
        _playingInstance = Instantiate(vrmaPrefab);
        vrmaPrefab.gameObject.SetActive(wasActive);
        
        var currentVrm = characterManager.CurrentVrmInstance;

        currentVrm.transform.localPosition = Vector3.zero;
        currentVrm.transform.localRotation = Quaternion.identity;
        
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