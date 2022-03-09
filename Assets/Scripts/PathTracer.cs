using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using UnityEngine;
using System.Runtime.InteropServices;

public class PathTracer : MonoBehaviour
{
    const int RENDER_THREADBLOCK = 16;
    struct RenderBuffers
    {
        public RenderTexture Target1;
        public RenderTexture Target2;
        public int Swap;

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

    public List<SDFObject> objects;

    private ComputeBuffer SDFObjects;

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

    public bool isEnabled = false;

    private void StartRender()
    {
        if(!isEnabled)
        {
            int SDFObjSize = Marshal.SizeOf(typeof(SDFObjectData));
            SDFObjects = new ComputeBuffer(objects.Count, SDFObjSize);

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
                name = "PointCloudRender"
            };
            cam.AddCommandBuffer(cameraEvent, cb);
            CommandBuffers[cam] = cb;
            Cameras.Add(cam);
        }
        else //get old command buffer
        {
            cb = cbs[0];
        }

        bool isDirty = false;



        if (!Buffers.ContainsKey(cam) || Buffers[cam].Target1.width != cam.pixelWidth || Buffers[cam].Target1.height != cam.pixelHeight)
        {
            RenderBuffers renderBuffers;
            renderBuffers = new RenderBuffers();
            renderBuffers.Swap = 0;
            Vector2Int textureResolution = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
            isDirty = CreateTexture(ref renderBuffers.Target1, textureResolution, FilterMode.Point, 0, RenderTextureFormat.ARGBHalf) || isDirty;
            isDirty = CreateTexture(ref renderBuffers.Target2, textureResolution, FilterMode.Point, 0, RenderTextureFormat.ARGBHalf) || isDirty;

            Buffers[cam] = renderBuffers;
        }
      
        Matrix4x4 View = cam.worldToCameraMatrix;
        Matrix4x4 Projection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
        Matrix4x4 ProjectionInverse = Projection.inverse;

        cb.Clear();

        cb.SetComputeIntParam(RenderShaders, "iFrame", iFrame);
        cb.SetComputeIntParam(RenderShaders, "SDFObjectCount", objects.Count);
        cb.SetComputeFloatParam(RenderShaders, "Accumulation", acc);
        
        cb.SetComputeVectorParam(RenderShaders, "CameraResolution", new Vector2(cam.pixelWidth, cam.pixelHeight));
        cb.SetComputeVectorParam(RenderShaders, "CameraPosition", cam.transform.position);
        cb.SetComputeMatrixParam(RenderShaders, "View", View);
        cb.SetComputeMatrixParam(RenderShaders, "ProjectionInverse",ProjectionInverse);

        cb.SetComputeBufferParam(RenderShaders, RenderID, "SDFObjects", SDFObjects);
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

    // Update is called once per frame
    void Update()
    {
        SDFObjectData[] objs = new SDFObjectData[objects.Count];
        for (int i = 0; i < objects.Count; i++)
        {
            SDFObjectData objData; 
            objData.position = objects[i].transform.position;
            objData.Type = (int)objects[i].type;
            objData.scale = objects[i].transform.lossyScale;
            objData.Material = 0;
            objData.rotation = objects[i].transform.rotation;

            objs[i] = objData;
        }
        
        SDFObjects.SetData(objs);

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

        Buffers.Clear();
        CommandBuffers.Clear();
        Cameras.Clear();

        isEnabled = false;
    }
}
