using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

[CustomEditor(typeof(GrassControl))]
public class GrassControlEditor : Editor
{
    [SerializeField] private GameObject grassObject;
    private GrassControl _grassControl;
    
    readonly string[] mainTabBarStrings = { "Auto Generate", "Paint/Edit" };
    int mainTab_current;
    
    // 当前笔刷模式是否启用
    bool paintModeActive;
    public int toolbarInt = 0;
    readonly string[] toolbarStrings = { "Add", "Remove" };
    RaycastHit[] terrainHit;
    
    // # UI
    public override void OnInspectorGUI()
    {
        // 这行删了也行的 =v=
        GUILayout.Label("== Remo Grass Generator ==", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter });
        EditorGUILayout.Space();
        
        Init_Tool(); // get GameObject grassObject & _grassControl;
        EditorGUILayout.Space();
        
        Generate_Grass();
        EditorGUILayout.Space();
        
        ResetGlassData();
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
        if (GUILayout.Button("Reset Glass Data List", GUILayout.Height(30)))
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
        
        
        mainTab_current = GUILayout.Toolbar(mainTab_current, mainTabBarStrings, GUILayout.Height(30));

        _grassControl.generationDensity = EditorGUILayout.Slider("Grass Place Density", _grassControl.generationDensity, 0.01f, 1f);

        switch (mainTab_current)
        {
            case 0: // Auto Generate
                ShowAutoGeneratePanel();
                break;
            case 1: // Paint/Edit
                ShowPaintEditPanel();
                break;
        }
        


