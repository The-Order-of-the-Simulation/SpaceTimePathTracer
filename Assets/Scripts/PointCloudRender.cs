using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering;
using UnityEngine;

public class PointCloudRender : MonoBehaviour
{
    struct RenderBuffers
    {
        public ComputeBuffer gridPoint;
        public ComputeBuffer sortedPoints;
        public ComputeBuffer hashGrid;
        public ComputeBuffer atomicGrid;
        public ComputeBuffer accumulation;
    }

    private const int GROUPTHREADS = 1024;
    private const int THREADBLOCK = 8;

    private ComputeShader RenderShaders;

    //kernel IDs
    private int GenKeyValueID;
    private int WriteHashGridID;
    private int ClearHashGridID;
    private int RenderID;
    private int AtomicRasterizerID;
    private int ClearAtomicGridID;

    private ComputeBuffer pointBuffer;

    private Material screenSpaceRenderer;
    private Mesh quad;
    private int WorkGroups;
    public Material material;
    private int pointCount = 1024*1024;
    public float exposure = 1.0f;
    [Range(0.0f, 1.0f)] public float acc = 0.0f;

    private Dictionary<Camera, RenderBuffers> Buffers = new Dictionary<Camera, RenderBuffers>();
    private Dictionary<Camera, Vector2Int> Resolutions = new Dictionary<Camera, Vector2Int>();

    private int iFrame = 0;

    void RenderCallBack(Camera cam)
    {
        CameraEvent cameraEvent = (cam.actualRenderingPath == RenderingPath.Forward) ? CameraEvent.BeforeForwardAlpha : CameraEvent.AfterLighting;

        CommandBuffer[] cbs = cam.GetCommandBuffers(cameraEvent);
        CommandBuffer curcb;

        if (cbs.Length == 0)  //create command buffer and add it the camera
        {
            curcb = new CommandBuffer
            {
                name = "PointCloudRender"
            };
            cam.AddCommandBuffer(cameraEvent, curcb);
            Resolutions[cam] = new Vector2Int(0, 0);
        }
        else //get old command buffer
        {
            curcb = cbs[0];
        }

        Vector2Int oldresolution = Resolutions[cam];
        RenderBuffers curbuf;

        //resolution has changed
        //todo; point count has changed
        if (cam.pixelWidth != oldresolution.x || cam.pixelHeight != oldresolution.y)
        {
            Resolutions[cam] = new Vector2Int(cam.pixelWidth, cam.pixelHeight);
           
            if(Buffers.ContainsKey(cam))
            {
                //clean up buffers
                curbuf = Buffers[cam];
                curbuf.gridPoint.Dispose();
                curbuf.hashGrid.Dispose();
                curbuf.sortedPoints.Dispose();
                curbuf.accumulation.Dispose();
                curbuf.atomicGrid.Dispose();
            }
            else 
            {
                curbuf = new RenderBuffers();
            }

            curbuf.gridPoint = new ComputeBuffer(pointCount, sizeof(uint) * 2);
            curbuf.hashGrid = new ComputeBuffer(cam.pixelWidth * cam.pixelHeight, sizeof(uint) * 2);
            curbuf.sortedPoints = new ComputeBuffer(pointCount, sizeof(float) * 4);
            curbuf.accumulation = new ComputeBuffer(cam.pixelWidth * cam.pixelHeight, sizeof(float) * 4);
            curbuf.atomicGrid = new ComputeBuffer(cam.pixelWidth * cam.pixelHeight, sizeof(uint) * 4);
            Buffers[cam] = curbuf;
        }
        else
        {
            curbuf = Buffers[cam];
        }

        Matrix4x4 ViewProjection = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true) * cam.worldToCameraMatrix;
        Matrix4x4 ViewProjectionInverse = ViewProjection.inverse;

        curcb.Clear();

        int RTWG = (cam.pixelWidth * cam.pixelHeight) / GROUPTHREADS;

        curcb.SetComputeIntParam(RenderShaders, "iFrame", iFrame);
        curcb.SetComputeFloatParam(RenderShaders, "Accumulation", acc);
        curcb.SetComputeIntParam(RenderShaders, "Length", pointCount);
        
        curcb.SetComputeVectorParam(RenderShaders, "Resolution", new Vector2(cam.pixelWidth, cam.pixelHeight));
        curcb.SetComputeVectorParam(RenderShaders, "CameraPosition", cam.transform.position);
        curcb.SetComputeMatrixParam(RenderShaders, "ViewProjection", ViewProjection);
        curcb.SetComputeMatrixParam(RenderShaders, "ViewProjectionInverse", ViewProjectionInverse);

        //Atomic rasterizer
        curcb.SetComputeBufferParam(RenderShaders, AtomicRasterizerID, "AtomicGrid", curbuf.atomicGrid);
        curcb.SetComputeBufferParam(RenderShaders, AtomicRasterizerID, "Points", pointBuffer);
        curcb.DispatchCompute(RenderShaders, AtomicRasterizerID, WorkGroups, 1, 1);

        curcb.SetComputeBufferParam(RenderShaders, RenderID, "HashGrid", curbuf.hashGrid);
        curcb.SetComputeBufferParam(RenderShaders, RenderID, "AtomicGrid", curbuf.atomicGrid);
        curcb.SetComputeBufferParam(RenderShaders, RenderID, "SortedPoints", curbuf.sortedPoints);
        curcb.SetComputeBufferParam(RenderShaders, RenderID, "Target", curbuf.accumulation);
        curcb.DispatchCompute(RenderShaders, RenderID, cam.pixelWidth / THREADBLOCK, cam.pixelHeight / THREADBLOCK, 1);

        curcb.SetGlobalVector("Resolution", new Vector2(cam.pixelWidth, cam.pixelHeight));
        curcb.SetGlobalBuffer("Target", curbuf.accumulation);
        curcb.SetGlobalFloat("exposure", exposure);
        curcb.SetGlobalInt("iFrame", iFrame);
        curcb.DrawMesh(quad, Matrix4x4.identity, material, 0);
    }

    void Start()
    {
       
    }

    public void Initialize(int count, ComputeBuffer points)
    {
        pointCount = count;

        RenderShaders = Resources.Load("RenderShaders") as ComputeShader;
        GenKeyValueID = RenderShaders.FindKernel("GenKeyValue");
        WriteHashGridID = RenderShaders.FindKernel("WriteHashGrid");
        ClearHashGridID = RenderShaders.FindKernel("ClearHashGrid");
        RenderID = RenderShaders.FindKernel("Render");
        AtomicRasterizerID = RenderShaders.FindKernel("AtomicRasterizer");

        WorkGroups = pointCount / GROUPTHREADS + 1;

        quad = PrimitiveHelper.GetPrimitiveMesh(PrimitiveType.Quad);
        Camera.onPreRender += RenderCallBack;

        pointBuffer = points;
    }

    // Update is called once per frame
    void Update()
    {
        iFrame++;
    }
}
