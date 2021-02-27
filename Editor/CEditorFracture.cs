using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MeshProcess;

public class CEditorFracture : EditorWindow
{
    private enum FractureTypes
    {
        Voronoi,
        Clustered,
        Slicing,
        Skinned,
        Plane,
        Cutout
    }

    private enum GenerateType
    {
        Preview,
        Generate,
        CreateAsset
    }

    [MenuItem("Window/Fracture")]
    public static void OpenEditor()
    {
        EditorWindow.GetWindow<CEditorFracture>("Fracture");
    }


    public const int raycastSteps = 8;
    public const string objectPostfix = "_Fractured";

    private FractureTypes fractureType = FractureTypes.Voronoi;

    public GameObject point;
    public GameObject source;
    public Material insideMaterial;
    public bool islands = false;
    public bool previewColliders = false;
    public float previewDistance = 0.5f;
    public int totalChunks = 5;
    public int seed = 0;

    public float objectMass = 100;
    public float jointBreakForce = 1000;
    public AudioClip onBreakSound;
    public uint VHACDResolution = 100000;

    //TODO: serialize
    //public SlicingConfiguration sliceConf;

    Vector3Int slices = Vector3Int.one;
    float offset_variations = 0;
    float angle_variations = 0;
    float amplitude = 0;
    float frequency = 1;
    int octaveNumber = 1;
    int surfaceResolution = 2;

    public int clusters = 5;
    public int sitesPerCluster = 5;
    public float clusterRadius = 1;

    private GameObject previewObject;

    private void OnEnable()
    {
        point = (GameObject)Resources.Load("Point");
    }

    private void OnSelectionChange()
    {
        Repaint();
    }

    protected void OnGUI()
    {
        if (Application.isPlaying)
        {
            GUILayout.Label("PLAY MODE ACTIVE", GUI.skin.box, GUILayout.ExpandWidth(true));
            return;
        }

        if (GUILayout.Button("Clean Up Objects")) CleanUp(true);

        GUILayout.Space(20);

        EditorGUI.BeginChangeCheck();
        source = EditorGUILayout.ObjectField("Source", source, typeof(GameObject), true) as GameObject;
        if (EditorGUI.EndChangeCheck())
        {
            CleanUp(true);
        }

        if (GUILayout.Button("Set selected object as Source") && Selection.activeGameObject != null)
        {
            //hachunk to not select preview chunks OR Points OR Destructible :)
            if (Selection.activeGameObject.GetComponent<ChunkInfo>() == null && Selection.activeGameObject.hideFlags != HideFlags.NotEditable && Selection.activeGameObject.GetComponent<Destructible>() == null)
            {
                if (Selection.activeGameObject.GetComponent<MeshFilter>() != null)
                {
                    CleanUp(false);

                    source = Selection.activeGameObject;

                    if (source.activeInHierarchy)
                        source.SetActive(false);

                    _createObject(GenerateType.Preview);

                    Selection.activeGameObject = previewObject;
                }
                if (Selection.activeGameObject.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                {
                    source = Selection.activeGameObject.GetComponentInChildren<SkinnedMeshRenderer>().gameObject;

                    if (source.activeInHierarchy)
                        source.SetActive(false);

                    _createObject(GenerateType.Preview);

                    Selection.activeGameObject = previewObject;
                }
            }
        }

        if (!source) return;

        EditorGUI.BeginChangeCheck();
        insideMaterial = (Material)EditorGUILayout.ObjectField("Inside Material", insideMaterial, typeof(Material), false);
        if (EditorGUI.EndChangeCheck())
        {
            _createObject(GenerateType.Preview);
        }

        if (!insideMaterial) EditorGUILayout.HelpBox("If inside material is not assigned, the same material as material of selected object will be used", MessageType.Info);

        EditorGUI.BeginChangeCheck();
        fractureType = (FractureTypes)EditorGUILayout.EnumPopup("Fracture Type", fractureType);
        if (EditorGUI.EndChangeCheck())
            _createObject(GenerateType.Preview);

        EditorGUILayout.BeginHorizontal();
        islands = EditorGUILayout.Toggle("Islands", islands);
        previewColliders = EditorGUILayout.Toggle("Preview Colliders", previewColliders);
        EditorGUILayout.EndHorizontal();

        seed = (int)EditorApplication.timeSinceStartup;

        EditorGUI.BeginChangeCheck();
        previewDistance = EditorGUILayout.Slider("Preview Chunks Distance", previewDistance, 0, 5);
        if (EditorGUI.EndChangeCheck())
        {
            UpdatePreview();
        }

        VHACDResolution = (uint)EditorGUILayout.IntSlider(new GUIContent("VHACD Resolution"), (int)VHACDResolution, 100000, 64000000);

        EditorGUILayout.Space(10);

        onBreakSound = (AudioClip)EditorGUILayout.ObjectField("On Break Sound", onBreakSound, typeof(AudioClip), false);
        objectMass = EditorGUILayout.FloatField("Object Mass", objectMass);
        jointBreakForce = EditorGUILayout.FloatField("Joint Break Force", jointBreakForce);

        bool canCreate = false;

        EditorGUI.BeginChangeCheck();
        if (fractureType == FractureTypes.Voronoi) canCreate = GUI_Voronoi();
        if (fractureType == FractureTypes.Clustered) canCreate = GUI_Clustered();
        if (fractureType == FractureTypes.Slicing) canCreate = GUI_Slicing();
        if (fractureType == FractureTypes.Skinned) canCreate = GUI_Skinned();
        if (fractureType == FractureTypes.Plane) canCreate = GUI_Plane();
        if (fractureType == FractureTypes.Cutout) canCreate = GUI_Cutout();
        if(EditorGUI.EndChangeCheck())
        {
            _createObject(GenerateType.Preview);
        }

        if (canCreate)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Generate"))
            {
                _createObject(GenerateType.Generate);
            }

            if (GUILayout.Button("Create Prefab"))
            {
                _createObject(GenerateType.CreateAsset);
            }
            GUILayout.EndHorizontal();
        }
    }

