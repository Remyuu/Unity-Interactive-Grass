using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Random = UnityEngine.Random;

[ExecuteInEditMode]
public class GrassControl : MonoBehaviour
{
    // base data lists - 当前所有草的数据
    [SerializeField, Header("= Grass Data List =")] 
    public List<GrassBlade> grassData = new List<GrassBlade>();
    
    [SerializeField, HideInInspector, Header("= Grass Visible ID List =")] 
    List<int> grassVisibleIDList = new List<int>();
    
    // 生草工具的面板数据存储
    [SerializeField, HideInInspector]
    public int grassLayer = 0; // 默认为 "Default" Layer
    [SerializeField, HideInInspector]
    public int grassAmountToGenerate = 20000;
    [SerializeField, HideInInspector]
    public float generationDensity = 0.67f;
    
    // 最终提交到Material的草最高数量，即CS->Material的Buffer大小，根据个人的系统修改，
        // Colin的代码中这里设置为有可能生成的草的总数量，我觉得是合理的。
        // 重点注意，这个数值只能设大，否则会造成显存泄漏
    [Header("Max Buffer Size")]
    public int maxBufferSize = 524800;
    
    [Header("Compute Shader To Use")]
    [SerializeField] private ComputeShader shader = default;
    
    [Header("Grass Material (Instanted)")]
    [SerializeField] private Material bladeMaterial = default;

    [SerializeField, Header("Trampler")] 
    public List<trampleStruct> tramplers = new List<trampleStruct>();
    [Range(0f,10f)] public float trampleStrength = 6f;
    
    [Header("Grass Blade")]
    [Range(0,1)] public float density = 0.5f;
    public Color _TopColor = new Color(0.7734f, 0, 1);
    public Color _BottomColor = new Color(0, 0, 1);
    [Range(10, 45)] public float maxBend = 30f;
    [Range(0,1f)] public float width = 0.2f;
    [Range(0,1f)] public float rd_width = 0.1f;
    [Range(0,2)] public float height = 1f;
    [Range(0,1f)] public float rd_height = 0.2f;

    [Header("LoD")]
    public float minFadeDistance = 50f;
    public float maxFadeDistance = 100f;
    public int CullingDepth = 4;
    
    [Header("Wind")]
    [Range(0, 20)] public float windSpeed = 1.5f;
    [Range(0, 360)] public float windDirection = 90f;
    [Range(10, 500)] public float windScale = 16f;
    
    Mesh Blade {
        get {
            Mesh mesh;

            if (blade != null) {
                mesh = blade;
            }
            else {
                mesh = new Mesh();
                
                float rowHeight = this.height / 4;
                float halfWidth = this.width ;
                
                //1. Use the above variables to define the vertices array
                Vector3[] vertices =
                {
                    new Vector3(-halfWidth, 0, 0),
                    new Vector3( halfWidth, 0, 0),
                    new Vector3(-halfWidth, rowHeight, 0),
                    new Vector3( halfWidth, rowHeight, 0),
                    new Vector3(-halfWidth*0.7f, rowHeight*2, 0),
                    new Vector3( halfWidth*0.7f, rowHeight*2, 0),
                    new Vector3(-halfWidth*0.3f, rowHeight*3, 0),
                    new Vector3( halfWidth*0.3f, rowHeight*3, 0),
                    new Vector3( 0, rowHeight*4, 0)
                };
                //2. Define the normals array, hint: each vertex uses the same normal
                Vector3 normal = new Vector3(0, 0, -1);
                Vector3[] normals =
                {
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal,
                    normal
                };
                //3. Define the uvs array
                Vector2[] uvs =
                {
                    new Vector2(0,0),
                    new Vector2(1,0),
                    new Vector2(0,0.25f),
                    new Vector2(1,0.25f),
                    new Vector2(0,0.5f),
                    new Vector2(1,0.5f),
                    new Vector2(0,0.75f),
                    new Vector2(1,0.75f),
                    new Vector2(0.5f,1)
                };
                //4. Define the indices array
                int[] indices =
                {
                    0,1,2,1,3,2,//row 1
                    2,3,4,3,5,4,//row 2
                    4,5,6,5,7,6,//row 3
                    6,7,8//row 4
                };                
                //5. Assign the mesh properties using the arrays
                //   for indices use
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.uv = uvs;
                mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            }
            return mesh;
        }
    }
    
