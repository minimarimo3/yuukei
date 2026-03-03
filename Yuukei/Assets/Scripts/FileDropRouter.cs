using UnityEngine;
using Kirurobo;
using System.IO;

[RequireComponent(typeof(UniWindowController))]
public class FileDropRouter : MonoBehaviour
{
    private UniWindowController _windowController;

    private void Awake()
    {
        _windowController = GetComponent<UniWindowController>();
        _windowController.allowDropFiles = true;
        // UniWindowControllerのファイルドロップイベントに登録
        _windowController.OnDropFiles += HandleFilesDropped;
    }

    private void OnDestroy()
    {
        if (_windowController != null)
        {
        _windowController.OnDropFiles -= HandleFilesDropped;
        }
    }

    private void HandleFilesDropped(string[] filePaths)
    {
        foreach (string path in filePaths)
        {
            if (!File.Exists(path)) continue;

            string extension = Path.GetExtension(path).ToLower();
            string fileName = Path.GetFileNameWithoutExtension(path);

            // 拡張子に基づいて適切なマネージャーにパスをルーティングする
            switch (extension)
            {
                case ".vrm":
                case ".bundle": // 独自のキャラモデル形式など
                    Debug.Log($"キャラクターファイルがドロップされました: {path}");
                    ConfigManager.Instance.AddCharacter(fileName, path);
                    // 必要であれば、ここでドロップされたキャラを即座に「選択」状態にする処理を呼ぶ
                    break;

                case ".txt":
                case ".json":
                    // TODO: 台本マネージャー等へルーティング
                    Debug.Log($"台本ファイルがドロップされました: {path}");
                    break;

                default:
                    Debug.Log($"未対応のファイル形式です: {path}");
                    break;
            }
        }
    }
}