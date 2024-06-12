using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GrassControl))]
public class GrassControlEditor : Editor
{
    [SerializeField] private GameObject grassObject;
    private GrassControl _grassControl;
    
    // # UI
    public override void OnInspectorGUI()
    {
        // 这行删了也行的 =v=
        GUILayout.Label("== Remo Grass Generator ==", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
        Init_Tool(); // get GameObject grassObject & _grassControl;
        ResetGlassData();
        EditorGUILayout.Space();
        
        Generate_Grass();
        
        EditorGUILayout.Space();
        
        // 默认的 GrassControl 的 Inspector 界面
        base.OnInspectorGUI();
    }

    // ## 初始化 UI 事件
    private void Init_Tool()
    {
        // 绑定到当前的刷草工具Object，草的数据就存在这个Object里。
        grassObject = (GameObject)EditorGUILayout.ObjectField("Grass Compute Object", grassObject, typeof(GameObject), true);   
        if (grassObject == null)
        {
            grassObject = FindObjectOfType<GrassControl>()?.gameObject;
        }
        if (grassObject != null)
        {
            _grassControl = grassObject.GetComponent<GrassControl>();
            if (_grassControl != null)
            {
                EditorGUILayout.LabelField("Connected To Object!");
            }
            else
            {
                EditorGUILayout.LabelField("Error! Connect to the OBJ which have <GrassControl>!");
                GUI.enabled = false;
            }
        }
    }
    
    // ## UI 按钮事件 - 重置草数组
    void ResetGlassData()
    {
        // 显示按钮并在点击时执行操作
        if (GUILayout.Button("Reset Glass Data List"))
        {
            // 清空所有草数据
            _grassControl.grassData.Clear();
            // 将 m_LocalBounds 的中心和大小都设置为 Vector3.zero
            _grassControl.m_LocalBounds = new Bounds(Vector3.zero, Vector3.zero);
            _grassControl.OnEnable();
        }
    }
    
    // ## UI 按钮事件 - 在选定对象上生成草
    private void Generate_Grass()
    {
        _grassControl.grassLayer = EditorGUILayout.LayerField("Grass Generation Layer", _grassControl.grassLayer);
        _grassControl.grassAmountToGenerate = EditorGUILayout.IntField("Grass Place Max Amount", _grassControl.grassAmountToGenerate);
        _grassControl.generationDensity = EditorGUILayout.Slider("Grass Place Density", _grassControl.generationDensity, 0.01f, 1f);
        
        // 实时显示当前Editor选中对象并控制按钮的可用性
        EditorGUILayout.LabelField("Selection Info:", EditorStyles.boldLabel);
        bool hasSelection = Selection.activeGameObject != null;
        GUI.enabled = hasSelection;
        if (hasSelection)
            foreach (GameObject obj in Selection.gameObjects)
                EditorGUILayout.LabelField(obj.name);
        else
            EditorGUILayout.LabelField("No active object selected.");

        // 显示按钮并在点击时执行操作
        if (GUILayout.Button("Generate Grass to Selected Objects") && hasSelection)
        {
            //====================================================================================
            // 在所有选中的对象上生成草
            //====================================================================================
            _grassControl.OnEnable();
            GenerateGrassForSelectedObjects();
            
        }

        // 恢复GUI的激活状态
        GUI.enabled = true;
    }
    
    //====================================================================================
    // 在所有选中的对象上生成草
    //====================================================================================
    void GenerateGrassForSelectedObjects()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            
            if (renderer != null && mf != null)
            {
                // 确保对象上有 MeshCollider 且开启，一会才能用Raycast
                MeshCollider collider = obj.GetComponent<MeshCollider>();
                if (collider == null) collider = obj.AddComponent<MeshCollider>();
                collider.enabled = true;
                
                // 获取Mesh的世界AABB
                Bounds bounds = renderer.bounds;
                Vector3 size = bounds.size;
                Vector3 scale = mf.transform.lossyScale;
                
                // 计算一会需要生成的 Grass 的数量
                float area = (size.x * scale.x) * (size.z * scale.z);
                int grassCount = Mathf.FloorToInt(area * _grassControl.generationDensity);
                
                //====================================================================================
                // 在指定的AABB内生成指定数量的草
                //====================================================================================
                GenerateGrassBlades(obj, bounds, grassCount);
            } else {
                Debug.LogError("MeshFilter or Renderer component is missing.");
            }
        }

    }
    
    //====================================================================================
    // 在指定的AABB内生成指定数量的草
    //====================================================================================
    private void GenerateGrassBlades(GameObject obj, Bounds bounds, int grassCount)
    {
        Vector3 worldBoundsMin = bounds.min;
        Vector3 worldBoundsMax = bounds.max;
        Debug.Log("bounds" + bounds);

        int attempts = 0;
        int bladesAdded = 0;
        float rayStartHeight = 10f;  // Start the ray above the highest point of the bounds

        // while循环是个需要注意的函数，第二行是为了保险加上的
        while (bladesAdded < grassCount &&
               attempts < grassCount * 10 && 
               _grassControl.grassData.Count < _grassControl.grassAmountToGenerate)
        {
            attempts++;
            Vector3 randomWorldPos = new Vector3(
                Random.Range(worldBoundsMin.x, worldBoundsMax.x),
                worldBoundsMax.y + rayStartHeight,
                Random.Range(worldBoundsMin.z, worldBoundsMax.z)
            );
            
            //====================================================================================
            // 在AABB中进行射线检测，返回生成的草的世界坐标
            //====================================================================================
            if (TryPlaceGrassBlade(randomWorldPos, bounds.min.y, out Vector3 grassPosWS))
            {
                _grassControl.grassData.Add(new GrassBlade(grassPosWS));  // Assuming GrassBlade takes a Vector3
                bladesAdded++;
            }
        }

        if (bladesAdded == 0)
        {
            Debug.LogWarning("No Grass Generated!");
            return;
        }
        
        // Update Bounds
            // Get the bounds of all the grass points and then expand
        _grassControl.m_LocalBounds = new Bounds(_grassControl.grassData[0].position, Vector3.one);
        for (int i = 0; i < _grassControl.grassData.Count; i++)
        {
            Vector3 target = _grassControl.grassData[i].position;
            _grassControl.m_LocalBounds.Encapsulate(target);
        }
        
        // Update Editor to Show The New Grass
        _grassControl.OnEnable();
    }

    //====================================================================================
    // 在AABB中进行射线检测，返回生成的草的世界坐标
    //====================================================================================
    private bool TryPlaceGrassBlade(Vector3 position,float boundMinY, out Vector3 grassPosWS)
    {
        RaycastHit hit;
        if (Physics.Raycast(position, Vector3.down, out hit, 
                Mathf.Max(100, position.y - boundMinY), 
                ~_grassControl.grassLayer))
        {
            grassPosWS = hit.point;  // hit.point is in world coordinates
            return true;
        }
        grassPosWS = Vector3.zero;
        return false;
    }


    // 确保面板数据正确
    void OnValidate()
    {
        EditorUtility.SetDirty(this);
    }
    
}