    // A state variable to help keep track of whether compute buffers have been set up
    private bool m_Initialized;
    // 获取摄像机
    private Camera m_MainCamera;
    // 程式化草Mesh
    private Mesh blade;
    // 获取地面贴图用于噪声预览
    private Material groundMaterial;
    // 实例化私有Shader占用各自Buffer
    private ComputeShader m_ComputeShader;
    private Material m_Material;
    // 草地Compute Shader的ID
    private int m_ID_GrassKernel;
    // GPU Buffer
    private ComputeBuffer m_InputBuffer;
    private ComputeBuffer m_OutputBuffer;
    private ComputeBuffer m_argsBuffer;
    private ComputeBuffer m_VisibleIDBuffer;
    // CPU Array 存草的数组
    private GrassBlade[] m_bladesArray;
    // 草的数量
    private int m_Count;
    // 所有草的AABB
    public Bounds m_LocalBounds;
    // 需要每帧都传给GPU的变量ID地址
    private int ID_time;
    private int ID_tramplePos; // 在[3]新增交互半径
    private int ID_trampleLength;
    private int ID_camreaPos;
    // 草CS的x大小
    private int m_DispatchSize;
    // 单个Compute Buffer变量的大小
    private const int SIZE_GRASS_INPUT_STRIDE  =  4 * sizeof(float);
    private const int SIZE_GRASS_OUTPUT_STRIDE = 12 * sizeof(float);
    private const int INDIRECT_ARGS_STRIDE     =  5 * sizeof(uint);
    private const int VISIBLE_ID_STRIDE        =  1 * sizeof(int);
    // 第二项会在Compute Shader中动态改变以实现LoD
    private uint[] argsBufferReset = new uint[5] {
        (uint)21, (uint)0, (uint)0, (uint)0, (uint)0
    }; // 当前Blade有7个三角形 3721
    
    // //====================================================================================
    // // 八叉树划分数据
    // //====================================================================================
    CullingTreeNode cullingTree;
    List<Bounds> BoundsListVis = new List<Bounds>();
    List<CullingTreeNode> leaves = new List<CullingTreeNode>();
    Plane[] cameraFrustumPlanes = new Plane[6];
    float cameraOriginalFarPlane;
    
    // speeding up the editor a bit
    Vector3 m_cachedCamPos;
    Quaternion m_cachedCamRot;
    
#if UNITY_EDITOR
    SceneView view;

    void OnDestroy()
    {
        // When the window is destroyed, remove the delegate
        // so that it will no longer do any drawing.
        SceneView.duringSceneGui -= this.OnScene;
    }

    void OnScene(SceneView scene)
    {
        view = scene;
        if (!Application.isPlaying)
        {
            if (view.camera != null)
            {
                m_MainCamera = view.camera;
            }
        }
        else
        {
            m_MainCamera = Camera.main;
        }
    }
    private void OnValidate()
    {
        // Set up components
        if (!Application.isPlaying)
        {
            if (view != null)
            {
                m_MainCamera = view.camera;
            }
        }
        else
        {
            m_MainCamera = Camera.main;
        }
    }
#endif
    
    public void OnEnable()
    {
        if (m_Initialized)
        {
            OnDisable();
        }
        InitShader();
    }
    
