using System;
using UnityEngine;

/// <summary>
/// キャラクターが存在する場合のみオブジェクトをアクティブにするコンポーネント。
/// </summary>
public class CharacterDependentVisibility : MonoBehaviour
{
    [SerializeField] private CharacterManager _characterManager;

    private void Start()
    {
        if (_characterManager == null)
            _characterManager = FindObjectOfType<CharacterManager>();

        if (_characterManager != null)
        {
            _characterManager.OnCharacterPresenceChanged += SetVisibility;
            // 初期状態を反映
            SetVisibility(_characterManager.CurrentVrmInstance != null);
        }
    }

    private void OnDestroy()
    {
        if (_characterManager != null)
        {
            _characterManager.OnCharacterPresenceChanged -= SetVisibility;
        }
    }

    private void SetVisibility(bool isPresent)
    {
        // オブジェクト全体を非アクティブ化します。
        // もし「非表示中もスクリプトのUpdate等は回したい」要件がある場合は、
        // gameObject.SetActive ではなく、Canvas.enabled や Renderer.enabled を
        // 切り替える設計に変更してください。
        gameObject.SetActive(isPresent);
    }
}