    private void _createObject(GenerateType generateType)
    {
        NvBlastExtUnity.setSeed(seed);

        CleanUp(false);

        GameObject cs = generateType == GenerateType.Preview ? new GameObject(generateType == GenerateType.Preview ? $"{source.name}{objectPostfix}Preview" : $"{source.name}{objectPostfix}") : Instantiate(source);
        if (generateType != GenerateType.Preview)
            cs.name = $"{source.name}{objectPostfix}";

        cs.SetActive(true);
        cs.transform.position = Vector3.zero;
        cs.transform.rotation = Quaternion.identity;
        cs.transform.localScale = Vector3.one;

        if (generateType == GenerateType.Preview)
            previewObject = cs;

        Mesh ms = null;

        Material[] mats = new Material[2];
        
        mats[1] = insideMaterial ? insideMaterial : source.GetComponent<MeshRenderer>().sharedMaterial;

        MeshFilter mf = source.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = source.GetComponent<SkinnedMeshRenderer>();

        if (mf != null)
        {
            mats[0] = source.GetComponent<MeshRenderer>().sharedMaterial;
            ms = source.GetComponent<MeshFilter>().sharedMesh;
        }
        if (smr != null)
        {
            mats[0] = smr.sharedMaterial;
            smr.gameObject.transform.position = Vector3.zero;
            smr.gameObject.transform.rotation = Quaternion.identity;
            smr.gameObject.transform.localScale = Vector3.one;
            ms = new Mesh(); 
            smr.BakeMesh(ms);
            //ms = smr.sharedMesh;
        }

        if (ms == null)
            return;

        NvMesh mymesh = new NvMesh(ms.vertices, ms.normals, ms.uv, ms.vertexCount, ms.GetIndices(0), (int)ms.GetIndexCount(0));

        // cleaner = new NvMeshCleaner();
        //cleaner.cleanMesh(mymesh);

        NvFractureTool fractureTool = new NvFractureTool();
        fractureTool.setRemoveIslands(islands);
        fractureTool.setSourceMesh(mymesh);

        if (fractureType == FractureTypes.Voronoi) _Voronoi(fractureTool, mymesh);
        if (fractureType == FractureTypes.Clustered) _Clustered(fractureTool, mymesh);
        if (fractureType == FractureTypes.Slicing) _Slicing(fractureTool, mymesh);
        if (fractureType == FractureTypes.Skinned) _Skinned(fractureTool, mymesh);
        if (fractureType == FractureTypes.Plane) _Plane(fractureTool, mymesh);
        if (fractureType == FractureTypes.Cutout) _Cutout(fractureTool, mymesh);

        fractureTool.finalizeFracturing();

        NvLogger.Log("Chunk Count: " + fractureTool.getChunkCount());

        if (generateType == GenerateType.CreateAsset)
        {
            if (!AssetDatabase.IsValidFolder("Assets/NvBlast Prefabs")) AssetDatabase.CreateFolder("Assets", "NvBlast Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/NvBlast Prefabs/Meshes")) AssetDatabase.CreateFolder("Assets/NvBlast Prefabs", "Meshes");
            if (!AssetDatabase.IsValidFolder("Assets/NvBlast Prefabs/Fractured")) AssetDatabase.CreateFolder("Assets/NvBlast Prefabs", "Fractured");

            FileUtil.DeleteFileOrDirectory("Assets/NvBlast Prefabs/Meshes/" + source.name);
            AssetDatabase.Refresh();
            AssetDatabase.CreateFolder("Assets/NvBlast Prefabs/Meshes", source.name);
        }

