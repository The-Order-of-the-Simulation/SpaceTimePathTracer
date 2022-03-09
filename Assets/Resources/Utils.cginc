#define PI 3.14159265
#define TWO_PI 6.28318530718

#define MAX_INT 0x7FFFFFFF
#define MAX_UINT 0xFFFFFFFF
#define MAX_FLOAT (3.402823466e+38f)

float2 CameraResolution;
float3 CameraPosition;
float4x4 View;
float4x4 ProjectionInverse;

struct Ray
{
    float2 pixel;
    float3 origin;
    float3 direction;
};

Ray computeCameraRay(uint2 pix)
{
    float4 clipPosition = float4(float2(2.0, -2.0) * (float2(pix) / CameraResolution.xy - 0.5), 1.0f, 1.0f);
    float3 localDirection = mul(ProjectionInverse, clipPosition).xyz;
	float3 worldDirection = mul(transpose(View), float4(localDirection, 0.0f)).xyz;
    Ray ray;
    ray.origin = CameraPosition;
    ray.direction = normalize(worldDirection);
    ray.pixel = float2(pix);
	return ray;
}

uint4 state; //internal RNG state 

uint4 pcg4d(inout uint4 v) 
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    v = v ^ (v >> 16u);
    v.x += v.y * v.w; v.y += v.z * v.x; v.z += v.x * v.y; v.w += v.y * v.z;
    return v;
}

float rand() {return float(pcg4d(state).x) / float(0xffffffffu);}
float2 rand2() {return float2(pcg4d(state).xy) / float(0xffffffffu);}
float3 rand3() {return float3(pcg4d(state).xyz) / float(0xffffffffu);}
float4 rand4() {return float4(pcg4d(state)) / float(0xffffffffu);}

float2 nrand2(float sigma, float2 mean)
{
    float2 Z = rand2();
    return mean + sigma * sqrt(-2.0 * log(Z.x)) * float2(cos(TWO_PI * Z.y), sin(TWO_PI * Z.y));
}

float2 cmul(float2 x, float2 y)
{
    return float2(x.x * y.x - x.y * y.y, x.x * y.y + x.y * y.x);
}

float2 cpow(float2 x, float po)
{
    float arg = atan2(x.y, x.x);
    return pow(abs(length(x)), po) * float2(cos(po * arg), sin(po * arg));
}

float2 disk()
{
    float2 r = rand2();
    return float2(sin(TWO_PI * r.x), cos(TWO_PI * r.x)) * sqrt(r.y);
}

//https://iquilezles.org/www/articles/distfunctions/distfunctions.htm

float sdBox(float3 p, float3 b)
{
    float3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float sdSphere(float3 pos, float R)
{
    return length(pos) - R;
}

float sdPlane(float3 pos, float3 d)
{
    return dot(pos, d);
}

float sdTorus(float3 pos, float2 t)
{
    float2 q = float2(length(pos.xz)-t.x,pos.y);
    return length(q)-t.y;
}

float sdVerticalCapsule( float3 pos, float h, float r )
{
    pos.y -= clamp( pos.y, 0.0, h );
    return length( pos ) - r;
}

float sdCappedCylinder( float3 pos, float h, float r )
{
    float2 d = abs(float2(length(pos.xz),pos.y)) - float2(h,r);
    return min(max(d.x,d.y),0.0) + length(max(d,0.0));
}

float sdEllipsoid(in float3 p, in float3 r)
{
    float k0 = length(p / r);
    float k1 = length(p / (r * r));
    return k0 * (k0 - 1.0) / k1;
}

float4 Conjugate(float4 quat)
{
    return quat * float4(-1, -1, -1, 1);
}

float3 RotateVector(float4 quat, float3 vec)
{
    return vec + 2.0 * cross(cross(vec, quat.xyz) + quat.w * vec, quat.xyz);
}

struct SDFObjectData
{
    float3 position;
    int Type;

    float3 scale;
    int Material;

    float4 rotation;
};

int SDFObjectCount;
StructuredBuffer<SDFObjectData> SDFObjects;

#define SDF_SPHERE 0
#define SDF_BOX 1
#define SDF_CAPSULE 2
#define SDF_TORUS 3
#define SDF_CYLINDER 4

float ComputeSDF(float3 pos, int id)
{
    float3 relativePos = RotateVector(SDFObjects[id].rotation, pos - SDFObjects[id].position);

    float3 Scale = SDFObjects[id].scale;

    float sdf = MAX_FLOAT;
    switch (SDFObjects[id].Type)
    {
    default:
        return sdf;
    case SDF_SPHERE:
        sdf = sdEllipsoid(relativePos, 0.5 * Scale);
        break;
    case SDF_BOX:
        sdf = sdBox(relativePos, 0.5 * Scale);
        break;
    case SDF_CAPSULE:
        sdf = sdVerticalCapsule(relativePos, Scale.y*0.5, Scale.x*0.5);
        break;
    case SDF_TORUS:
        sdf = sdTorus(relativePos, float2(Scale.y, Scale.x*0.5));
        break;
    case SDF_CYLINDER:
        sdf = sdCappedCylinder(relativePos, Scale.x*0.5, Scale.y);
        break;
    }

    return sdf;
}

float SceneSDF(float3 pos, inout int ID)
{
    float sdf = MAX_FLOAT;
    ID = 0;

    for (int i = 0; i < SDFObjectCount; i++)
    {
        float newsdf = ComputeSDF(pos, i);
        if (newsdf < sdf)
        {
            sdf = newsdf;
            ID = i;
        }
    }

    return sdf;
}

