#define PI 3.14159265
#define TWO_PI 6.28318530718

#define MAX_INT 0x7FFFFFFF
#define MAX_UINT 0xFFFFFFFF
#define MAX_FLOAT (3.402823466e+38f)

#define NUMERICAL_DERIVATIVE_EPS 0.01

float2 CameraResolution;
float3 CameraPosition;
float4x4 View;
float4x4 ProjectionInverse;

struct Ray
{
    float3 origin;
    float distance;
    float3 direction;
};

Ray computeCameraRay(float2 pix)
{
    float4 clipPosition = float4(float2(2.0, -2.0) * (pix / CameraResolution.xy - 0.5), 1.0f, 1.0f);
    float3 localDirection = mul(ProjectionInverse, clipPosition).xyz;
	float3 worldDirection = mul(transpose(View), float4(localDirection, 0.0f)).xyz;
    Ray ray;
    ray.origin = CameraPosition;
    ray.direction = normalize(worldDirection);
    ray.distance = 0;
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

void InitializeRand(uint frame, uint2 pix)
{
    state = uint4(frame, pix, 0);
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

float3 udir()
{
    float2 rng = rand2();
    float phi = 2.*PI*rng.x;
    float cos_theta = 2.*rng.y-1.0;
    float sin_theta = sqrt(1.0-cos_theta*cos_theta);
    float cos_phi = cos(phi);
    float sin_phi = sin(phi);
    return float3(cos_phi*sin_theta, sin_phi*sin_theta, cos_theta);
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

float3 Normal(float3 p, int i) 
{
    const float h = NUMERICAL_DERIVATIVE_EPS; 
    const float2 k = float2(1,-1);
    return normalize( k.xyy*ComputeSDF( p + k.xyy*h, i) + 
                      k.yyx*ComputeSDF( p + k.yyx*h, i) + 
                      k.yxy*ComputeSDF( p + k.yxy*h, i) + 
                      k.xxx*ComputeSDF( p + k.xxx*h, i) );
}

struct Material
{
    float3 color;
    float roughness;
    float metal;
};

//optimized inverse of symmetric matrix
float4x4 inverse_sym(float4x4 m) {
	float n11 = m[0][0], n12 = m[1][0], n13 = m[2][0], n14 = m[3][0];
	float n22 = m[1][1], n23 = m[2][1], n24 = m[3][1];
	float n33 = m[2][2], n34 = m[3][2];
	float n44 = m[3][3];

	float t11 = 2.0 * n23 * n34 * n24 - n24 * n33 * n24 - n22 * n34 * n34 - n23 * n23 * n44 + n22 * n33 * n44;
	float t12 = n14 * n33 * n24 - n13 * n34 * n24 - n14 * n23 * n34 + n12 * n34 * n34 + n13 * n23 * n44 - n12 * n33 * n44;
	float t13 = n13 * n24 * n24 - n14 * n23 * n24 + n14 * n22 * n34 - n12 * n24 * n34 - n13 * n22 * n44 + n12 * n23 * n44;
	float t14 = n14 * n23 * n23 - n13 * n24 * n23 - n14 * n22 * n33 + n12 * n24 * n33 + n13 * n22 * n34 - n12 * n23 * n34;

	float det = n11 * t11 + n12 * t12 + n13 * t13 + n14 * t14;
	float idet = 1.0f / det;

	float4x4 ret;

	ret[0][0] = t11 * idet;
	ret[0][1] = (n24 * n33 * n14 - n23 * n34 * n14 - n24 * n13 * n34 + n12 * n34 * n34 + n23 * n13 * n44 - n12 * n33 * n44) * idet;
	ret[0][2] = (n22 * n34 * n14 - n24 * n23 * n14 + n24 * n13 * n24 - n12 * n34 * n24 - n22 * n13 * n44 + n12 * n23 * n44) * idet;
	ret[0][3] = (n23 * n23 * n14 - n22 * n33 * n14 - n23 * n13 * n24 + n12 * n33 * n24 + n22 * n13 * n34 - n12 * n23 * n34) * idet;

	ret[1][0] = ret[0][1];
	ret[1][1] = (2.0 * n13 * n34 * n14 - n14 * n33 * n14 - n11 * n34 * n34 - n13 * n13 * n44 + n11 * n33 * n44) * idet;
	ret[1][2] = (n14 * n23 * n14 - n12 * n34 * n14 - n14 * n13 * n24 + n11 * n34 * n24 + n12 * n13 * n44 - n11 * n23 * n44) * idet;
	ret[1][3] = (n12 * n33 * n14 - n13 * n23 * n14 + n13 * n13 * n24 - n11 * n33 * n24 - n12 * n13 * n34 + n11 * n23 * n34) * idet;

	ret[2][0] = ret[0][2];
	ret[2][1] = ret[1][2];
    ret[2][2] = (2.0 * n12 * n24 * n14 - n14 * n22 * n14 - n11 * n24 * n24 - n12 * n12 * n44 + n11 * n22 * n44) * idet;
	ret[2][3] = (n13 * n22 * n14 - n12 * n23 * n14 - n13 * n12 * n24 + n11 * n23 * n24 + n12 * n12 * n34 - n11 * n22 * n34) * idet;

	ret[3][0] = ret[0][3];
	ret[3][1] = ret[1][3];
	ret[3][2] = ret[2][3];
	ret[3][3] = (2.0 * n12 * n23 * n13 - n13 * n22 * n13 - n11 * n23 * n23 - n12 * n12 * n33 + n11 * n22 * n33) * idet;

	return ret;
}

float sqr(float x)
{
    return x * x;
}