        if (previewColliders)
            source.SetActive(false);

        for (int i = 1; i < fractureTool.getChunkCount(); i++)
        {
            GameObject chunk = new GameObject("Chunk" + i);
            chunk.transform.parent = cs.transform;

            MeshFilter chunkmf = chunk.AddComponent<MeshFilter>();
            MeshRenderer chunkmr = chunk.AddComponent<MeshRenderer>();

            chunkmr.sharedMaterials = mats;

            NvMesh outside = fractureTool.getChunkMesh(i, false);
            NvMesh inside = fractureTool.getChunkMesh(i, true);

            Mesh m = outside.toUnityMesh();
            m.subMeshCount = 2;
            m.SetIndices(inside.getIndexes(), MeshTopology.Triangles, 1);
            chunkmf.sharedMesh = m;

            if (generateType == GenerateType.CreateAsset)
            {
                AssetDatabase.CreateAsset(m, "Assets/NvBlast Prefabs/Meshes/" + source.name + "/Chunk" + i + ".asset");
            }

            if (generateType == GenerateType.Preview) chunk.AddComponent<ChunkInfo>();

            if (generateType == GenerateType.Generate || generateType == GenerateType.CreateAsset || previewColliders)
            {
                VHACD vhacd = chunk.AddComponent<VHACD>();
                vhacd.m_parameters.m_resolution = VHACDResolution;
                List<Mesh> meshes = vhacd.GenerateConvexMeshes();

                foreach (Mesh mesh in meshes)
                {
                    MeshCollider collider = chunk.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                    collider.convex = true;
                }
                DestroyImmediate(vhacd);
            }

            chunk.transform.position = Vector3.zero;
            chunk.transform.rotation = Quaternion.identity;
        }

        if (generateType == GenerateType.Generate || generateType == GenerateType.CreateAsset)
        {
            cs.AddComponent<FractureObject>().jointBreakForce = jointBreakForce;

            for (int i = 0; i < cs.transform.childCount; i++)
            {
                Transform chunk = cs.transform.GetChild(i);
                chunk.gameObject.layer = LayerMask.NameToLayer("Fracture");
                Bounds chunkBounds = new Bounds(chunk.GetComponent<MeshRenderer>().bounds.center, chunk.GetComponent<MeshRenderer>().bounds.size * 1.5f);

                chunk.GetComponent<MeshRenderer>().enabled = false;

                ChunkRuntimeInfo chunkInfo = chunk.gameObject.AddComponent<ChunkRuntimeInfo>();
            }
        }

