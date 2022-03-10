//the deviation from flat space for an alcubierre metric

// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles

#define FLAT_SPACE float4x4(-1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1)

#define GEODESIC_DT 0.1

struct SpaceTimeRay
{
    float4 P; //4momentum vector
    float4 Q; //space time position
    float4 Qt; //proper time position derivative
    float3 Dir; //space direction
};

float4x4 AlcubierreMetricDeviation(float4 q)
{
    //warp params
    const float R = 1.0;
    const float sig = 35.0; 
    const float v = 1.1;

    float x = v*q.x;
    float r = sqrt(sqr(q.y - x) + q.z*q.z + q.w*q.w);
    float f = 0.5*(tanh(sig*(r + R)) - tanh(sig*(r - R)))/tanh(sig*R);
    float gtt = v*v*f*f;
    float gxt = -v*f;
    return float4x4(gtt, gxt,  0,  0,
                    gxt,   0,  0,  0,
                    0,     0,  0,  0,
                    0,     0,  0,  0);
}



//the deviation from flat space for a kerr metric
float4x4 KerrMetricDeviation(float4 q)
{
    //kerr params
    const float a = 0.99;
    const float m = 1.0;
    const float Q = 0.0;

    //Kerr metric in Kerr-Schild coordinates 
    float3 p = q.yzw;
    float rho = dot(p,p) - a*a;
    float r2 = 0.5*(rho + sqrt(rho*rho + 4.0*a*a*p.z*p.z));
    float r = sqrt(r2);
    float4 k = float4(1.0, (r*p.x + a*p.y)/(r2 + a*a), (r*p.y - a*p.x)/(r2 + a*a), p.z/r);
    float f = r2*(2.0*m*r - Q*Q)/(r2*r2 + a*a*p.z*p.z);
    float4 k1 = f * k;
    return float4x4(k.x*k1, k.y*k1, k.z*k1, k.w*k1);    
}

float4x4 KerrMetricInverseDeviation(float4 q)
{
    //kerr params
    const float a = 0.99;
    const float m = 1.0;
    const float Q = 0.0;
    //inverse of kerr metric in Kerr-Schild coordinates 
    float3 p = q.yzw;
    float rho = dot(p,p) - a*a;
    float r2 = 0.5*(rho + sqrt(rho*rho + 4.0*a*a*p.z*p.z));
    float r = sqrt(r2);
    float r2aa = 1.0/(r2 + a * a);
    float4 k = float4(1.0, (r*p.x + a*p.y)*r2aa, (r*p.y - a*p.x)*r2aa, p.z/r);

    float4 etak = float4(k.x, -k.yzw);
    float f = r2*(2.0*m*r - Q*Q)/((1.0 + dot(etak, k))*(r2*r2 + a*a*p.z*p.z));
    etak *= f;

    return float4x4(-k.x*etak, k.y*etak, k.z*etak, k.w*etak);  
}

bool StopCondition(float3 p, float3 dir)
{
    //kerr params
    const float a = 0.99;
    const float m = 1.0;
    const float Q = 0.0;

    float rho = dot(p,p) - a*a;
    float r = sqrt(0.5*(rho + sqrt(rho*rho + 4.0*a*a*p.z*p.z)));
    float rdotq = dot(p, dir);
    //under the horizon
    if (r < 1.0) return true;
    else return false;
}

//space time metric
float4x4 G(float4 q)
{
    return FLAT_SPACE + KerrMetricDeviation(q);
}

float4x4 Ginv(float4 q)
{
    return FLAT_SPACE + KerrMetricInverseDeviation(q);
}

//lagrangian
float L(float4 qt, float4x4 g)
{
    return dot(mul(g,qt),qt);
}

float L(float4 qt, float4 q)
{
    float4x4 g = G(q);
    return L(qt, g);
}

//vector to momentum at point q
float4 Vec2P(float3 v, float3 x)
{
    return 2.0*mul(G(float4(0.0, x)),float4(1.0, v)); 
}

float4 P2Vec(float4 p, float4 q)
{
    return mul(Ginv(q),p);
}

float H(float4 p, float4x4 ginv)
{
    //general geodesic hamiltonian
    return dot(mul(ginv,p),p);
}

float H(float4 p, float4 q)
{
    return H(p, Ginv(q));
}

//Hamilton's equations simple integration step
bool hstep(inout SpaceTimeRay r, float max_distance)
{    
    const float2 dq = float2(0, NUMERICAL_DERIVATIVE_EPS);

    float4x4 ginv = Ginv(r.Q);
    float4 qt = mul(ginv,r.P);
    float3 dHdq = (float3(L(qt,r.Q+dq.xyxx),L(qt,r.Q+dq.xxyx),L(qt,r.Q+dq.xxxy))-H(r.P, ginv))/dq.y; 

    float dt = clamp(GEODESIC_DT * length(r.P.yzw) / (length(dHdq) + 0.0001), 0.01, 0.5*max_distance/(length(qt.yzw) + 0.0001));

    r.Qt = qt;
    r.Dir = normalize(qt.yzw);
    r.P += float4(0, dHdq)*dt;
    r.Q += 2.0*qt*dt;

    return StopCondition(r.Q.yzw, r.Dir);
}