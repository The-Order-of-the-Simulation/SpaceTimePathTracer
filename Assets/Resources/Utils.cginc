#define PI 3.14159265
#define TWO_PI 6.28318530718

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

float3 cpow3(float3 z, float po)
{
    float r = length(z);
    float b = po * acos(z.y / r);
    float a = po * atan2(z.x, z.z);
    return pow(r, po) * float3(sin(b) * sin(a), cos(b), sin(b) * cos(a));
}

float Kernel2(float2 dx)
{
    float2 f = max(1.5 - abs(dx), 0.0);
    float2 k = min(max(0.75 - dx * dx, 0.5), 0.5 * f * f);
    return k.x * k.y;
}

//integer log2 of n
uint uint_log2(uint n)
{
#define S(k) if (n >= (1 << k)) { i += k; n >>= k; }
    int i = -(n == 0); 
    S(16); S(8); S(4); S(2); S(1); 
    return i;
#undef S
}

#define LEVEL_0 float2(96, 54)

//give the screen space bounding box of the object and get the higrid id
uint higrid_enc(float2 bmin, float bmax)
{
    float2 c = LEVEL_0 * (bmax + bmin) / 2.0;
    float2 s = LEVEL_0 * (bmax - bmin);
    float m = max(s.x, s.y); //get largest size of the AABB
    uint lvl = uint_log2(max(1.0 / m, 1.0)); //get level of the hierarchy
    //get id of the pixel on this level
    return lvl;
}

float3 fmod3(float3 x, float3 y)
{
    return x - y * floor(x / y);
}

//3d morton curve
uint spreadBy2bits(uint x)
{
	x = (x | (x << 16)) & 0x030000FF;
	x = (x | (x << 8)) & 0x0300F00F;
	x = (x | (x << 4)) & 0x030C30C3;
	x = (x | (x << 2)) & 0x09249249;
}

uint contractBy2bits(uint x)
{
	x = x & 0x09249249;
	x = ((x >> 2) | x) & 0x030C30C3;
	x = ((x >> 4) | x) & 0x0300F00F;
	x = ((x >> 8) | x) & 0x030000FF;
	x = ((x >> 16) | x) & 0x000003FF;
	return x;
}

uint morton3D(uint3 x)
{
	return spreadBy2bits(x.x) | (spreadBy2bits(x.y) << 1) | (spreadBy2bits(x.z) << 2);
}

uint3 inverseMorton3D(uint index)
{
	return uint3(contractBy2bits(index), contractBy2bits(index >> 1), contractBy2bits(index >> 2));
}

//2d morton curve
uint spreadBy1bit(uint x)
{
    x &= 0x0000ffff;                  // x = ---- ---- ---- ---- fedc ba98 7654 3210
    x = (x ^ (x <<  8)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
    x = (x ^ (x <<  4)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
    x = (x ^ (x <<  2)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
    x = (x ^ (x <<  1)) & 0x55555555; // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
    return x;
}

uint contractBy1bit(uint x)
{
    x &= 0x55555555;                  // x = -f-e -d-c -b-a -9-8 -7-6 -5-4 -3-2 -1-0
    x = (x ^ (x >>  1)) & 0x33333333; // x = --fe --dc --ba --98 --76 --54 --32 --10
    x = (x ^ (x >>  2)) & 0x0f0f0f0f; // x = ---- fedc ---- ba98 ---- 7654 ---- 3210
    x = (x ^ (x >>  4)) & 0x00ff00ff; // x = ---- ---- fedc ba98 ---- ---- 7654 3210
    x = (x ^ (x >>  8)) & 0x0000ffff; // x = ---- ---- ---- ---- fedc ba98 7654 3210
    return x;
}

uint morton2D(uint2 x)
{
    return spreadBy1bit(x.x) | (spreadBy1bit(x.y) << 1);
}

uint2 inverseMorton2D(uint index)
{
	return uint2(contractBy1bit(index), contractBy1bit(index >> 1));
}

//rainbow

float3 bump3y(float3 x, float3 yoffset)
{
    float3 y = float3(1., 1., 1.) - x * x;
    y = clamp(y - yoffset, 0, 1);
    return y;
}

float3 spectral_zucconi(float w)
{
    // w: [400, 700]
    // x: [0,   1]
    float x = clamp((w - 400.0) / 300.0, 0, 1);

    const float3 cs = float3(3.54541723, 2.86670055, 2.29421995);
    const float3 xs = float3(0.69548916, 0.49416934, 0.28269708);
    const float3 ys = float3(0.02320775, 0.15936245, 0.53520021);

    return bump3y(cs * (x - xs), ys);
}

#define uintscale 512.0
uint float2uint(float x)
{
    return uint(x * uintscale);
}

float uint2float(uint x)
{
    return float(x) / uintscale;
}

float sdBox(float3 p, float3 b)
{
	float3 q = abs(p) - b;
	return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}