    void InitShader()
    {
#if UNITY_EDITOR
        SceneView.duringSceneGui += this.OnScene;
        if (!Application.isPlaying)
        {
            if (view != null && view.camera != null)
            {
                m_MainCamera = view.camera;
            }
        }
#endif
        if (Application.isPlaying)
        {
            m_MainCamera = Camera.main;
        }
        
        // Don't do anything if resources are not found,
        if (grassData.Count == 0)
        {
            return;
        }
        
        if (shader == null || bladeMaterial == null)
        {
            Debug.LogWarning("Missing Compute Shader/Material in grass Settings", this);
            return;
        }
        
        blade = Blade;
        
        // Init Kernel
        m_ComputeShader = Instantiate(shader);
        m_Material = Instantiate(bladeMaterial);
        m_ID_GrassKernel = m_ComputeShader.FindKernel("BendGrass");

        m_Count = grassData.Count;
        
        m_bladesArray = new GrassBlade[m_Count];
        
        int index = 0;
        while (index < m_Count)
        {
            m_bladesArray[index] = new GrassBlade(grassData[index].position);
            index++;
        }
        
        // Get m_DispatchSize
        Calculate_Thread_Size();

        // Link m_InputBuffer, m_OutputBuffer, m_argsBuffer to CS
        // Link m_OutputBuffer to Material
        Link_GPUBuffer(); 
        
        ID_tramplePos = Shader.PropertyToID("tramplePos");
        ID_trampleLength = Shader.PropertyToID("trampleLength");
        ID_time = Shader.PropertyToID("time");
        ID_camreaPos = Shader.PropertyToID("_CameraPositionWS");
        
        // Set Base Property to CS
        SetGrassDataBase();
        
        // Update Grass Material Property
        UpdateGrassMaterial();
        
        
        // 标记初始化完成
        m_Initialized = true;

        UpdateBounds();
        
        SetupQuadTree();
    }

    private void Calculate_Thread_Size()
    {
        uint threadGroupSize;
        m_ComputeShader.GetKernelThreadGroupSizes(m_ID_GrassKernel, out threadGroupSize, out _, out _);
        m_DispatchSize = Mathf.CeilToInt(grassVisibleIDList.Count / threadGroupSize);
    }
    
    private void Link_GPUBuffer()
    {
        m_InputBuffer = new ComputeBuffer(m_Count, SIZE_GRASS_INPUT_STRIDE,
            ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        m_InputBuffer.SetData(m_bladesArray);
        m_ComputeShader.SetBuffer(m_ID_GrassKernel, "_InputBuffer", m_InputBuffer);
        
        m_OutputBuffer = new ComputeBuffer(maxBufferSize, SIZE_GRASS_OUTPUT_STRIDE,
            ComputeBufferType.Append);
        m_OutputBuffer.SetCounterValue(0);
        m_ComputeShader.SetBuffer(m_ID_GrassKernel, "_OutputBuffer", m_OutputBuffer);
        m_Material     .SetBuffer("_OutputBuffer", m_OutputBuffer);
        
        m_argsBuffer = new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, 
            ComputeBufferType.IndirectArguments);
        m_argsBuffer.SetData(argsBufferReset);
        m_ComputeShader.SetBuffer(m_ID_GrassKernel, "_IndirectArgsBuffer", m_argsBuffer);
        
        m_VisibleIDBuffer = new ComputeBuffer(grassData.Count, VISIBLE_ID_STRIDE,
            ComputeBufferType.Structured); //uint only, per visible grass
        m_ComputeShader.SetBuffer(m_ID_GrassKernel, "_VisibleIDBuffer", m_VisibleIDBuffer);
    }
    
    private void UpdateGrassMaterial()
    {
        m_Material.SetColor("_BottomColor", _BottomColor);
        m_Material.SetColor("_TopColor", _TopColor);
        m_Material.SetVector("_AABB",new Vector4(m_LocalBounds.center.x, m_LocalBounds.center.y, m_LocalBounds.center.z, 0f));
    }
    