        if (generateType == GenerateType.CreateAsset)
        {
            GameObject p = PrefabUtility.SaveAsPrefabAsset(cs, "Assets/NvBlast Prefabs/Fractured/" + source.name + "_fractured.prefab");

            GameObject fo;

            bool skinnedMesh = false;
            if (source.GetComponent<SkinnedMeshRenderer>() != null) skinnedMesh = true;

            if (skinnedMesh)
                fo = Instantiate(source.transform.root.gameObject);
            else
                fo = Instantiate(source);

            Destructible d = fo.AddComponent<Destructible>();
            d.fracturedPrefab = p;

            bool hasCollider = false;
            if (fo.GetComponent<MeshCollider>() != null) hasCollider = true;
            if (fo.GetComponent<BoxCollider>() != null) hasCollider = true;
            if (fo.GetComponent<SphereCollider>() != null) hasCollider = true;
            if (fo.GetComponent<CapsuleCollider>() != null) hasCollider = true;

            if (!hasCollider)
            {
                BoxCollider bc = fo.AddComponent<BoxCollider>();
                if (skinnedMesh)
                {
                    Bounds b = source.GetComponent<SkinnedMeshRenderer>().bounds;
                    bc.center = new Vector3(0,.5f,0);
                    bc.size = b.size;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(fo, "Assets/NvBlast Prefabs/" + source.name + ".prefab");
            DestroyImmediate(fo);
        }

        cs.transform.position = source.transform.position;
        cs.transform.rotation = source.transform.rotation;
        cs.transform.localScale = source.transform.localScale;

        UpdatePreview();
    }

    private void _Cutout(NvFractureTool fractureTool, NvMesh mesh)
    {
    }

    private void _Plane(NvFractureTool fractureTool, NvMesh mesh)
    {
    }

    private void _Skinned(NvFractureTool fractureTool, NvMesh mesh)
    {
        SkinnedMeshRenderer smr = source.GetComponent<SkinnedMeshRenderer>();
        NvVoronoiSitesGenerator sites = new NvVoronoiSitesGenerator(mesh);
        sites.boneSiteGeneration(smr);
        fractureTool.voronoiFracturing(0, sites);
    }

    private void _Slicing(NvFractureTool fractureTool, NvMesh mesh)
    {
        SlicingConfiguration conf = new SlicingConfiguration();
        conf.slices = slices;
        conf.offset_variations = offset_variations;
        conf.angle_variations = angle_variations;

        conf.noise.amplitude = amplitude;
        conf.noise.frequency = frequency;
        conf.noise.octaveNumber = octaveNumber;
        conf.noise.surfaceResolution = surfaceResolution;

        fractureTool.slicing(0, conf, false);
    }

    private void _Clustered(NvFractureTool fractureTool, NvMesh mesh)
    {
        NvVoronoiSitesGenerator sites = new NvVoronoiSitesGenerator(mesh);
        sites.clusteredSitesGeneration(clusters, sitesPerCluster, clusterRadius);
        fractureTool.voronoiFracturing(0, sites);
    }

    private void _Voronoi(NvFractureTool fractureTool, NvMesh mesh)
    {
        NvVoronoiSitesGenerator sites = new NvVoronoiSitesGenerator(mesh);
        sites.uniformlyGenerateSitesInMesh(totalChunks);
        fractureTool.voronoiFracturing(0, sites);
    }

    private bool GUI_Voronoi()
    {
        GUILayout.Space(20);
        GUILayout.Label("VORONOI FRACTURE", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32));

        totalChunks = EditorGUILayout.IntSlider("Total Chunks", totalChunks, 2, 100);

        if (GUILayout.Button("Visualize Points"))
        {
            _Visualize();
        }
        return true;
    }

    private bool GUI_Clustered()
    {
        GUILayout.Space(20);
        GUILayout.Label("CLUSTERED VORONOI FRACTURE", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32));

        clusters = EditorGUILayout.IntSlider("Clusters", clusters, 1, 100);
        sitesPerCluster = EditorGUILayout.IntSlider("Sites", sitesPerCluster, 1, 100);
        clusterRadius = EditorGUILayout.FloatField("Radius", clusterRadius);

        if (GUILayout.Button("Visualize Points"))
        {
            _Visualize();
        }