        // 恢复GUI的激活状态
        GUI.enabled = true;
    }

    // #### 平均生成草分支
    void ShowAutoGeneratePanel()
    {
        
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
    }
    
    // #### 草地笔刷工具分支
    void ShowPaintEditPanel()
    {
        EditorGUILayout.BeginHorizontal(); // 水平排布
            EditorGUILayout.LabelField("Paint Mode:", EditorStyles.boldLabel);
            paintModeActive = EditorGUILayout.Toggle(paintModeActive);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Separator();
        
        EditorGUILayout.LabelField("Hit Settings", EditorStyles.boldLabel);
        LayerMask tempMask = EditorGUILayout.MaskField("Hit Mask", InternalEditorUtility.LayerMaskToConcatenatedLayersMask(_grassControl.hitMask), InternalEditorUtility.layers);
        _grassControl.hitMask = InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(tempMask);
        EditorGUILayout.Separator();
        
        EditorGUILayout.LabelField("Paint Status (Right-Mouse Button to paint)", EditorStyles.boldLabel);
        toolbarInt = GUILayout.Toolbar(toolbarInt, toolbarStrings);
        
        EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);
        _grassControl.brushSize = EditorGUILayout.Slider("Brush Size", _grassControl.brushSize, 0.1f, 50f);
        
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
    
    void OnSceneGUI(SceneView sceneView)
    {
        if (paintModeActive)
        {
            DrawHandles();
        }
    }
    
    
    RaycastHit[] m_Results = new RaycastHit[1];
    Ray ray;
    
    Vector3 hitPos;
    Vector3 hitNormal;
    
    [HideInInspector]
    public Vector3 hitPosGizmo;
    private Vector3 lastPosition = Vector3.zero;
    
    Vector3 cachedPos;

    // draw the painter handles
    void DrawHandles()
    {

        //  Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
        
        int hits = Physics.RaycastNonAlloc(ray, m_Results, 200f, _grassControl.hitMask.value);
        for (int i = 0; i < hits; i++)
        {
            hitPos = m_Results[i].point;
            hitNormal = m_Results[i].normal;
        }

        // Add Grass Color Base Settings
        Color discColor = Color.green;
        Color discColor2 = new(0, 0.5f, 0, 0.4f);
        switch (toolbarInt)
        {
            case 0:
                discColor = Color.green;
                discColor2 = new(0, 0.5f, 0, 0.4f);
                break;
            case 1:
                discColor = Color.red;
                discColor2 = new Color(0.5f, 0f, 0f, 0.4f);
                break;
        }
        
        Handles.color = discColor;
        Handles.DrawWireDisc(hitPos, hitNormal, _grassControl.brushSize);
        Handles.color = discColor2;
        Handles.DrawSolidDisc(hitPos, hitNormal, _grassControl.brushSize);

        if (hitPos != cachedPos)
        {
            SceneView.RepaintAll();
            cachedPos = hitPos;
        }
    }
    
    Vector3 mousePos;
    void OnScene(SceneView scene)
    {
        if (paintModeActive)
        {
            Event e = Event.current;
            mousePos = e.mousePosition;
            float ppp = EditorGUIUtility.pixelsPerPoint;
            mousePos.y = scene.camera.pixelHeight - mousePos.y * ppp;
            mousePos.x *= ppp;
            mousePos.z = 0;

            // ray for gizmo(disc)
            ray = scene.camera.ScreenPointToRay(mousePos);
            // TODO:undo system 
            // ...

            if (e.type == EventType.MouseDrag && e.button == 1)
            {
                switch (toolbarInt)
                {
                    case 0:
                        AddGrassPainting(terrainHit, e);
                        break;
                    case 1:
                        RemoveAtPoint(terrainHit, e);
                        break;
                }
                RebuildMeshFast();
            }

            // on up
            if (e.type == EventType.MouseUp && e.button == 1)
            {
                RebuildMesh();
            }
        }
    }
    
    public void AddGrassPainting(RaycastHit[] terrainHit, Event e)
    {
        int hits = (Physics.RaycastNonAlloc(ray, terrainHit, 200f, _grassControl.hitMask.value));
        for (int i = 0; i < hits; i++)
        {
            if ((1 << terrainHit[i].transform.gameObject.layer) > 0)
            {
                int grassToPlace = (int)(_grassControl.density * _grassControl.brushSize);
                
                for (int k = 0; k < grassToPlace; k++)
                {
                    if (terrainHit[i].normal != Vector3.zero)
                    {

                        Vector2 randomOffset = Random.insideUnitCircle * (_grassControl.brushSize * 10 / EditorGUIUtility.pixelsPerPoint);

                        Vector2 mousePosition = e.mousePosition;
                        Vector2 randomPosition = mousePosition + randomOffset;

                        Ray ray2 = HandleUtility.GUIPointToWorldRay(randomPosition);
                        
                        int hits2 = (Physics.RaycastNonAlloc(ray2, terrainHit, 200f, _grassControl.hitMask.value));

                        if (_grassControl.grassData.Count >= _grassControl.grassAmountToGenerate)
                        {
                            Debug.Log("Grass Reach Max!");
                            continue;
                        }
                        for (int l = 0; l < hits2; l++)
                        {
                            if ((1 << terrainHit[l].transform.gameObject.layer) > 0 && terrainHit[l].normal.y <= (1.8f) && terrainHit[l].normal.y >= (0.2f))
                            {
                                hitPos = terrainHit[l].point;
                                hitNormal = terrainHit[l].normal;

                                if (k != 0)
                                {
                                    // can paint
                                    GrassBlade newData = new GrassBlade();
                                    newData.position = hitPos;

                                        
                                    _grassControl.grassData.Add(newData);

                                }
                                else
                                {// to not place everything at once, check if the first placed point far enough away from the last placed first one
                                    if (Vector3.Distance(terrainHit[l].point, lastPosition) > _grassControl.brushSize)
                                    {

                                        GrassBlade newData = new GrassBlade();
                                        newData.position = hitPos;
                                        _grassControl.grassData.Add(newData);
                                        if (k == 0)
                                        {
                                            lastPosition = hitPos;
                                        }
                                    }

                                }
                            }

                        }
                    }
                }
            }
        }
        e.Use();
    }
    
    public void RemoveAtPoint(RaycastHit[] terrainHit, Event e)
    {

        int hits = (Physics.RaycastNonAlloc(ray, terrainHit, 100f, _grassControl.hitMask.value));
        for (int i = 0; i < hits; i++)
        {
            hitPos = terrainHit[i].point;
            hitPosGizmo = hitPos;
            hitNormal = terrainHit[i].normal;
            RemovePositionsNearRaycastHit(hitPos, _grassControl.brushSize);
        }

        e.Use();
    }
    
    private void RemovePositionsNearRaycastHit(Vector3 hitPoint, float radius)
    {
        // Remove positions within the specified radius
        _grassControl.grassData.RemoveAll(pos => Vector3.Distance(pos.position, hitPoint) <= radius);
    }
    
    void RebuildMesh()
    {
        _grassControl.Reset();
    }

    void RebuildMeshFast()
    {
        _grassControl.ResetFaster();

    }
    
    
    

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        SceneView.duringSceneGui += this.OnScene;
        terrainHit = new RaycastHit[1];
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui -= this.OnScene;
    }
    
    void OnDestroy()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui -= this.OnScene;
    }
    
    
// #if UNITY_EDITOR
// #endif
}