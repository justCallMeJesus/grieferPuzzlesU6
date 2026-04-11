using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Dieses Fenster kannst du im Editor über "Tools/Copy BoxCollider To Children" öffnen
public class CopyBoxColliderToChildren : EditorWindow
{
    GameObject source; // du kannst das noch im Inspector setzen, brauchst es aber nicht mehr direkt nutzen
    bool includeInactive = true;

    [MenuItem("Tools/Copy BoxCollider To Children")]
    static void Open()
    {
        GetWindow<CopyBoxColliderToChildren>("Copy BoxCollider");
    }

    void OnGUI()
    {
        source = (GameObject)EditorGUILayout.ObjectField("Source Object (optional)", source, typeof(GameObject), true);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        GUI.enabled = Selection.transforms.Length > 0;
        if (GUILayout.Button("Apply to all selected objects' geometry_0"))
        {
            ApplyToSelectedGeometry0();
        }
        GUI.enabled = true;
    }

    // Wird aufgerufen, wenn du den Button klickst
    void ApplyToSelectedGeometry0()
    {
        // Alle aktuell ausgewählten Objekte iterieren
        foreach (Transform selectedTransform in Selection.transforms)
        {
            Transform parent = selectedTransform;

            // Wenn der User ausversehen ein geometry_0 auswählt, trotzdem auf den Parent schauen
            if (parent.name.ToLower() == "geometry_0" && parent.parent != null)
            {
                parent = parent.parent;
            }

            // Child mit Namen "geometry_0" suchen (case‑insensitiv)
            Transform child = parent.Find("geometry_0");
            if (child == null)
            {
                child = FindChildByNameIgnoreCase(parent, "geometry_0");
            }

            if (child != null)
            {
                // Auf GEOMETRY_0 den Collider anpassen
                ApplyColliderToGeometryObject(child.gameObject);
            }
        }

        // Szene als geändert markieren
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("BoxCollider values applied to geometry_0 children.");
    }

    // Hilfsfunktion: finde ein Child nach Namen case‑insensitive
    Transform FindChildByNameIgnoreCase(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.ToLower() == name.ToLower())
            {
                return child;
            }
        }
        return null;
    }

    // Hier wird der Collider auf dem geometry_0‑Objekt gesetzt
    void ApplyColliderToGeometryObject(GameObject geometryObj)
    {
        BoxCollider col = geometryObj.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = Undo.AddComponent<BoxCollider>(geometryObj);
        }

        // --- WICHTIG: Trage hier deine bekannten Werte ein ---
        // Du hast das Beispiel:
        //   center: (-0.1127603, 0.5947096, 0.06651554)
        //   size:   (1.060974, 0.560808, 0.09755837)
        Undo.RecordObject(col, "Set BoxCollider on geometry_0");

        col.center = new Vector3(-0.1127603f, 0.5947096f, 0.06651554f);
        col.size = new Vector3(1.060974f, 0.560808f, 0.09755837f);

        EditorUtility.SetDirty(col);

        // Falls es eine Prefab‑Instanz ist, Änderung recorden
        if (PrefabUtility.IsPartOfPrefabInstance(geometryObj))
        {
            PrefabUtility.RecordPrefabInstancePropertyModifications(col);
        }
    }
}