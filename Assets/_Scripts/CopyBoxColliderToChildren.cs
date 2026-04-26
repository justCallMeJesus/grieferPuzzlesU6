using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class CopyMeshColliderToChildren : EditorWindow
{
    GameObject source;
    bool includeInactive = true;

    [MenuItem("Tools/Copy MeshCollider To Children")]
    static void Open()
    {
        GetWindow<CopyMeshColliderToChildren>("Copy MeshCollider");
    }

    void OnGUI()
    {
        source = (GameObject)EditorGUILayout.ObjectField("Source Object", source, typeof(GameObject), true);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        GUI.enabled = source != null;
        if (GUILayout.Button("Apply to all geometry_0 under Source"))
        {
            ApplyToAllGeometry0UnderSource();
        }
        GUI.enabled = true;
    }

    void ApplyToAllGeometry0UnderSource()
    {
        if (source == null)
        {
            Debug.LogWarning("Bitte zuerst big_walls als Source Object zuweisen.");
            return;
        }

        Transform root = source.transform;
        int count = 0;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(includeInactive))
        {
            if (t.name.Equals("geometry_0", System.StringComparison.OrdinalIgnoreCase))
            {
                ApplyMeshColliderToGeometryObject(t.gameObject);
                count++;
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("MeshCollider auf " + count + " geometry_0-Objekte angewendet.");
    }

    void ApplyMeshColliderToGeometryObject(GameObject geometryObj)
    {
        MeshFilter mf = geometryObj.GetComponent<MeshFilter>();
        if (mf == null)
        {
            Debug.LogWarning("Kein MeshFilter gefunden auf: " + geometryObj.name);
            return;
        }

        MeshCollider col = geometryObj.GetComponent<MeshCollider>();
        if (col == null)
        {
            col = Undo.AddComponent<MeshCollider>(geometryObj);
        }

        Undo.RecordObject(col, "Set MeshCollider on geometry_0");
        col.sharedMesh = mf.sharedMesh;
        col.convex = false;

        EditorUtility.SetDirty(col);

        if (PrefabUtility.IsPartOfPrefabInstance(geometryObj))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(col);
        }
    }
}