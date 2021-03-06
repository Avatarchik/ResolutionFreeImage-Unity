#ifndef RFI_PATTERN_DOT_REGULAR_CGINC
#define RFI_PATTERN_DOT_REGULAR_CGINC

// ## Calculate EXACT distance as possible as you can in pattern functions. ##

// #include "ShapeUtils.cginc"

float dotRegularDistance(float2 uv, float diameter) {
    float2 p = frac(uv) - 0.5;
    return diameter * -0.5 + length(p);
}

fixed4 dotRegularPattern(float2 uv, float es, float diameter, fixed4 col0, fixed4 col1) {
    float d = dotRegularDistance(uv, diameter);
    // float3 rgb = float3(0.0);
    // rgb.xy = p;
    // rgb.xy = ap;
    // rgb = float3(-d, d, exp(-d*d*10000.0));
    return lerp(col0, col1, edgeWeight(d, es));
}

#endif