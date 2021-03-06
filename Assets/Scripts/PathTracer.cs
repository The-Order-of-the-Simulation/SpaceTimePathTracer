using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using UnityEngine;
using System.Runtime.InteropServices;

public class PathTracer : MonoBehaviour
{
    const int RENDER_THREADBLOCK = 16;
    class RenderBuffers
    {
        public RenderTexture Target1;
        public RenderTexture Target2;
        public int Swap = 0;

        public void SwapBuffers()
        {
            Swap = 1 - Swap;
        }

        public RenderTexture GetFront()
        {
            return (Swap == 0) ? Target1 : Target2;
        }

        public RenderTexture GetBack()
        {
            return (Swap == 0) ? Target2 : Target1;
        }
    }

    struct SDFObjectData
    {
        public Vector3 position;
        public int Type;

        public Vector3 scale;
        public int Material;

        public Quaternion rotation;
    }

    struct SDFMaterialData
    {
        public Vector3 Color;
        public float Roughness;
        public Vector3 Glow;
        public float Metal;
    }

    public List<SDFObject> objects;
    public List<SDFMaterial> materials;
    [ColorUsage(true, true)]
    public Color BackgroundColor;

    public Cubemap background;

    private ComputeBuffer SDFObjects;
    private ComputeBuffer SDFMaterials;
    private ComputeBuffer Metrics;

    private ComputeShader RenderShaders;

    //kernel IDs
    private int RenderID;

    private Dictionary<Camera, RenderBuffers> Buffers = new Dictionary<Camera, RenderBuffers>();
    private Dictionary<Camera, CommandBuffer> CommandBuffers = new Dictionary<Camera, CommandBuffer>();
    private List<Camera> Cameras = new List<Camera>();

    private Mesh quad;
    public Material material;
    public float exposure = 1.0f;
    [Range(0.0f, 1.0f)] public float acc = 0.0f;
    private int iFrame = 0;

    public int IntDivideCeil(int a, int b)
    {
        return (a + b - 1) / b;
    }

    private bool isEnabled = false;

    private void StartRender()
    {
        if(!isEnabled)
        {
            int SDFObjSize = Marshal.SizeOf(typeof(SDFObjectData));
            SDFObjects = new ComputeBuffer(objects.Count, SDFObjSize);
            int SDFMatSize = Marshal.SizeOf(typeof(SDFMaterialData));
            SDFMaterials = new ComputeBuffer(materials.Count, SDFMatSize);

            RenderShaders = Resources.Load("RenderShaders") as ComputeShader;
            RenderID = RenderShaders.FindKernel("Render");

            quad = PrimitiveHelper.GetPrimitiveMesh(PrimitiveType.Quad);
            Camera.onPreRender += RenderCallBack;

            isEnabled = true;
        }
    }

    private void OnEnable()
    {
        StartRender();
    }

    // Start is called before the first frame update
    void Start()
    {
        StartRender();
    }

    private bool CreateTexture(ref RenderTexture texture, Vector2Int resolution, FilterMode filterMode, int depth, RenderTextureFormat format,
                                           bool enableRandomWrite = true)
    {
        if (texture == null || texture.width != resolution.x || texture.height != resolution.y)
        {
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
            texture = new RenderTexture(resolution.x, resolution.y, depth, format);
            texture.enableRandomWrite = enableRandomWrite;
            texture.filterMode = filterMode;
            texture.Create();
            return true;
        }
        return false;
    }


