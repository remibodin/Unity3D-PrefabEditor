using System.IO;
using System.Linq;

using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// </summary>
[InitializeOnLoad]
public class PrefabEditorAssetProcessor : UnityEditor.AssetModificationProcessor
{
    /// <summary>
    /// Path of tmp PrefabEditor scene
    /// </summary>
    private static string mTmpScenePath;

    /// <summary>
    /// ctor call when unity editor start
    /// </summary>
    static PrefabEditorAssetProcessor()
    {
        SceneView.onSceneGUIDelegate += OnGUIScene;
        EditorApplication.update = OnUpdateEditor;
        mTmpScenePath = Path.Combine(GetDirectoryName(), "PrefabTmpScene.unity");

        // On windows editor, final path must be converted into UNIX format
        // to match with AssetDatabase.GetAssetPath and OnWillSaveAssets param
        #if UNITY_EDITOR_WIN
        mTmpScenePath = mTmpScenePath.Replace('\\', '/');
        #endif
    }

    /// <summary>
    /// Get path of PrefabEditor directory
    /// </summary>
    /// <returns>PrefabEditor path</returns>
    private static string GetDirectoryName()
    {
        var monoscript = MonoScript.FromScriptableObject(PrefabEditor.Instance);
        return Path.GetDirectoryName(AssetDatabase.GetAssetPath(monoscript));
    }

    /// <summary>
    /// Call during editor update
    /// </summary>
    private static void OnUpdateEditor()
    {
        // Remove tmp scene if prefab editor is close
        if (File.Exists(mTmpScenePath) && PrefabEditor.Instance.prefabInstance == null)
        {
            File.Delete(mTmpScenePath);
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// Call by the editor when user double click on any asset
    /// </summary>
    /// <param name="instanceID"></param>
    /// <param name="line"></param>
    /// <returns></returns>
    [UnityEditor.Callbacks.OnOpenAssetAttribute(1)]
    public static bool OpenPrefabEditorCallback(int instanceID, int line)
    {
        string assetPath = AssetDatabase.GetAssetPath(instanceID);
        var fileInfo = new FileInfo(assetPath);
        if (fileInfo.Extension == ".prefab")
        {
            OpenPrefabEditor(assetPath);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Draw GUI on SceneView
    /// </summary>
    /// <param name=sceneView></param>
    private static void OnGUIScene(SceneView sceneView)
    {
        if (PrefabEditor.Instance.prefabInstance == null)
        {
            return;
        }

        GUIStyle style = new GUIStyle();
        style.fontSize = 20;
        style.normal.textColor = new Color(0,0,0,0.5f);
        style.alignment = TextAnchor.UpperLeft;
        style.fontStyle = FontStyle.Bold;
        style.margin = new RectOffset(15, 0, 0, 0);

        Handles.BeginGUI();
        GUILayout.Label("Prefab Editor", style);
        if (!string.IsNullOrEmpty(PrefabEditor.Instance.previousScenePath))
        {
            if (GUILayout.Button("Back to previous scene", GUILayout.Width(175)))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(PrefabEditor.Instance.previousScenePath, OpenSceneMode.Single);
                }
            }
        }
        Handles.EndGUI();
    }

    /// <summary>
    /// This is called by Unity when it is about to write serialized assets or scene files to disk.
    /// </summary>
    /// <param name=paths></param>
    private static string[] OnWillSaveAssets(string[] paths)
    {
        if (paths.Contains(mTmpScenePath) && PrefabEditor.Instance.prefabInstance != null)
        {
            PrefabUtility.ReplacePrefab(
                PrefabEditor.Instance.prefabInstance, 
                PrefabEditor.Instance.prefabRoot, 
                ReplacePrefabOptions.ConnectToPrefab);
        }
        return paths;
    }

    /// <summary>
    /// Open prefab editor
    /// </summary>
    /// <param name=assetPath>Path of prefab file in assets directory</param>
    public static void OpenPrefabEditor(string assetPath)
    {
        // Load object at path
        var prefab = AssetDatabase.LoadAssetAtPath(assetPath, typeof(GameObject)) as GameObject;

        // Quit if load failed
        if (!prefab)
        {
            return;
        }

        // Quit if object is not a prefab
        if (PrefabUtility.GetPrefabType(prefab) != PrefabType.Prefab)
        {
            return;
        }

        // Quit if editor is in playmode
        if (EditorApplication.isPlaying)
        {
            EditorWindow.focusedWindow.ShowNotification(new GUIContent("Can't open prefab editor in playmode"));
            return;
        }

        // Save currently open scene
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        //Save current scene path
        PrefabEditor.Instance.previousScenePath = EditorSceneManager.GetActiveScene().path;

        //Create new empty scene
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);

        //Save this new scene 
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), mTmpScenePath);

        PrefabEditor.Instance.prefabRoot = prefab;

        //Instantiate the prefab and select it
        var prefabInstance = PrefabUtility.InstantiatePrefab(PrefabEditor.Instance.prefabRoot) as GameObject;
        PrefabEditor.Instance.prefabInstance = prefabInstance;

        if (prefab.GetComponentInChildren<CanvasRenderer>())
        {
            var canvas = new GameObject("Canvas");
            canvas.AddComponent<Canvas>();
            prefabInstance.transform.SetParent(canvas.transform);
        }

        Selection.activeGameObject = PrefabEditor.Instance.prefabInstance;

        //Focus our scene view camera to aid in editing
        if (SceneView.lastActiveSceneView)
        {
            SceneView.lastActiveSceneView.Focus();
            //Focus on prefab instance in scene editor
            SceneView.lastActiveSceneView.FrameSelected();
            //Enable default editor lighting
            SceneView.lastActiveSceneView.m_SceneLighting = false;
        }
    }
}

/// <summary>
/// PrefabEditor data.
/// ScriptableObject to use GetAssetPath in GetDirectoryName
/// and dont lose ref when assembly reload
/// </summary>
public class PrefabEditor : ScriptableObject
{
    #region singleton
    private static PrefabEditor m_instance = null;
    public static PrefabEditor Instance
    {
        get
        {
            if (m_instance == null)
            {
                m_instance = CreateInstance<PrefabEditor>();
                m_instance.hideFlags = HideFlags.HideAndDontSave;
            }
            return m_instance;
        }
    }

    private void OnEnable()
    {
        m_instance = this;
    }
    #endregion

    public Object prefabRoot;
    public GameObject prefabInstance;
    public string previousScenePath;
}