    private void SetGrassDataBase()
    {
        // 预计算一些下面会用到的量
        Vector4 wind = new Vector4(Mathf.Cos(windDirection * Mathf.PI / 180), 
            Mathf.Sin(windDirection * Mathf.PI / 180), windSpeed, windScale);
        // 初始化那些运行时不需要每一帧都修改的变量
        // Trample
        
        m_ComputeShader.SetFloat("trampleStrength", trampleStrength);
        
        // Blade
        m_ComputeShader.SetInt("_NumSourceVertices", m_Count);
        
        m_ComputeShader.SetFloat("bladeHeight", height);
        m_ComputeShader.SetFloat("bladeWeight", width);
        m_ComputeShader.SetFloat("bladeHeightOffset", rd_height);
        m_ComputeShader.SetFloat("bladeWeightOffset", rd_width);
        m_ComputeShader.SetFloat("maxBend", maxBend * Mathf.PI / 180);
        
        m_ComputeShader.SetVector("wind", wind);
        
        m_ComputeShader.SetFloat("_MinFadeDist", minFadeDistance);
        m_ComputeShader.SetFloat("_MaxFadeDist", maxFadeDistance);
        
        m_Material.SetVector("_AABB",new Vector4(m_LocalBounds.center.x, m_LocalBounds.center.y, m_LocalBounds.center.z, 0f));

    }

    private void SetGrassDataUpdate()
    {
        // 每一帧都需要更新
        m_ComputeShader.SetFloat(ID_time, Time.time);
        m_ComputeShader.SetInt("_NumSourceVertices", grassVisibleIDList.Count);
        // 设置多交互物体
        if (tramplers.Count > 0)
        {
            Vector4[] positions = new Vector4[tramplers.Count];
            for (int i = 0; i < tramplers.Count; i++)
            {
                if(tramplers[i].trampler == null) continue;
                positions[i] = new Vector4(tramplers[i].trampler.position.x, tramplers[i].trampler.position.y, tramplers[i].trampler.position.z,
                    tramplers[i].trampleRadius);
            }
            m_ComputeShader.SetVectorArray(ID_tramplePos, positions);
            m_ComputeShader.SetFloat(ID_trampleLength, tramplers.Count);
        }
        
        // 传入摄像机坐标
        if (m_MainCamera != null)
            m_ComputeShader.SetVector(ID_camreaPos, m_MainCamera.transform.position);
#if UNITY_EDITOR
        else if (view != null && view.camera != null)
        {
            m_ComputeShader.SetVector(ID_camreaPos, view.camera.transform.position);
        }

#endif
    }
    
    void Update()
    {
        // If in edit mode, we need to update the shaders each Update to make sure settings changes are applied
        // Don't worry, in edit mode, Update isn't called each frame
        if (!Application.isPlaying)
        {
            OnDisable();
            OnEnable();
        }
        if (!m_Initialized)
        {
            return;
        }

        GetFrustumData();
        
        SetGrassDataUpdate();
        
        m_OutputBuffer.SetCounterValue(0);
        m_argsBuffer.SetData(argsBufferReset);

        Calculate_Thread_Size();
        
        if (grassVisibleIDList.Count > 0)
        {
            // make sure the compute shader is dispatched even when theres very little grass
            m_DispatchSize += 1;
        }
        
        if (m_DispatchSize > 0)
        {        
            m_ComputeShader.Dispatch(m_ID_GrassKernel, m_DispatchSize, 1, 1);

            //====================================================================================
            // Final DrawMeshInstancedIndirect 
            //====================================================================================
            // appendBuffer用来存储最终需要渲染的实例数据
            // 对应args的第二个uint
            ComputeBuffer.CopyCount(m_OutputBuffer, m_argsBuffer, 4); 
        
            Graphics.DrawMeshInstancedIndirect(blade, 0, m_Material, m_LocalBounds, m_argsBuffer);
        }

    }
    
    // 组件被禁用
    private void OnDisable()
    {
        ReleaseResources();
    }