        return true;
    }

    private bool GUI_Skinned()
    {
        GUILayout.Space(20);
        GUILayout.Label("SKINNED MESH VORONOI FRACTURE", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32));

        if (source.GetComponent<SkinnedMeshRenderer>() == null)
        {
            EditorGUILayout.HelpBox("Skinned Mesh Not Selected", MessageType.Error);
            return false;
        }

        if (source.transform.root.position != Vector3.zero)
        {
            EditorGUILayout.HelpBox("Root must be at 0,0,0 for Skinned Meshes", MessageType.Info);
            if (GUILayout.Button("FIX"))
            {
                source.transform.root.position = Vector3.zero;
                source.transform.root.rotation = Quaternion.identity;
                source.transform.root.localScale = Vector3.one;
            }

            return false;
        }

        if (GUILayout.Button("Visualize Points"))
        {
            _Visualize();
        }

        return true;
    }

    private bool GUI_Slicing()
    {
        GUILayout.Space(20);
        GUILayout.Label("SLICING FRACTURE", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32));

        slices = EditorGUILayout.Vector3IntField("Slices", slices);
        offset_variations = EditorGUILayout.Slider("Offset", offset_variations, 0, 1);
        angle_variations = EditorGUILayout.Slider("Angle", angle_variations, 0, 1);

        GUILayout.BeginHorizontal();
        amplitude = EditorGUILayout.FloatField("Amplitude", amplitude);
        frequency = EditorGUILayout.FloatField("Frequency", frequency);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        octaveNumber = EditorGUILayout.IntField("Octave", octaveNumber);
        surfaceResolution = EditorGUILayout.IntField("Resolution", surfaceResolution);
        GUILayout.EndHorizontal();

        return true;
    }

    private bool GUI_Plane()
    {
        GUILayout.Space(20);
        GUILayout.Label("PLANE FRACTURE", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32));

        GUILayout.Label("Coming Soon...");
        return false;
    }

    private bool GUI_Cutout()
    {
        GUILayout.Space(20);
        GUILayout.Label("CUTOUT FRACTURE", GUI.skin.box, GUILayout.ExpandWidth(true), GUILayout.MinHeight(32));

        GUILayout.Label("Coming Soon...");
        return false;
    }

    private void _Visualize()
    {
        NvBlastExtUnity.setSeed(seed);

        CleanUp(false);
        if (source == null) return;

        GameObject ps = new GameObject("POINTS");
        ps.transform.position = Vector3.zero;
        ps.transform.rotation = Quaternion.identity;
        ps.transform.localScale = Vector3.one;

        Mesh ms = null;

        MeshFilter mf = source.GetComponent<MeshFilter>();
        SkinnedMeshRenderer smr = source.GetComponent<SkinnedMeshRenderer>();

        if (mf != null)
        {
            ms = source.GetComponent<MeshFilter>().sharedMesh;
        }
        if (smr != null)
        {
            smr.gameObject.transform.position = Vector3.zero;
            smr.gameObject.transform.rotation = Quaternion.identity;
            smr.gameObject.transform.localScale = Vector3.one;
            ms = new Mesh();
            smr.BakeMesh(ms);
            //ms = smr.sharedMesh;
        }

        if (ms == null) return;

        NvMesh mymesh = new NvMesh(ms.vertices, ms.normals, ms.uv, ms.vertexCount, ms.GetIndices(0), (int)ms.GetIndexCount(0));

        NvVoronoiSitesGenerator sites = new NvVoronoiSitesGenerator(mymesh);
        if (fractureType == FractureTypes.Voronoi) sites.uniformlyGenerateSitesInMesh(totalChunks);
        if (fractureType == FractureTypes.Clustered) sites.clusteredSitesGeneration(clusters, sitesPerCluster, clusterRadius);
        if (fractureType == FractureTypes.Skinned) sites.boneSiteGeneration(smr);

        Vector3[] vs = sites.getSites();

        for (int i = 0; i < vs.Length; i++)
        {
            GameObject po = Instantiate(point, vs[i], Quaternion.identity, ps.transform);
            po.hideFlags = HideFlags.NotEditable;
        }

        ps.transform.rotation = source.transform.rotation;
        ps.transform.position = source.transform.position;
    }

    private void CleanUp(bool enableSource)
    {
        if(source)
            source.SetActive(enableSource);

        GameObject.DestroyImmediate(GameObject.Find("POINTS"));
        if (source)
            GameObject.DestroyImmediate(GameObject.Find($"{source.name}{objectPostfix}Preview"));
    }

    private void UpdatePreview()
    {
        if (previewObject == null) return;

        Transform[] ts = previewObject.transform.GetComponentsInChildren<Transform>();

        foreach (Transform t in ts)
        {
            ChunkInfo ci = t.GetComponent<ChunkInfo>();
            if (ci != null)
            {
                ci.UpdatePreview(previewDistance);
            }
        }
    }
}