    void RenderCallBack(Camera cam)
    {
        if (!isEnabled) return;

        CameraEvent cameraEvent = (cam.actualRenderingPath == RenderingPath.Forward) ? CameraEvent.BeforeForwardAlpha : CameraEvent.AfterLighting;

        CommandBuffer[] cbs = cam.GetCommandBuffers(cameraEvent);
        CommandBuffer cb;

        if (cbs.Length == 0)  //create command buffer and add it the camera
        {
            cb = new CommandBuffer
            {
                name = "PathTracer"
            };
            cam.AddCommandBuffer(cameraEvent, cb);
            CommandBuffers[cam] = cb;
            Cameras.Add(cam);
        }
        else //get old command buffer
        {
            cb = cbs[0];
        }


        if (!Buffers.ContainsKey(cam) || Buffers[cam].Target1.width != cam.pixelWidth || Buffers[cam].Target1.height != cam.pixelHeight)
        {
            bool isDirty = false;

            RenderBuffers renderBuffers;
            renderBuffers = new RenderBuffers();
            Vector2Int textureResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            isDirty = CreateTexture(ref renderBuffers.Target1, textureResolution, FilterMode.Point, 0, RenderTextureFormat.ARGBFloat) || isDirty;
            isDirty = CreateTexture(ref renderBuffers.Target2, textureResolution, FilterMode.Point, 0, RenderTextureFormat.ARGBFloat) || isDirty;

            Buffers[cam] = renderBuffers;
        }
      
        Matrix4x4 View = cam.worldToCameraMatrix;
        Matrix4x4 Projection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        Matrix4x4 ProjectionInverse = Projection.inverse;

        cb.Clear();

        cb.SetComputeIntParam(RenderShaders, "iFrame", iFrame);
        cb.SetComputeIntParam(RenderShaders, "SDFObjectCount", objects.Count);
        cb.SetComputeFloatParam(RenderShaders, "Accumulation", acc);
        cb.SetComputeVectorParam(RenderShaders, "BackgroundColor", new Vector3(BackgroundColor.r, BackgroundColor.g, BackgroundColor.b));

        cb.SetComputeVectorParam(RenderShaders, "CameraResolution", new Vector2(cam.pixelWidth, cam.pixelHeight));
        cb.SetComputeVectorParam(RenderShaders, "CameraPosition", cam.transform.position);
        cb.SetComputeMatrixParam(RenderShaders, "View", View);
        cb.SetComputeMatrixParam(RenderShaders, "ProjectionInverse",ProjectionInverse);

        cb.SetComputeBufferParam(RenderShaders, RenderID, "SDFObjects", SDFObjects);
        cb.SetComputeBufferParam(RenderShaders, RenderID, "SDFMaterials", SDFMaterials);
        cb.SetComputeTextureParam(RenderShaders, RenderID, "Background", background);
        cb.SetComputeTextureParam(RenderShaders, RenderID, "Previous", Buffers[cam].GetBack());
        cb.SetComputeTextureParam(RenderShaders, RenderID, "Target", Buffers[cam].GetFront());

        cb.DispatchCompute(RenderShaders, RenderID, IntDivideCeil(cam.pixelWidth, RENDER_THREADBLOCK), IntDivideCeil(cam.pixelHeight, RENDER_THREADBLOCK), 1);
        
        cb.SetGlobalVector("Resolution", new Vector2(cam.pixelWidth, cam.pixelHeight));
        cb.SetGlobalTexture("Render", Buffers[cam].GetFront());
        cb.SetGlobalFloat("exposure", exposure);
        cb.SetGlobalInt("iFrame", iFrame);
        cb.DrawMesh(quad, Matrix4x4.identity, material, 0);

        Buffers[cam].SwapBuffers();
    }

    void UploadObjectData()
    {
        SDFObjectData[] objs = new SDFObjectData[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            SDFObjectData objData;
            objData.position = objects[i].transform.position;
            objData.Type = (int)objects[i].type;
            objData.scale = objects[i].transform.lossyScale;
            objData.Material = objects[i].Material;
            objData.rotation = objects[i].transform.rotation;

            objs[i] = objData;
        }

        SDFObjects.SetData(objs);
    }

    void UploadMaterialData()
    {
        SDFMaterialData[] objs = new SDFMaterialData[materials.Count];
        for (int i = 0; i < materials.Count; i++)
        {
            SDFMaterialData matData;
            matData.Color = new Vector3(materials[i].Color.r, materials[i].Color.g, materials[i].Color.b);
            matData.Glow = new Vector3(materials[i].Glow.r, materials[i].Glow.g, materials[i].Glow.b);
            matData.Roughness = materials[i].Roughness;
            matData.Metal = materials[i].Metal;
            objs[i] = matData;
        }

        SDFMaterials.SetData(objs);
    }

    // Update is called once per frame
    void Update()
    {
        UploadObjectData();
        UploadMaterialData();

        iFrame++;
    }

    private void OnDisable()
    {
        foreach (var cam in Cameras)
        {
            CommandBuffers[cam].Clear();
        }

        Camera.onPreRender -= RenderCallBack;

        if (SDFObjects != null)
            SDFObjects.Release();

        if (SDFMaterials != null)
            SDFMaterials.Release();

        Buffers.Clear();
        CommandBuffers.Clear();
        Cameras.Clear();

        isEnabled = false;
    }
}
