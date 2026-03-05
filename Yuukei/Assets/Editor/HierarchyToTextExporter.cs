using UnityEngine;
using UnityEditor;
using System.Text;
using System.Linq;
using UnityEngine.SceneManagement;

public class HierarchyToTextExporter : Editor
{
    [MenuItem("GameObject/AI用にHierarchyをコピー", false, -10)]
    public static void CopyHierarchyForAI()
    {
        StringBuilder sb = new StringBuilder();
        GameObject[] selectedObjects = Selection.gameObjects;

        if (selectedObjects.Length > 0)
        {
            sb.AppendLine("### 選択されたGameObjectの階層構造");
            // 選択されたオブジェクトのみを出力（ルートレベルの兄弟として扱う）
            foreach (GameObject obj in selectedObjects)
            {
                BuildTreeString(obj.transform, sb, 0);
            }
        }
        else
        {
            sb.AppendLine($"### シーン全体の階層構造 (Scene: {SceneManager.GetActiveScene().name})");
            // 何も選択されていない場合はシーンのルートオブジェクトを全て出力
            GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (GameObject obj in rootObjects)
            {
                BuildTreeString(obj.transform, sb, 0);
            }
        }

        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log("[AI Exporter] Hierarchyの構造をクリップボードにコピーしました！");
    }

    private static void BuildTreeString(Transform transform, StringBuilder sb, int depth)
    {
        // インデントの生成
        string indent = new string(' ', depth * 4);
        string prefix = depth == 0 ? "- " : "└ ";

        GameObject obj = transform.gameObject;
        
        // コンポーネントのリストを取得 (Missing Script対策も含む)
        Component[] components = obj.GetComponents<Component>();
        string compNames = string.Join(", ", components.Select(c => c == null ? "MissingScript" : c.GetType().Name));

        // タグとレイヤーの情報 (デフォルト設定の場合は省略して可読性を上げる)
        string tagInfo = obj.CompareTag("Untagged") ? "" : $" [Tag: {obj.tag}]";
        string layerInfo = obj.layer == 0 ? "" : $" [Layer: {LayerMask.LayerToName(obj.layer)}]";
        string activeState = obj.activeSelf ? "" : " (Inactive)";

        // 1行に情報をまとめる
        sb.AppendLine($"{indent}{prefix}{obj.name}{activeState}{tagInfo}{layerInfo} <Components: {compNames}>");

        // 子オブジェクトを再帰的に処理
        foreach (Transform child in transform)
        {
            BuildTreeString(child, sb, depth + 1);
        }
    }
}