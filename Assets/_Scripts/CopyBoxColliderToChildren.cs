using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CopyBoxColliderToChildren : EditorWindow
{
    GameObject source;
    bool includeInactive = true;

    [MenuItem("Tools/Copy BoxCollider To Children")]
    static void Open()
    {
        GetWindow<CopyBoxColliderToChildren>("Copy BoxCollider");
    }

    void OnGUI()
    {
        source = (GameObject)EditorGUILayout.ObjectField("Source Object", source, typeof(GameObject), true);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        GUI.enabled = source != null;
        if (GUILayout.Button("Apply to Children"))
        {
            Apply();
        }
        GUI.enabled = true;
    }

    void Apply()
    {
        BoxCollider srcCol = source.GetComponent<BoxCollider>();
        if (srcCol == null)
        {
            Debug.LogError("Source object has no BoxCollider.");
            return;
        }

        Transform root = source.transform.parent;
        if (root == null)
        {
            Debug.LogError("Source object must be inside a parent folder/object with the other copies.");
            return;
        }

        foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive))
        {
            if (t.gameObject == source) continue;

            BoxCollider col = t.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(t.gameObject);

            Undo.RecordObject(col, "Copy BoxCollider");
            col.center = srcCol.center;
            col.size = srcCol.size;
            EditorUtility.SetDirty(col);

            if (PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(col);
        }

        EditorSceneManager.MarkSceneDirty(source.scene);
        Debug.Log("BoxCollider values applied permanently.");
    }
}