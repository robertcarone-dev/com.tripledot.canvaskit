#if SHADERPASS != SHADERPASS_CUSTOM_UI
#error SHADERPASS_CUSTOM_UI_is_not_correctly_defined
#endif

float4 GetVertexPositionNDC(float3 positionCS)
{
    float3 ndc = positionCS * 0.5f;
    float4 positionNDC;
    positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x);
    positionNDC.zw = float2(positionCS.z, 1.0);
    return positionNDC;
}

float4 TransformModelToHClip(float3 positionOS)
{
    return mul(unity_MatrixVP, mul(unity_ObjectToWorld, float4(positionOS, 1.0)));
}

Varyings BuildVaryings(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS);

    output.positionCS = TransformWorldToHClip(positionWS);

    #if UNITY_UV_STARTS_AT_TOP
    output.positionCS = TransformModelToHClip(input.positionOS);
    output.texCoord0.y = 1.0 - output.texCoord0.y;
    #endif

    #ifdef ATTRIBUTES_NEED_NORMAL
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    #else
    float3 normalWS = float3(0.0, 0.0, 0.0);
    #endif

    #ifdef ATTRIBUTES_NEED_TANGENT
    float4 tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w);
    #endif

    #if defined(HAVE_VERTEX_MODIFICATION)
    ApplyVertexModification(input, normalWS, positionWS, _TimeParameters.xyz);
    #endif

    #ifdef VARYINGS_NEED_POSITION_WS
    output.positionWS = positionWS;
    #endif

    #ifdef VARYINGS_NEED_NORMAL_WS
    output.normalWS = normalWS;
    #endif

    #ifdef VARYINGS_NEED_TANGENT_WS
    output.tangentWS = tangentWS;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD0) || defined(VARYINGS_DS_NEED_TEXCOORD0)
    output.texCoord0 = input.uv0;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD1) || defined(VARYINGS_DS_NEED_TEXCOORD1)
    #ifdef UNITY_UI_CLIP_RECT
    float2 pixelSize = output.positionCS.w;
    pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    output.texCoord1 = float4(input.positionOS.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));
    #endif
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD2) || defined(VARYINGS_DS_NEED_TEXCOORD2)
    output.texCoord2 = input.uv2;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD3) || defined(VARYINGS_DS_NEED_TEXCOORD3)
    output.texCoord3 = input.uv3;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD4) || defined(VARYINGS_DS_NEED_TEXCOORD4)
    output.texCoord4 = input.uv4;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD5) || defined(VARYINGS_DS_NEED_TEXCOORD5)
    output.texCoord5 = input.uv5;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD6) || defined(VARYINGS_DS_NEED_TEXCOORD6)
    output.texCoord6 = input.uv6;
    #endif

    #if defined(VARYINGS_NEED_TEXCOORD7) || defined(VARYINGS_DS_NEED_TEXCOORD7)
    output.texCoord7 = input.uv7;
    #endif

    #if defined(VARYINGS_NEED_COLOR) || defined(VARYINGS_DS_NEED_COLOR)
    output.color = input.color;
    #endif

    #ifdef VARYINGS_NEED_SCREENPOSITION
    output.screenPosition = GetVertexPositionNDC(output.positionCS);
    #endif

    return output;
}

PackedVaryings vert(Attributes input)
{
    Varyings output = BuildVaryings(input);
    PackedVaryings packedOutput = PackVaryings(output);
    return packedOutput;
}

void ApplyCanvasKitBlendMode(inout half4 color)
{
    #if defined(_CANVASKIT_BLEND_MATERIAL_OVERRIDE)
    int blendMode = (int)(_Blend + 0.5);
    if (blendMode == 1)
        color.rgb *= color.a;
    else if (blendMode == 3)
        color.rgb = lerp(half3(1.0, 1.0, 1.0), color.rgb, color.a);
    #elif defined(_CANVASKIT_BLEND_PREMULTIPLY)
    color.rgb *= color.a;
    #elif defined(_CANVASKIT_BLEND_MULTIPLY)
    color.rgb = lerp(half3(1.0, 1.0, 1.0), color.rgb, color.a);
    #endif
}

half4 frag(PackedVaryings packedInput) : SV_TARGET
{
    Varyings unpacked = UnpackVaryings(packedInput);
    UNITY_SETUP_INSTANCE_ID(unpacked);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(unpacked);
    SurfaceDescriptionInputs surfaceDescriptionInputs = BuildSurfaceDescriptionInputs(unpacked);
    SurfaceDescription surfaceDescription = SurfaceDescriptionFunction(surfaceDescriptionInputs);

    const half alphaPrecision = 0xff;
    const half invAlphaPrecision = 1.0 / alphaPrecision;
    unpacked.color.a = round(unpacked.color.a * alphaPrecision) * invAlphaPrecision;

    half alpha = surfaceDescription.Alpha;
    half4 color = half4(surfaceDescription.BaseColor + surfaceDescription.Emission, alpha);

    #if !defined(HAVE_VFX_MODIFICATION) && !defined(_DISABLE_COLOR_TINT)
    color *= unpacked.color;
    #endif

    #ifdef UNITY_UI_CLIP_RECT
    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(unpacked.texCoord1.xy)) * unpacked.texCoord1.zw);
    color.a *= m.x * m.y;
    #endif

    #if _ALPHATEST_ON
    clip(alpha - surfaceDescription.AlphaClipThreshold);
    #endif

    ApplyCanvasKitBlendMode(color);

    return color;
}