    private void ReleaseResources()
    {
        if (m_Initialized)
        {
            if (Application.isPlaying)
            {
                if (m_ComputeShader != null) Destroy(m_ComputeShader);
                if (m_Material != null) Destroy(m_Material);
            }
            else
            {
                if (m_ComputeShader != null) DestroyImmediate(m_ComputeShader);
                if (m_Material != null) DestroyImmediate(m_Material);
            }
            m_InputBuffer?.Release();
            m_argsBuffer?.Release();
            m_OutputBuffer?.Release();
            m_VisibleIDBuffer?.Release();
        }
        m_Initialized = false;
    }
    
    void GrassFastList(int count)
    {
        grassVisibleIDList = Enumerable.Range(0, count).ToArray().ToList();
    }


    public void UpdateBounds()
    {
        m_LocalBounds = new Bounds(grassData[0].position, Vector3.one);
        for (int i = 0; i < grassData.Count; i++)
        {
            Vector3 target = grassData[i].position;
            m_LocalBounds.Encapsulate(target);
        }
    }

    void SetupQuadTree()
    {
        // if (full)
        // {
            cullingTree = new CullingTreeNode(m_LocalBounds, CullingDepth);
            cullingTree.RetrieveAllLeaves(leaves);
            //add the id of each grass point into the right cullingtree
            for (int i = 0; i < grassData.Count; i++)
            {
                cullingTree.FindLeaf(grassData[i].position, i);
            }

            cullingTree.ClearEmpty();
        // }
        // else
        // {
        //     // just make everything visible while editing grass
        //     GrassFastList(grassData.Count);
        //     m_VisibleIDBuffer.SetData(grassVisibleIDList);
        // }
    }
    
    void GetFrustumData()
    {
        if (m_MainCamera == null)
        {
            return;
        }

        // Check if the camera's position or rotation has changed
        if (m_cachedCamRot == m_MainCamera.transform.rotation && m_cachedCamPos == m_MainCamera.transform.position && Application.isPlaying)
        {
            return; // Camera hasn't moved, no need for frustum culling
        }

        // Cache camera position and rotation for next frame
        m_cachedCamPos = m_MainCamera.transform.position;
        m_cachedCamRot = m_MainCamera.transform.rotation;

        // Get frustum data from the main camera without modifying far clip plane
        GeometryUtility.CalculateFrustumPlanes(m_MainCamera, cameraFrustumPlanes);
        
        // Perform full frustum culling
        cameraOriginalFarPlane = m_MainCamera.farClipPlane;
        m_MainCamera.farClipPlane = maxFadeDistance;
        BoundsListVis.Clear();
        grassVisibleIDList.Clear();
        cullingTree.RetrieveLeaves(cameraFrustumPlanes, BoundsListVis, grassVisibleIDList);
        m_VisibleIDBuffer.SetData(grassVisibleIDList);
        m_MainCamera.farClipPlane = cameraOriginalFarPlane;
    }

    void OnDrawGizmos()
    {
        // Gizmos.DrawWireCube(m_LocalBounds.center, m_LocalBounds.size);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(m_LocalBounds.center, m_LocalBounds.size);
        Gizmos.color = new Color(0, 1, 0, 1f);
        for (int i = 0; i < BoundsListVis.Count; i++)
        {
            Gizmos.DrawWireCube(BoundsListVis[i].center, BoundsListVis[i].size);
        }
    }
}

[System.Serializable]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind
    .Sequential)]
public struct GrassBlade {
    public Vector3 position; // 世界坐标位置
    public float padding;
    public GrassBlade( Vector3 pos) {
        position.x = pos.x;
        position.y = pos.y;
        position.z = pos.z;
        padding = 0;
    }
}

[System.Serializable]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind
    .Sequential)]
public struct trampleStruct {
    public Transform trampler;
    public float trampleRadius;
    public trampleStruct(Transform t) {
        trampler = t;
        trampleRadius = 1.5f;
    }
}
