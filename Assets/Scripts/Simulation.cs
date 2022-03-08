using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Simulation : MonoBehaviour
{
    private ComputeShader SimulationShader;
    private int PointGenID;
    private int GridRasterID;

    private CommandBuffer cb;
    private ComputeBuffer pointBuffer;
    private ComputeBuffer manipData;

    private const int GROUPTHREADS = 1024;

    private int WorkGroups;

    private int iFrame = 0;
    public bool genpoints = true;
    public int pointCount = 1024 * 1024;

    [Range(0.0f, 10.0f)] public float param1 = 0.0f;

    public PointCloudRender pointCloud;
    public List<Manipulator> manipulators;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
       
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }

    void OnDrawGizmos()
    {
        OnDrawGizmosSelected();
    }

    // Start is called before the first frame update
    void Start()
    {
        SimulationShader = Resources.Load("Simulation") as ComputeShader;
        PointGenID = SimulationShader.FindKernel("PointGen");
        GridRasterID = SimulationShader.FindKernel("GridRaster");

        WorkGroups = pointCount / GROUPTHREADS + 1;

        pointBuffer = new ComputeBuffer(pointCount * 2, sizeof(float) * 4);

        manipData = new ComputeBuffer(manipulators.Count, sizeof(float) * 4);

        cb = new CommandBuffer
        {
            name = "Simulation"
        };

        pointCloud.Initialize(pointCount, pointBuffer);
    }

    // Update is called once per frame
    void Update()
    {
        if (genpoints || iFrame == 0)
        {
            Vector4[] manips = new Vector4[manipulators.Count];
            for(int i = 0; i < manipulators.Count; i++)
            {
                Vector3 pos = manipulators[i].transform.position;
                manips[i] = new Vector4(pos.x, pos.y, pos.z, manipulators[i].Force);
            }

            manipData.SetData(manips);

            cb.Clear();
            cb.SetComputeIntParam(SimulationShader, "iFrame", iFrame);
            cb.SetComputeIntParam(SimulationShader, "ManipCount", manipulators.Count);
            cb.SetComputeIntParam(SimulationShader, "Length", pointCount);
            cb.SetComputeFloatParam(SimulationShader, "Param1", param1);

            cb.SetComputeVectorParam(SimulationShader, "BBoxCenter", transform.position);
            cb.SetComputeVectorParam(SimulationShader, "BBoxSize", transform.lossyScale);

            cb.SetComputeBufferParam(SimulationShader, PointGenID, "Points", pointBuffer);
            cb.SetComputeBufferParam(SimulationShader, PointGenID, "Manipulators", manipData);
            cb.DispatchCompute(SimulationShader, PointGenID, WorkGroups, 1, 1);

            Graphics.ExecuteCommandBuffer(cb);
            iFrame++;
        }
    }
}
