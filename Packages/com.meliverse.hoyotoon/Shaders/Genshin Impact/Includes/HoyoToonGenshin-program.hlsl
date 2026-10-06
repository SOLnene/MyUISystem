// ====================================================================
// VERTEX SHADERS 
vs_out vs_model(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast all output values to zero to prevent potential errors
    UNITY_SETUP_INSTANCE_ID(v); 
    UNITY_INITIALIZE_OUTPUT(vs_out, o); 
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); 
    #if defined(use_vat)
        if(_VertexAnimType == 1)
        {
            mavuika_vat_vs(v.vertex, v.uv_1.xy, v.normal, v.v_col);
        }
        else if (_VertexAnimType == 2)
        {
            o.pos = v.vertex;

            // Transform vertex from object to world space, then to clip space
            float4 worldSpacePosition = mul(unity_ObjectToWorld, v.vertex);
            float4 clipSpacePosition = mul(UNITY_MATRIX_VP, worldSpacePosition);
            o.pos = clipSpacePosition;

            // Check if vertex should be hidden based on vertex color alpha
            bool shouldHideVertex = v.v_col.w == 0 || v.v_col.w == 1;
            shouldHideVertex = (_DisappearByVerColAlpha) ? shouldHideVertex : false;

            // Early return if vertex should be hidden
            if(shouldHideVertex)
            {
                o = (vs_out)0.0f;
                return o;
            }

            // Declare variables for vertex animation
            float4 vertexColor;
            float3 texCoord0;
            float3 texCoord1;
            float3 texCoord2;
            float4 texCoord3;
            float4 texCoord4;
            float4 tempVec0;
            uint4 tempUintVec0;
            bool2 tempBoolVec0;
            float4 tempVec1;
            float4 tempVec2;
            float3 normalizedTangent;
            float3 normalizedBitangent;
            float3 normalizedNormal;
            float3 tempVec4;
            uint3 tempUintVec4;
            float3 boundMin;
            float3 boundSize;
            float3 positionA;
            float3 positionB;
            bool isAutoPlayback;
            float2 frameUV;
            float currentFrameNormalized;
            bool isPositiveFrame;
            float frameTime;
            bool isNextFrameZero;
            float normalLength;
            float tangentLength;

            // Calculate vertical UV coordinate
            float verticalUV = (-v.uv_1.y) + 1.0;

            // Check if auto playback is enabled
            isAutoPlayback = 9.99999975e-06 < _IfAutoPlayback;

            // Calculate animation timing
            frameTime = _Time.y * _Speed;
            frameTime = frameTime * _HoudiniFPS;
            frameTime = frameTime / _FrameCount;
            frameTime = frac(frameTime);
            
            float frameIndex = frameTime * _FrameCount;
            float frameFraction = frac(frameIndex);
            frameIndex = floor(frameIndex);
            frameIndex = frameIndex / _FrameCount;

            // Handle negative frame indices
            isPositiveFrame = frameIndex >= (-frameIndex);
            frameIndex = frac(abs(frameIndex));
            frameUV.y = (isPositiveFrame) ? frameIndex : (-frameIndex);
            
            // Calculate next frame
            float nextFrameIndex = frameTime * _FrameCount + 1.0;
            nextFrameIndex = floor(nextFrameIndex);
            nextFrameIndex = nextFrameIndex / _FrameCount;

            isPositiveFrame = nextFrameIndex >= (-nextFrameIndex);
            nextFrameIndex = frac(abs(nextFrameIndex));
            frameUV.x = (isPositiveFrame) ? nextFrameIndex : (-nextFrameIndex);
            
            // Scale frame coordinates
            frameUV.xy = frameUV.xy * float2(_FrameCount, _FrameCount);

            // Check if next frame is zero
            isNextFrameZero = abs(frameUV.x) < 9.99999975e-06;

            // Prepare animation parameters
            tempVec1.z = (isNextFrameZero) ? 0.0 : frameFraction;
            frameUV.xy = frameUV.xy / float2(_FrameCount, _FrameCount);
            frameUV.xy = verticalUV + frameUV.xy;
            tempVec1.xy = (-frameUV.yx) + float2(1.0, 1.0);
            
            // Handle manual frame selection
            float manualFrameIndex = floor(_CurrentFrame);
            float prevManualFrameIndex = manualFrameIndex - 1.0;
            frameUV.xy = float2(manualFrameIndex, prevManualFrameIndex) / float2(_FrameCount, _FrameCount);

            isPositiveFrame = frameUV.y >= (-frameUV.y);
            frameIndex = frac(abs(frameUV.y));
            frameUV.y = (isPositiveFrame) ? frameIndex : (-frameIndex);

            isPositiveFrame = frameUV.x >= (-frameUV.x);
            frameIndex = frac(abs(frameUV.x));
            frameUV.x = (isPositiveFrame) ? frameIndex : (-frameIndex);
            
            frameUV.xy = frameUV.xy * float2(_FrameCount, _FrameCount);
            frameUV.xy = frameUV.xy / float2(_FrameCount, _FrameCount);
            
            tempVec0.w = verticalUV + frameUV.y;
            tempVec0.x = verticalUV + frameUV.x;
            tempVec2.xy = (-tempVec0.wx) + float2(1.0, 1.0);
            tempVec2.z = frac(_CurrentFrame);
            
            // Select animation parameters based on playback mode
            tempVec0.xyz = (isAutoPlayback) ? tempVec1.xyz : tempVec2.xyz;
            
            // Normalize vertex normal
            normalLength = dot(v.normal.xyz, v.normal.xyz);
            normalLength = rsqrt(normalLength);
            normalizedNormal = normalLength * v.normal.xyz;
            
            // Normalize vertex tangent
            tangentLength = dot(v.tangent.xyz, v.tangent.xyz);
            tangentLength = rsqrt(tangentLength);
            normalizedTangent = tangentLength * v.tangent.xyz;
            
            // Calculate bitangent
            normalizedBitangent = cross(normalizedNormal, normalizedTangent);
            
            // Sample position texture
            tempVec0.w = v.uv_1.x;
            float3 sampledPosition = _PosTex_A.SampleLevel(sampler_linear_repeat, tempVec0.wx, 0).xyz;
            
            // Decode position data
            float3 decodedPosition = sampledPosition * float3(255.0, 255.0, 255.0);
            tempUintVec4.xyz = uint3(decodedPosition);
            tempUintVec4.xyz = (uint3)tempUintVec4.xyz << int3(8, 8, 8);
            tempVec4.xyz = uint3(tempUintVec4.xyz);
            tempVec4.xyz = tempVec4.xyz * float3(1.52590219e-05, 1.52590219e-05, 1.52590219e-05);
            
            // Calculate position within bounds
            boundMin.x = _BoundMinX;
            boundMin.yz = float2(_BoundMinY, _BoundMinZ);
            boundSize.xyz = float3(_BoundMaxX, _BoundMaxY, _BoundMaxZ) - boundMin.xyz;
            positionA.xyz = tempVec4.xyz * boundSize.xyz + boundMin.xyz;

            // Handle interframe interpolation if enabled
            if(_IfInterframeInterp) {
                float3 nextFramePosition = _PosTex_A.SampleLevel(sampler_linear_repeat, tempVec0.wy, 0).xyz;
                float3 decodedNextPosition = nextFramePosition * float3(255.0, 255.0, 255.0);
                tempUintVec0.xyw = uint3(decodedNextPosition);
                tempUintVec0.xyw = (uint3)tempUintVec0.xyw << int3(8, 8, 8);
                tempVec0.xyw = uint3(tempUintVec0.xyw);
                tempVec0.xyw = tempVec0.xyw * float3(1.52590219e-05, 1.52590219e-05, 1.52590219e-05);
                
                positionB.xyz = tempVec0.xyw * boundSize.xyz + boundMin.xyz;
                float3 positionDelta = positionB.xyz - positionA.xyz;
                tempVec0.xyz = positionDelta * tempVec0.zzz + positionA.xyz;
            } else {
                tempVec0.xyz = positionA.xyz;
            }
            
            // Calculate final vertex position
            float3 tangentOffset = normalizedBitangent * tempVec0.yyy;
            float3 combinedOffset = tempVec0.xxx * normalizedTangent + tangentOffset;
            float3 finalOffset = tempVec0.zzz * normalizedNormal + combinedOffset;
            float3 finalPosition = finalOffset + v.vertex.xyz;
            
            // Transform to clip space
            o.pos = UnityObjectToClipPos(finalPosition);
            
            // Calculate world space normal
            float3 worldNormal;
            worldNormal.x = dot(v.normal.xyz, unity_WorldToObject[0].xyz);
            worldNormal.y = dot(v.normal.xyz, unity_WorldToObject[1].xyz);
            worldNormal.z = dot(v.normal.xyz, unity_WorldToObject[2].xyz);
            
            
            o.normal.xyz = normalize(worldNormal);
            
            // Calculate view direction
            float3 worldPosition = unity_ObjectToWorld[3].xyz * v.vertex.www + finalPosition;
            float3 viewDirection = _WorldSpaceCameraPos.xyz - worldPosition;
            o.view = normalize(viewDirection);
            
            // Calculate screen position
            float2 screenPos = o.pos.xw * float2(0.5, 0.5);
            float projectionY = o.pos.y * _ProjectionParams.x;
            float2 projectionPos = float2(screenPos.x, projectionY * 0.5);
            texCoord4.xy = o.pos.zz + projectionPos;
            
            // Set output values
            o.v_col = v.v_col;
            o.uv_a = v.uv_0.xyxy;
            o.ss_pos = ComputeScreenPos(o.pos);
            return o;
        }
    #endif

    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    #if defined(_is_shadow)
    if(_UseHairShadow && (variant_selector == 4)) // if material type is bangs
    {
        // this shifts the bangs around so they appear to be moving like a shadow
        float4 ws_pos = mul(unity_ObjectToWorld, v.vertex);
        float3 vl = mul(_WorldSpaceLightPos0.xyz, UNITY_MATRIX_V) * (1.f / ws_pos.w);
        float3 offset_pos = ((vl * .001f) * float3(7,-1,5)) + v.vertex.xyz;
        v.vertex.xyz = offset_pos;
        o.pos = UnityObjectToClipPos(v.vertex);
    }
    else if(_UseHairShadow && (variant_selector == 1)) // if material type is face
    {
        o.pos = pos_wvp;
    }
    else
    {
        o.pos = float4(-90,-90,-90,1.0f); // if shadow pass, set position to a negative value so it doesn't render
    }
    #endif
    o.uv_a.xy = v.uv_0.xy;
    o.uv_a.zw = v.uv_1.xy;
    o.uv_b.xy = v.uv_2.xy;
    o.uv_b.zw = v.uv_3.xy;
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    o.tangent.xyz = mul((float3x3)unity_ObjectToWorld, v.tangent);
    o.tangent.w = v.tangent.w * unity_WorldTransformParams.w;
    o.view = camera_position() - mul(unity_ObjectToWorld, v.vertex).xyz;
    o.n_view.xyz = -mul(unity_ObjectToWorld, v.vertex).xyz + unity_ObjectToWorld[3].xyz;
    o.n_view = float4(_DummyFixedForNormal ? o.n_view : o.view, 1.0f);
    o.ws_pos =  v.vertex;
    o.ppos = o.pos;
    o.ss_pos = ComputeScreenPos(o.pos);
    o.v_col = v.v_col;
    o.light_pos = mul(_LightMatrix0, o.ws_pos);
    #if defined(faceishadow)
        // Face_SDF uses the interpolated vertex lighting term, not a renormalized fragment normal.
        o.native_face_ndotl = dot(normalize(o.normal), _WorldSpaceLightPos0.xyz);
    #endif

    // parallax shit
    // cringe
    float3 bitangent = cross(o.normal.xyz, o.tangent.xyz) * o.tangent.w;
    if(!_UseGlassSpecularToggle)
    {
        float3 five;
        float3 six;
        float3 parallax;

        float3 view = normalize(o.view);

        five.x = o.tangent.z;
        five.y = bitangent.x;
        five.z = o.normal.x;
        six.x = o.tangent.x;
        six.y = bitangent.z;
        six.z = o.normal.y;

        parallax = view.yyy * six;
        parallax = five * view.xxx + parallax;
        bitangent.x = o.tangent.y;
        bitangent.z = o.normal.z;
        parallax = bitangent * view.zzz + parallax;
        o.parallax = parallax; 
    }

    TRANSFER_SHADOW(o)

    return o; // output to pixel shader
}

vs_out vs_edge(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast all output values to zero to prevent potential errors
    if(_OutlineType ==  0.0f)
    {
        vs_out o = (vs_out)0.0f;
    }
    else
    {   
        float outlineWidth = v.v_col.w;
        float outline_tex = 1.0f;
        #if defined(use_outline)
            outline_tex = packed_channel_picker(sampler_linear_repeat, _OutlineTex, v.uv_0.xy, _OutlineWidthChannel) * _UseOutlineTex;
        #endif
        
        switch(_OutlineWidthSource * _UseOutlineTex)
        {
            case 0:
                outlineWidth = v.v_col.w;
                break;
            case 1:
                outlineWidth = outline_tex;
                break;
            case 2:
                outlineWidth = outline_tex * v.v_col.w;
                break;
        }
        float3 outline_normal = (_OutlineType == 1.0) ? v.normal : v.tangent.xyz;
        
        #if defined(use_vat)
            if(_VertexAnimType != 0)
            {
                mavuika_vat_vs(v.vertex, v.uv_1.xy, outline_normal, v.v_col.xyzw);
            }
        #endif
        
        float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);
        float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
        o.view = normalize(view);
        float3 ws_normal = mul(outline_normal, (float3x3)unity_ObjectToWorld);

        outline_normal = mul((float3x3)UNITY_MATRIX_MV, outline_normal);
        if(!_DisableZShift)outline_normal.z = 0.01f;
        outline_normal.xy = normalize(outline_normal.xyz).xy;

        if(!_FallbackOutlines)
        {
            float fov_matrix = unity_CameraProjection[1].y;

            float fov = _DisableFOVScalingOL ? 1.0f : 2.414f / fov_matrix; // may need to come back in and change this back to 1.0f
            // can't remember in what vrchat mode this was messing up 



            float depth = -wv_pos.z * fov;

            float2 adjs = (depth <  _OutlineWidthAdjustZs.y) ? _OutlineWidthAdjustZs.xy : _OutlineWidthAdjustZs.yz; 
            float2 scales = (depth <  _OutlineWidthAdjustZs.y) ? _OutlineWidthAdjustScales.xy : _OutlineWidthAdjustScales.yz; 
            
            float z_scale = depth + -(adjs.x);
            float2 z_something = float2((-adjs.x) + adjs.y, (-scales.x) + scales.y);
            z_something.x = max(z_something.x, 0.001f);
            z_scale = z_scale / z_something.x;
            z_scale = saturate(z_scale);
            z_scale = z_scale * z_something.y + scales.x;
            z_scale = _DisableDepthScaling ? 0.1f : z_scale;

            // the next 5 or so lines could be written in one line like the above 
            float outline_scale = (_OutlineWidth * 1.5f) * z_scale;
            outline_scale = outline_scale * 100.0f;
            outline_scale = outline_scale * _Scale;
            outline_scale = outline_scale * 0.414f;
            outline_scale = outline_scale * outlineWidth;
            // if(_DisableDepthScaling) outline_scale = 0.1f * outline_scale;
            #if defined(faceishadow)
                if(_UseFaceMapNew) outline_scale = outline_scale * _FaceMapTex.SampleLevel(sampler_linear_repeat, v.uv_0.xy, 0.0f).z;
            #endif

            float offset_depth = saturate(1.0f - depth);
            float max_offset = lerp(_MaxOutlineZOffset * 0.1, _MaxOutlineZOffset, offset_depth);

            float3 z_offset = (wv_pos.xyz) * (float3)max_offset * (float3)_Scale;
            // the above version of the line causes a floating point division by zero warning in unity even though it didnt used to do that
            // but it probably has something to do with the instancing support v added

            float blue = v.v_col.z + -0.5f; // never trust this fucking line
            // it always breaks things

            o.pos = wv_pos;
            o.pos.xyz = (o.pos.xyz + (z_offset)) + (outline_normal.xyz * outline_scale);
        }
        else
        {
            o.pos = wv_pos;
            o.pos.xyz = o.pos.xyz + (outline_normal.xyz * (_OutlineWidth * 100.0f * _Scale * 0.414f * outlineWidth));
        }

        o.ws_pos = o.pos;
        
        o.pos = mul(UNITY_MATRIX_P, o.pos);
        o.ss_pos = ComputeScreenPos(o.pos);
        o.normal = normalize(ws_normal);
        o.uv_a.xy = v.uv_0;
        o.uv_a.zw = v.uv_1;
        o.v_col = v.v_col;
    }
    return o;
}

vs_out vs_nyx(vs_in v)
{
    vs_out o = (vs_out)0.0f; // cast all output values to zero to prevent potential errors
    #if defined(nyx_outline)
    if(_OutlineType ==  0.0f)
    {
        vs_out o = (vs_out)0.0f;
    }
    else
    {
        float3 outline_normal = (_OutlineType == 1.0) ? v.normal : v.tangent.xyz;
        float4 wv_pos = mul(UNITY_MATRIX_MV, v.vertex);
        float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
        o.view = normalize(view);
        float3 ws_normal = mul(outline_normal, (float3x3)unity_ObjectToWorld);

        outline_normal = mul((float3x3)UNITY_MATRIX_MV, outline_normal);
        outline_normal.z = 0.01f;
        outline_normal.xy = normalize(outline_normal.xyz).xy;

        float outline_width = v.v_col.w;


        float something = wv_pos.z + wv_pos.y;
        something = something * _NyxStateOutlineVertAnimNoiseScale.x;
        float2 screen_pos;
        screen_pos.x = something * 0.5;
        screen_pos.y = wv_pos.x * _NyxStateOutlineVertAnimNoiseScale.y;
        float2 noise_uv = _Time.yy * _NyxStateOutlineVertAnimNoiseAnim;
        noise_uv = frac(noise_uv);
        noise_uv = noise_uv + screen_pos.xy;

        float noise_a = _NyxStateOutlineNoise.SampleLevel(sampler_linear_repeat, noise_uv, 0).y;
        
        float widthScale = _NyxStateOutlineWidthScale * _NyxStateOutlineWidthScaleRange.x;
        float nyx_outline_width = saturate((-wv_pos.y) * _NyxStateOutlineWidthScaleLerpHeightRange.z + (-_NyxStateOutlineWidthScaleLerpHeightRange.w));
        nyx_outline_width = nyx_outline_width * (_NyxStateOutlineWidthScaleRange.y * _NyxStateOutlineWidthScale + (-widthScale)) + widthScale;
        nyx_outline_width = (_NyxStateEnableOutlineWidthScaleHeightLerp) ? nyx_outline_width : _NyxStateOutlineWidthScale;

        widthScale = _NyxStateOutlineVertAnimScale * _NyxStateOutlineVertAnimScaleRange.x;
        float height_width = saturate((-wv_pos.y) * _NyxStateOutlineVertAnimScaleLerpHeightRange.z + (-_NyxStateOutlineVertAnimScaleLerpHeightRange.w));
        height_width = height_width * (_NyxStateOutlineVertAnimScaleRange.y * _NyxStateOutlineVertAnimScale + (-widthScale)) + widthScale;
        height_width = (_NyxStateEnableOutlineVertAnimScaleHeightLerp) ? height_width : _NyxStateOutlineVertAnimScale;

        float outline_height = noise_a * height_width + nyx_outline_width;

        float res_width = max((-_ScreenParams.y) + _NyxStateOutlineWidthVarietyWithResolution.x, 0.0f);
        res_width = min(res_width * _NyxStateOutlineWidthVarietyWithResolution.y,  _NyxStateOutlineWidthVarietyWithResolution.z) + 1.0f;


        outline_width = (outline_width * 0.005f) * (res_width * outline_height) * 0.5f;

        float3 normalized_pos = (normalize(wv_pos.xyz) * (float3)_MaxOutlineZOffset) * (float3)_Scale;

        o.normal = (_OutlineType == 1.0) ? v.normal : v.tangent.xyz;
        o.normal = mul((float3x3)unity_ObjectToWorld, o.normal.xyz);
        
        o.pos = wv_pos;
        o.pos.xyz = normalized_pos.xyz * (v.v_col.z + -0.5f) + o.pos.xyz;
        o.pos.xyz = outline_normal * outline_width + o.pos.xyz;

        o.ss_pos = ComputeScreenPos(o.pos);

        o.uv_a.xy = v.uv_0;
    }
    o.pos = mul(UNITY_MATRIX_P, o.pos);
    #endif
    return o;
}

shadow_out vs_shadow(shadow_in v)
{
    shadow_out o = (shadow_out)0.0f; // initialize so no funny compile errors
    float3 view = _WorldSpaceCameraPos.xyz - (float3)mul(v.vertex.xyz, unity_ObjectToWorld);
    o.view = normalize(view);
    o.normal = mul((float3x3)unity_ObjectToWorld, v.normal);
    float4 pos_ws  = mul(unity_ObjectToWorld, v.vertex);
    o.ws_pos = pos_ws;
    float4 pos_wvp = mul(UNITY_MATRIX_VP, pos_ws);
    o.pos = pos_wvp;
    o.uv_a = float4(v.uv_0.xy, v.uv_1.xy);
    TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
    return o;
}

// ====================================================================
// PIXEL SHADERS
float pupil_radial_depth(float2 uv)
{
    // Odette's original center/radius; this is procedural depth, not a height texture.
    float2 radialUV = (uv - 0.5f) / 0.46f;
    return pow(saturate(dot(radialUV, radialUV)), 0.5f * _DepthPower);
}

float2 pupil_refraction_uv(float2 uv, float3 viewInEye)
{
    bool originalEye = _UseOriginalEyeFrame > 0.5f;
    if(!originalEye && (abs(viewInEye.z) <= 0.0001f || _ParallaxScale <= 0.0f || viewInEye.z < 0.0f)) return uv;
    float2 rayUV = -viewInEye.xy * (_ParallaxScale / viewInEye.z);
    int steps = (int)ceil(16.0f - 12.0f * (originalEye ? abs(viewInEye.z) : saturate(abs(viewInEye.z))));
    float depthStep = 1.0f / steps;
    float upperDepth = 1.0f;
    float upperError = upperDepth - pupil_radial_depth(uv);
    if(!originalEye && upperError <= 0.01f) return uv;
    float lowerDepth = upperDepth;
    float lowerError = upperError;
    float2 coarseOffset = 0.0f;

    // Bracket the ray/depth crossing with 4-16 arithmetic steps; no texture reads in the search.
    [loop] for(int stepIndex = 1; stepIndex <= steps; ++stepIndex)
    {
        if(originalEye)
        {
            // The source steps even at an exact depth match, stopping only after crossing.
            if(lowerError < 0.0f) break;
            upperDepth = lowerDepth;
            upperError = lowerError;
            lowerDepth -= depthStep;
            coarseOffset += rayUV * depthStep;
        }
        else lowerDepth = 1.0f - (float)stepIndex / steps;
        lowerError = lowerDepth - pupil_radial_depth(uv + (originalEye ? coarseOffset : rayUV * (1.0f - lowerDepth)));
        if(!originalEye)
        {
            if(lowerError <= 0.0f) break;
            upperDepth = lowerDepth;
            upperError = lowerError;
        }
    }

    float intersectionDepth = lowerDepth;
    [unroll] for(int refinement = 0; refinement < 3; ++refinement)
    {
        intersectionDepth = originalEye
            ? (upperDepth * lowerError - upperError * lowerDepth) / (lowerError - upperError)
            : lerp(lowerDepth, upperDepth, saturate(-lowerError / max(upperError - lowerError, 0.000001f)));
        float error = intersectionDepth - pupil_radial_depth(uv + rayUV * (1.0f - intersectionDepth));
        if(abs(error) <= 0.01f) return uv + rayUV * (1.0f - intersectionDepth);
        if(error > 0.0f)
        {
            upperDepth = intersectionDepth;
            upperError = error;
        }
        else
        {
            lowerDepth = intersectionDepth;
            lowerError = error;
        }
    }
    // The source keeps the coarse hit unless one of the three refinements converges.
    return uv + (originalEye ? coarseOffset : rayUV * (1.0f - intersectionDepth));
}

void pupil_head_frame(float3 normalWS, float3 positionWS,
    out float3 headU, out float3 headV, out bool positiveSide)
{
    // Match cb0[252..254]: mesh tangents are not the game's eye coordinate frame.
    headU = normalize(-_CharacterHeadCenterXDirWS
        - normalWS * dot(-_CharacterHeadCenterXDirWS, normalWS));
    headV = normalize(cross(headU, normalWS));
    positiveSide = dot(positionWS - _CharacterHeadCenterWorldPosition,
        cross(_CharacterHeadCenterXDirWS, normalize(_CharacterFaceWorldDirection))) > 0.0f;
}

float pupil_light_angle(float2 direction)
{
    // Preserve the compiled shader's atan approximation and quadrant selection.
    float length2D = length(direction);
    direction = length2D > 0.001f ? direction / length2D : float2(0.0f, 1.0f);
    float ratio = min(abs(direction.x), abs(direction.y)) / max(abs(direction.x), abs(direction.y));
    float ratio2 = ratio * ratio;
    float polynomial = (((0.0208350997f * ratio2 - 0.0851330012f) * ratio2
        + 0.180141002f) * ratio2 - 0.330299497f) * ratio2 + 0.999866009f;
    float angle = ratio * polynomial;
    if(abs(direction.y) < abs(direction.x)) angle = 1.57079637f - angle;
    if(direction.y < 0.0f) angle -= 3.141593f;
    if(min(direction.x, direction.y) < 0.0f && max(direction.x, direction.y) >= 0.0f) angle = -angle;
    return angle;
}

float3 pupil_caustic(float2 uv, float2 refractedUV, float refractedAlpha,
    float3 normalWS, float3 positionWS, float3 lightDirectionWS, out float irisMask)
{
    // Pupil_019 computes both UV modes; the caller selects pre-layer or post-MatCap addition.
    // The noise-texture branch is not used by Odette.
    float2 disc = (uv - 0.5f) / 0.46f;
    irisMask = (1.0f - smoothstep(1.0f - 0.127f, 1.0f, dot(disc, disc)))
        * saturate(_EyeMaskWithMatcap.Sample(sampler_linear_clamp, uv).a);
    if(irisMask <= 0.01f) return 0.0f;

    float3 headU, headV;
    bool positiveSide;
    pupil_head_frame(normalWS, positionWS, headU, headV, positiveSide);
    // The decoded caustic side test is cross(face, headX), independent of the MatCap comparison frame.
    positiveSide = dot(positionWS - _CharacterHeadCenterWorldPosition,
        cross(-_CharacterHeadCenterXDirWS, normalize(_CharacterFaceWorldDirection))) > 0.0f;
    headV = normalize(headV);
    if(!positiveSide && _isUVMirror < 0.5f) headV = -headV;
    float3 causticLight = normalize(normalize(lightDirectionWS) + _CausticLightDirectionOffset);
    float2 lightInEye = float2(dot(headV, causticLight), dot(headU, causticLight));
    float2 causticUV = lerp(uv, refractedUV, _CausticUseRefractedUV);
    float2 radial = (causticUV - _CausticCenter) * 2.0f;
    radial.x *= positiveSide ? -1.0f : 1.0f;
    // UV center and an axial light have no 2D direction; avoid undefined rsqrt(0).
    radial *= rsqrt(max(dot(radial, radial), 0.000001f));
    lightInEye *= rsqrt(max(dot(lightInEye, lightInEye), 0.000001f));
    float directionResponse = pow(saturate(dot(radial, -lightInEye)), _CausticDirectionSharpness);
    float facing = dot(normalize(normalWS), causticLight);
    float front = smoothstep(-_CausticFrontBackTransition, _CausticFrontBackTransition, facing);
    float angleResponse = pow(saturate(1.0f - abs(facing)), _CausticAngleResponse) * front;
    float response = 1.0f + _CausticLightInfluence * (directionResponse * angleResponse - 1.0f);
    response = max(response, _CausticBackLightMin * (1.0f - front));
    response = lerp(response, step(_CausticThreshold, response), _CausticBinary);
    // Source alpha weights both modes; only the non-refracted final addition uses irisMask.
    float sourceAlpha = lerp(_MainTex.Sample(sampler_MainTex, uv).a, refractedAlpha, _CausticUseRefractedUV);
    return _CausticColor.rgb * (sourceAlpha * response);
}

float4 ps_model(vs_out i,  bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    modelFadeClip(i.ss_pos);
    // R marks overlapping highlight geometry, not another copy of the iris surface.
    if(_UseEyeRegionRouting > 0.5f) clip(0.5f - i.v_col.r);

    // get the world space position now, might be more expensive here but concerving vertex outputs
    float4 ws_pos = mul(unity_ObjectToWorld, i.ws_pos);
    // FUTURE PROOFING :
    float cockType = _StarCockType;
    float cockEnabled = _StarCloakEnable;
    if(!cockEnabled) 
    {
        cockType = 10;
    }
    
    // stupid legacy specular support, please mihoyo dont change value names again
    float specular_enabled = 0;
    if((_SpecularHighlights == 1) || (_UseToonSpecular == 1))
    { // cringe ass code
        specular_enabled = 1;
    }
    // GET LIGHT ATTENUATION FOR BOTH PASSES : 
    UNITY_LIGHT_ATTENUATION(atten, i, ws_pos.xyz);

    // INITIALIZE PIXEL SHADER OUTPUT: 
    float4 out_color = (float4)1.0f; 
    // INITIALIZE VERTEX INPUTS: 
    float3 normal = normalize(i.normal);
    float3 tangent = normal;
    float3 bitangent = normal;
    normal = (vface) ? normal : -normal; // check if back facing and invert the normals 
    float3 view   = normalize(i.view);
    float2 uv_a = (!vface && _UseBackFaceUV2) ? i.uv_a.zw : i.uv_a.xy; 
    float4 uv_b = i.uv_b;
    float3 light = _WorldSpaceLightPos0.xyz;
    float night_shift = auto_night_shift();

    float unity_shadow = SHADOW_ATTENUATION(i);
    unity_shadow = smoothstep(0.5f, 1.f, unity_shadow);

    if(!_EnableShadow || !_EnableSelfShadow) 
    {
        unity_shadow = 1.f;
    }

    // SAMPLE TEXTURES : 
    float2 irisUV = uv_a;
    [branch] if(_UseEyeRefraction > 0.5f && i.v_col.r < 0.5f)
    {
        // Match the existing skinned eye frame; leave decorative layers and outer reflection UVs unchanged.
        float3 refractionTangent = i.tangent.xyz - normal * dot(i.tangent.xyz, normal);
        refractionTangent *= rsqrt(max(dot(refractionTangent, refractionTangent), 0.000001f));
        float3 refractionBitangent = cross(normal, refractionTangent) * i.tangent.w;
        float3 viewInEye = float3(dot(view, refractionTangent), dot(view, refractionBitangent), dot(view, normal));
        if(_UseOriginalEyeFrame > 0.5f)
        {
            float3 headU, headV;
            bool positiveSide;
            pupil_head_frame(i.normal, ws_pos.xyz, headU, headV, positiveSide);
            if(!positiveSide && _isUVMirror < 0.5f) headV = -headV;
            // The source ray uses the opposite side convention when UV mirroring is enabled.
            float mirrorDirection = _isUVMirror < 0.5f ? 1.0f : -1.0f;
            viewInEye = float3(dot(view, headV) * (positiveSide ? -1.0f : 1.0f) * mirrorDirection,
                dot(view, headU), dot(view, i.normal));
        }
        irisUV = pupil_refraction_uv(uv_a, viewInEye);
    }
    float4 diffuse = _MainTex.Sample(sampler_MainTex, irisUV);
    float3 unrefractedIris = diffuse.rgb;
    float irisCompositionMask = 1.0f;
    if(_UseOriginalEyeFrame > 0.5f)
    {
        float2 disc = (uv_a - 0.5f) / 0.46f;
        irisCompositionMask = (1.0f - smoothstep(1.0f - 0.127f, 1.0f, dot(disc, disc)))
            * saturate(_EyeMaskWithMatcap.Sample(sampler_linear_clamp, uv_a).a);
        irisCompositionMask = irisCompositionMask > 0.01f ? irisCompositionMask : 0.0f;
        unrefractedIris = _MainTex.Sample(sampler_MainTex, uv_a).rgb;
    }
    [branch] if(_UseEyeHighlights > 0.5f && _UseDecodedEyeMatcap > 0.5f && i.v_col.r < 0.5f)
    {
        // Decode RG at iris UVs, then sample B at independent view-normal UVs.
        // The previous mesh frame remains available only for A/B comparison.
        float3 baseTangent = i.tangent.xyz - normal * dot(i.tangent.xyz, normal);
        baseTangent *= rsqrt(max(dot(baseTangent, baseTangent), 0.000001f));
        float3 baseBitangent = cross(normal, baseTangent) * i.tangent.w;
        float2 packedXY = _BaseMatcapPackedTex.Sample(sampler_linear_clamp, irisUV).rg * 2.0f - 1.0f;
        float3 packedNormal = normalize(float3(packedXY, sqrt(max(0.0f, 1.0f - dot(packedXY, packedXY)))));
        float3 baseNormal = normalize(baseTangent * packedNormal.x + baseBitangent * packedNormal.y
            + normal * packedNormal.z);
        if(_UseOriginalEyeFrame > 0.5f)
        {
            float3 headU, headV;
            bool positiveSide;
            pupil_head_frame(i.normal, ws_pos.xyz, headU, headV, positiveSide);
            if(!positiveSide && _isUVMirror < 0.5f)
            {
                headV = -headV;
                packedXY.x = -packedXY.x;
            }
            baseNormal = normalize(headV * packedXY.x + headU * packedXY.y
                + i.normal * sqrt(max(0.0f, 1.0f - dot(packedXY, packedXY))));
        }
        float2 baseMatcapUV = mul((float3x3)UNITY_MATRIX_V, baseNormal).xy * (0.5f * _BaseMatcapSize) + 0.5f;
        float baseReflection = _BaseMatcapPackedTex.Sample(sampler_linear_clamp, baseMatcapUV).b;
        // Odette's original BaseMatcapBlendMode is additive (0), before the iris layers.
        diffuse.rgb += baseReflection * _BaseMatcapTint.rgb * _BaseMatcapIntensity;
    }
    #ifdef _IS_PASS_BASE
    float3 pupilCausticColor = 0.0f;
    float pupilCausticMask = 0.0f;
    [branch] if(_UseEyeCaustics > 0.5f && i.v_col.r < 0.5f)
    {
        pupilCausticColor = pupil_caustic(uv_a, irisUV, diffuse.a, i.normal, ws_pos.xyz, light, pupilCausticMask);
        if(_CausticUseRefractedUV > 0.5f) diffuse.rgb += pupilCausticColor;
    }
    #endif
    [branch] if(_UseEyeLayers > 0.5f)
    {
        bool originalEye = _UseOriginalEyeFrame > 0.5f;
        float irisMask = irisCompositionMask;
        float2 eyeOffset = 0.0f;
        if(_UseEyeLayerParallax > 0.5f)
        {
            // Use the eye mesh's tangent frame, not world axes, so the layers follow head rotation.
            float3 eyeTangent = i.tangent.xyz - normal * dot(i.tangent.xyz, normal);
            eyeTangent *= rsqrt(max(dot(eyeTangent, eyeTangent), 0.000001f));
            float3 eyeBitangent = cross(normal, eyeTangent) * i.tangent.w;
            // Bound grazing-angle offsets to prevent the iris layers sliding out of the eye.
            eyeOffset = float2(dot(view, eyeTangent), dot(view, eyeBitangent)) / max(abs(dot(view, normal)), 0.25f);
            if(originalEye)
            {
                float3 headU, headV;
                bool positiveSide;
                pupil_head_frame(i.normal, ws_pos.xyz, headU, headV, positiveSide);
                if(!positiveSide && _isUVMirror < 0.5f) headV = -headV;
                float mirrorDirection = _isUVMirror < 0.5f ? 1.0f : -1.0f;
                float2 eyeView = float2(dot(view, headV) * (positiveSide ? -1.0f : 1.0f) * mirrorDirection, dot(view, headU));
                eyeOffset = -eyeView * irisMask / max(dot(view, i.normal), 0.00001f);
            }
        }
        // Odette uses mode 0: strength weights both layer RGB and alpha; the disc masks the blend.
        // Keep the comparison path and base alpha unchanged for existing clipping/emission rules.
        if(_ParallaxLayer1 > 0.5f)
        {
            float2 layerUV = uv_a * _ParallaxLayer1Tex_ST.xy + _ParallaxLayer1Tex_ST.zw;
            layerUV += eyeOffset * _ParallaxLayer1ParallaxScale;
            float4 layer = _ParallaxLayer1Tex.Sample(sampler_linear_clamp, layerUV);
            float weight = originalEye ? saturate(layer.a * _ParallaxLayer1Intensity) * irisMask
                : saturate(layer.a * _ParallaxLayer1Tint.a);
            diffuse.rgb = lerp(diffuse.rgb, layer.rgb * _ParallaxLayer1Tint.rgb * _ParallaxLayer1Intensity, weight);
        }
        if(_ParallaxLayer3 > 0.5f)
        {
            float2 layerUV = uv_a * _ParallaxLayer3Tex_ST.xy + _ParallaxLayer3Tex_ST.zw;
            layerUV += eyeOffset * _ParallaxLayer3ParallaxScale;
            float4 layer = _ParallaxLayer3Tex.Sample(sampler_linear_clamp, layerUV);
            float weight = originalEye ? saturate(layer.a * _ParallaxLayer3Intensity) * irisMask
                : saturate(layer.a * _ParallaxLayer3Tint.a);
            diffuse.rgb = lerp(diffuse.rgb, layer.rgb * _ParallaxLayer3Tint.rgb * _ParallaxLayer3Intensity, weight);
        }
    }
    // Pupil_019 blends the whole inner composite back to the original-UV color before outer reflection.
    // This mask is separate from each layer's alpha weight; preserve alpha for the existing passes.
    if(_UseOriginalEyeFrame > 0.5f && i.v_col.r < 0.5f)
        diffuse.rgb = lerp(unrefractedIris, diffuse.rgb, irisCompositionMask);
    float4 lightmap = _LightMapTex.Sample(sampler_linear_repeat, uv_a);

    // MATERIAL REGION 
    float material_id = materialID(lightmap.w);

    #if defined(use_shadow)
        float2 ao_uv[3] =
        {
            uv_a, i.uv_a.zw, i.ss_pos.xy/i.ss_pos.ww
        };
        float2 final_uv =  ao_uv[_CustomAOUV % 3] * _CustomAO_ST.xy + _CustomAO_ST.zw;
        float customao  = _AOSamplerType == 0 ? _CustomAO.Sample(sampler_linear_repeat, final_uv).x : _CustomAO.Sample(sampler_linear_clamp, final_uv).x ; // loop through array if number is greater than 3 or less than 0
        // customao = customao * unity_shadow;
    #endif
    #if defined(use_bump)
        float4 normalmap = _BumpMap.Sample(sampler_linear_repeat, uv_a);
    #endif
    #if defined(faceishadow)
        float4 facemap = _FaceMapTex.Sample(sampler_linear_repeat, uv_a);
    #endif

    #if defined(_is_shadow)
        if(!_EnableNyxOutline)
        {
            if(_UseHairShadow && variant_selector == 4) // if material type is bangs
            {  
                float4 hair_color = lerp(_HairShadowColor, _CoolHairShadowColor, night_shift);
                hair_color.w = 0.07f;
                diffuse = hair_color;
                return diffuse;
            }
            else if(_UseHairShadow && variant_selector == 1) // if material type is face
            {
                float shadow_mask = shadow_area_face(uv_a, light).x;
                clip(shadow_mask - 0.1f);
                return shadow_mask;
            }
            else
            {
                clip(-1);
            }
        }
        else
        {return diffuse;} // if shadow pass, clip everything so it doesn't render
    #endif

    #ifdef _IS_PASS_BASE // Basic character shading pass, should only include the basic enviro light stuff + debug rendering shit
        // do the vat stuff first so it can be returned as soon as possible
        #if defined(use_vat)
        if(_VertexAnimType == 1)
        {
            if(_MainTexAlphaUse == 1) clip(diffuse.w - _MainTexAlphaCutoff);
            mavuika_vat_ps(diffuse, i.uv_a, normal, view, i.v_col);
            return diffuse;
        }
        else if(_VertexAnimType==2)
        {
            float4 u_xlat0;
            float3 u_xlat10_0;
            bool u_xlatb0;
            float4 u_xlat1;
            float3 u_xlat10_1;
            float3 u_xlat16_2;
            float3 u_xlat16_3;
            float2 u_xlat4;
            float u_xlat16_4;
            float u_xlat12;
            float u_xlat16_14;

            float vertical_fade = (i.uv_a.y + (-_VerticalFadeOffset)) / _VerticalFadeRange;
            vertical_fade = saturate(vertical_fade);
            vertical_fade = -vertical_fade + 1.0f;

            if((_VerticalFadeToggle && _EnableDithering) && (vertical_fade < 0.95f))
            {
                ditherClip(i.ss_pos.xy/i.ss_pos.z, vertical_fade);
            }

            u_xlat0.x = i.uv_a.y;
            u_xlat0.y = 0.5;
            u_xlat10_1.xyz = _VerticalRampTex.Sample(sampler_linear_clamp, u_xlat0.xy).xyz;
            u_xlat10_0.xyz = _VerticalRampTex2.Sample(sampler_linear_clamp, u_xlat0.xy).xyz;
            u_xlat16_2.xyz = (-u_xlat10_1.xyz) + u_xlat10_0.xyz;
            u_xlat16_2.xyz = _VerticalRampLerp * u_xlat16_2.xyz + u_xlat10_1.xyz;
            u_xlat16_3.xyz = u_xlat16_2.xyz * _VerticalRampTint.xyz;
            u_xlat0.xy = i.uv_a.yx * _HighlightMaskTex_ST.yx + _HighlightMaskTex_ST.wz;
            u_xlat0 = _HighlightMaskTex.Sample(sampler_linear_repeat, u_xlat0.xy);
            u_xlat16_14 = (_HighlightMaskTexChannelSwitch == 3) ? u_xlat0.w : 0.0;
            u_xlat16_14 = (_HighlightMaskTexChannelSwitch == 2) ? u_xlat0.z : u_xlat16_14;
            u_xlat16_14 = (_HighlightMaskTexChannelSwitch == 1) ? u_xlat0.y : u_xlat16_14;
            u_xlat16_14 = (_HighlightMaskTexChannelSwitch == 0) ? u_xlat0.x : u_xlat16_14;
            u_xlat16_2.xyz = (-u_xlat16_2.xyz) * _VerticalRampTint.xyz + _HighlightColor.xyz;
            u_xlat16_2.xyz = u_xlat16_14 * u_xlat16_2.xyz + u_xlat16_3.xyz;
            u_xlat0.xy = i.uv_a.xy * _HighlightMaskTex2_ST.xy + _HighlightMaskTex2_ST.zw;

            u_xlat12 = _HighlightMaskTex2VOffsetByVerColA ? i.v_col.w : float(0.0);
            u_xlat0.z = u_xlat12 + u_xlat0.x;
            u_xlat0 = _HighlightMaskTex2.Sample(sampler_linear_repeat, u_xlat0.yz);
            u_xlat16_14 = (_HighlightMaskTex2ChannelSwitch == 3) ? u_xlat0.w : 0.0;
            u_xlat16_14 = (_HighlightMaskTex2ChannelSwitch == 2) ? u_xlat0.z : u_xlat16_14;
            u_xlat16_14 = (_HighlightMaskTex2ChannelSwitch == 1) ? u_xlat0.y : u_xlat16_14;
            u_xlat16_14 = (_HighlightMaskTex2ChannelSwitch == 0) ? u_xlat0.x : u_xlat16_14;
            u_xlat16_3.xyz = (-u_xlat16_2.xyz) + _HighlightColor2.xyz;
            u_xlat16_2.xyz = u_xlat16_14 * u_xlat16_3.xyz + u_xlat16_2.xyz;
            u_xlat16_2.xyz = u_xlat16_2.xyz * _Color.xyz;
            u_xlat16_2.xyz = u_xlat16_2.xyz * _DayColor.xyz;
            u_xlat16_14 = max(u_xlat16_2.z, u_xlat16_2.y);
            u_xlat16_14 = max(u_xlat16_14, u_xlat16_2.x);

            u_xlatb0 = 1.0<u_xlat16_14;

            u_xlat16_3.x = float(1.0) / float(u_xlat16_14);
            u_xlat16_3.xyz = u_xlat16_2.xyz * u_xlat16_3.xxx;
            u_xlat16_4 = u_xlat16_14 * 0.0500000007;

            u_xlat16_4 = clamp(u_xlat16_4, 0.0, 1.0);

            u_xlat4.x = sqrt(u_xlat16_4);
            float3 color = (bool(u_xlatb0)) ? u_xlat16_3.xyz : u_xlat16_2.xyz;
            return float4(color, (_VerticalFadeToggle?vertical_fade:1));
        }
        #endif
        float2 metalspec;
        metalspec.x = lightmap.x < 0.50f;
        metalspec.y = lightmap.x < 0.90f; // if metal area


        

        half bump_enable = _UseBumpMap;
        half isMainNormal = _isNativeMainNormal;
        half isLine = _TextureLineUse;
        float fixNormal = _DummyFixedForNormal;
        bool native_cloth = _ShiningMaterialBranch > 0.5f;
        // Body response is independent of both the sparkle region selector and the silk toggle.
        bool native_body = _UseNativeBodyResponse > 0.5f;
        bool native_response = native_cloth || native_body;
        float3 geometry_normal = normal;

        // adding support for new normal mapping boolean
        #if defined(use_bump)
            if (native_response)
            {
                float3 cloth_normal = normalmap.rgb;
                // Body and Dress01 use SelectMatID=1 in the source; it neutralizes RG on region 1.
                if (!(bump_enable || isMainNormal) || ((native_body || _ShiningMaterialBranch > 1.5f) && material_id == 1))
                    cloth_normal.rg = 0.5f;
                // Native VS emits origin-position here; its derivative frame has the opposite sign to world position.
                float3 cloth_position = _DummyFixedForNormal
                    ? unity_ObjectToWorld._m03_m13_m23 - ws_pos.xyz : i.view;
                cloth_normal_mapping(cloth_normal, cloth_position, uv_a, geometry_normal, normal, tangent, bitangent);
            }
            else if(bump_enable || isMainNormal) normal_mapping(normalmap, _DummyFixedForNormal ? ws_pos.xyz : i.view, _BumpScale, uv_a, normal, tangent, bitangent);
        #if defined(sdf_line)
            if((bump_enable || isMainNormal) && _TextureLineUse && ((_TextureLineMultiplier.x + _TextureLineMultiplier.y + _TextureLineMultiplier.z) > 0)) detail_line(i.ss_pos.zw, normalmap.z, diffuse.xyz);
        #endif
        #endif

        float3 normal_stock = normalize(i.normal);
        if (native_response) normal_stock = normal;

        #if defined(use_stockings)
            if(_UseCharacterStockings && (material_id  == 4)) 
            {
                float2 stocking_uv = uv_b.xy * _StockingsDetailTilingNear; 
                /* Previous implementation retained for rollback.
                // stocking normal logic
                float3 stocking_normal;
                stocking_normal.z = 1.0f;
                stocking_normal.xy = _StockingsDetailTex.Sample(sampler_linear_repeat, stocking_uv).xy * 2 - 1;
                stocking_normal.xy *= _StockingsDetailScale;

                // normal_stock = normalize(mul(float3x3(tangent, bitangent, normal), stocking_normal));

                float3 normal_stock = normalize(i.normal); 
                float3 tangent_stock; 
                float3 bitangent_stock;
                normal_mapping(stocking_normal, normalize(i.view), _StockingsDetailScale, uv_b.xy, normal_stock, tangent, bitangent);

                // normal = normalize(normal_stock);
                */

                float2 stocking_rg = _StockingsDetailTex.Sample(sampler_linear_repeat, stocking_uv).rg;
                if (native_response)
                {
                    if (lightmap.b <= 0.95f)
                    {
                        float2 detail_xy = (stocking_rg * 2.0f - 1.0f) * _StockingsDetailScale;
                        float detail_z = sqrt(1.0f - min(1.0f, dot(detail_xy, detail_xy)));
                        float3 detail_normal = normalize(tangent * detail_xy.x + bitangent * detail_xy.y + geometry_normal * detail_z);
                        // Native stockings share the combined normal with toon specular and cloth shining.
                        normal = normalize(normal + detail_normal);
                    }
                    normal_stock = normal;
                }
                else
                {
                // Keep RG encoded for normal_mapping's single decode. Scale around neutral 0.5;
                // use scale=0 there to keep tangent-space Z=1 and apply strength only once.
                float3 stocking_normal = float3((stocking_rg - 0.5f) * _StockingsDetailScale + 0.5f, 1.0f);
                float3 tangent_stock;
                float3 bitangent_stock;
                // Update the outer normal used by character_stocking, without replacing the body frame.
                normal_mapping(stocking_normal, ws_pos.xyz, 0.0f, uv_b.xy, normal_stock, tangent_stock, bitangent_stock);
                }
            }
        #endif
        // do this after the bump mapping to ensure that the normals are updated 
        // INITIALIZE INPUT VECTORS :
        float3 half_vector = normalize(light + view);
        float ndotl = dot(normal, light);
        float ndotv = dot(normal, view);
        float ndoth = dot(normal, half_vector);

        // lighting
        float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
        float3 light_color = max(ambient_color, _LightColor0.rgb);

        float3 GI_color = DecodeLightProbe(normal);
        GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
        float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
        GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;


        // COLOR SHIT 
        #if defined(use_texTint)
        if(_MainTexColoring) diffuse = maintint(diffuse);
        #endif
        // sample mask texture
        #if defined (has_mask)
        float4 material_mask = _MaterialMasksTex.Sample(sampler_linear_repeat, uv_a);
        #endif
        #if defined(can_shift)
            // HUE MASKS: 
            float diffuse_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv_a, _DiffuseMaskSource);
            float rim_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv_a, _RimMaskSource);
            float emission_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, uv_a, _EmissionMaskSource);

            if(!_UseHueMask)
            {
                diffuse_mask = 1.0f;
                rim_mask = 1.0f;
                emission_mask = 1.0f;
            }
        #endif

        if(_MainTexAlphaUse == 0) diffuse.w = 1.0f;
        if(_MainTexAlphaUse == 3) diffuse.xyz = lerp(diffuse, _FaceBlushColor, diffuse.w * _FaceBlushStrength);
        if(_MainTexAlphaUse == 1) clip(diffuse.w - _MainTexAlphaCutoff);

        // EMISSION SHIT :
        float emis_check = (_MainTexAlphaUse == 2 || _EmissionType == 1);
        float emis_check_eye = _EyePulse ? _ToggleEyeGlow * pulsate(_PulseSpeed, _PulseMinStrength, _PulseMaxStrength, _EyeTimeOffset) : _ToggleEyeGlow;
        float mask = diffuse.w; 
        float eye_mask = 0.0f;
        if(_EmissionType == 1) mask = _CustomEmissionTex.Sample(sampler_linear_repeat, uv_a).x;
        if(_ToggleEyeGlow == 1) eye_mask = lightmap.y > 0.95f;
        mask = saturate(mask - 0.02f); // removing any artifacts
        if(_StarCloakEnable && _StarCockEmis) mask = 1.0f;
        if((_StarCloakEnable && _StarCockEmis) && (_StarCockType == 2) && (!emis_check)) mask = diffuse.w;
        emis_check = _TogglePulse ? emis_check * pulsate(_PulseSpeed, _PulseMinStrength, _PulseMaxStrength, 0.0f) : emis_check; 
        

        if(_UseFaceMapNew)
        {
            mask = 0.0f;
            eye_mask = 0.0f;
        }

        // SHADOW
        float3 shadow = (float3)1.0f;
        float3 metalshadow = (float3)0.0f;
        float3 s_color = (float3)1.0f;
        float3 leather = (float3)0;
        #if defined(use_shadow)
            float shadow_ndotl = ndotl;
            #if defined(faceishadow)
                if (_UseFaceMapNew && _UseNativeFaceSDF > 0.5f) shadow_ndotl = i.native_face_ndotl;
            #endif
            if(_EnableShadow) shadow_color(lightmap.y, i.v_col.x, customao, unity_shadow, i.v_col.y, shadow_ndotl, material_id, i.uv_a.xy, shadow, metalshadow, s_color, light);
        #endif
        float native_face_specular = 0.0f;
        #if defined(faceishadow)
            [branch] if (_UseFaceMapNew && _UseNativeFaceSDF > 0.5f)
                native_face_specular = native_face_sdf_specular(uv_a, native_face_light_direction(light));
        #endif
           // SPECULAR : 
        float3 specular = (float3)0.0f;
        float3 holographic = (float3)1.0f;
        #if defined(use_specular)
            if(specular_enabled) specular_color(ndoth, shadow, lightmap.x, lightmap.z, material_id, specular);
            if(lightmap.x > 0.90f && !(native_response && _UseCharacterStockings && material_id == 4)) specular = 0.0f; // making sure the specular doesnt bleed into the metal area
        #endif
        // METALIC :
        float3 native_metal_specular = 0.0f;
        #if defined(use_metal)
            if(_MetalMaterial && !native_response && !(_UseCharacterStockings * (material_id == 4))) metalics(metalshadow, normal, ndoth, lightmap.x, vface, diffuse.xyz);
            if(_DebugMode && (_DebugMetal == 1) && !native_response) return float4(diffuse.xyz, 1.0f);
            // moving these after the metal so the metal can also be recolored
        #endif
        if(!_DisableColors && !_UseMaterialMasksTex) diffuse = diffuse * coloring(material_id);
        #if defined(has_mask)
            if(_UseMaterialMasksTex)
            {
                material_mask = material_mask * float4(_UseMaterial3, _UseMaterial4, _UseMaterial5, _UseMaterial5);
                float3 color = lerp(_Color, _Color2, material_mask.w);
                color = lerp(color, _Color3, material_mask.x);
                color = lerp(color, _Color4, material_mask.y);
                color = lerp(color, _Color5, material_mask.z);
                diffuse.xyz = diffuse.xyz * color;
            }
        #endif
        #if defined(can_shift)
            if(_EnableColorHue) diffuse.xyz = hue_shift(diffuse.xyz, material_id, _ColorHue, _ColorHue2, _ColorHue3, _ColorHue4, _ColorHue5, _GlobalColorHue, _AutomaticColorShift, _ShiftColorSpeed, diffuse_mask);
        #endif
        #if defined(use_metal)
            bool native_metal = native_response && _MetalMaterial && lightmap.r > 0.9f
                && !(_UseCharacterStockings && material_id == 4);
            if (native_metal)
            {
                float rampWidth = _UseVertexRampWidth > 0.0f
                    ? max(0.01f, i.v_col.y * 2.0f) * _ShadowRampWidth : _ShadowRampWidth;
                float environmentShadow = max(_NativeStockingEnvironmentShadow, 1.0f - unity_shadow);
                native_body_metal(normal, view, light, metalshadow.y,
                    metalshadow.y > 0.0f ? metalshadow.z * rampWidth : 0.0f,
                    environmentShadow, lightmap.b, diffuse.xyz, native_metal_specular);
                // Native metal already owns its base shadow; the ordinary toon ramp must not shade it again.
                s_color = 1.0f;
            }
            if (native_response && _DebugMode && _DebugMetal == 1)
                return float4(native_metal ? diffuse.xyz + native_metal_specular : 0.0f, 1.0f);
        #endif
        out_color = diffuse;
        out_color.w = 1.0f;

        float3 cloth_specular = 0.0f;
        #if defined(use_stockings)
            if(_UseCharacterStockings && (material_id  == 4)) 
            {
                float2 stocking_uv[4] = 
                {
                    i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, i.uv_b.zw
                };
                character_stocking(normal_stock, view, light, stocking_uv[_StockingUV % 4], lightmap,
                    length(_WorldSpaceCameraPos.xyz - ws_pos.xyz), unity_shadow, out_color, cloth_specular);
            }
        #endif

        #if defined(use_nbrbase)
        if(_UseCharacterNbrBase)
        {
            if(lightmap.x < 0.85f && lightmap.x > 0.5f)
            {
                nbr(normal, view, light, lightmap, out_color);  
            }
        }
        #endif

        #if defined(asmogay_arm)
            if(_HandEffectEnable)
            {
                out_color = diffuse;
                arm_effect(out_color, i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, view, normal, ndotl);
                #if defined(can_shift)
                    if(_EnableColorHue) out_color.xyz = hue_shift(out_color.xyz, material_id, _ColorHue, _ColorHue2, _ColorHue3, _ColorHue4, _ColorHue5, _GlobalColorHue, _AutomaticColorShift, _ShiftColorSpeed, diffuse_mask);
                #endif
                out_color.xyz = out_color.xyz * light_color;
                out_color.xyz = out_color.xyz + (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));
                return out_color;
            }
        #endif
        // FRESNEL : 
        // this is used for skill effects or certain idle animations
        // think like that one idle animation raiden has
        #if defined(has_fresnel)
            if(_EnableFresnel)fresnel_hit(ndotv, out_color.xyz);
        #endif

        #if defined(is_cock)
            if(_StarCloakEnable) star_cocks(float4(out_color.xyz, diffuse.w), i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, _ScreenIsWorld ? i.ppos : i.ss_pos, ndotv, light, i.parallax);
        #endif
        // apply specular opacity
        float3 spec_color = 0.0f;
        #if defined(use_specular)
            spec_color = out_color.xyz + (float3)-1.0f;
            float spec_opacity = _SpecOpacity;
            if (native_response)
            {
                float spec_opacities[5] = { _SpecOpacity, _SpecOpacity2, _SpecOpacity3, _SpecOpacity4, _SpecOpacity5 };
                spec_opacity = spec_opacities[get_index(material_id)];
                // Native tint uses colored diffuse, before stocking and other optional base responses.
                spec_color = diffuse.xyz - 1.0f;
            }
            spec_color.xyz = spec_opacity * spec_color.xyz + (float3)1.0f;
            spec_color.xyz = spec_color.xyz * specular;
        #endif

        #if defined(use_leather)
            // override the color with the leather if needed
            if(_UseCharacterLeather ) leather_color(ndoth, normal, light, lightmap.z, leather, holographic, out_color.xyz);
        #endif

        out_color.xyz = out_color.xyz * s_color + (spec_color) + cloth_specular;
        out_color.xyz += native_face_specular;
        [branch] if (native_response) out_color.xyz += native_metal_specular;
        // Silk is additive to the authored base response, not a replacement for legacy stockings.
        [branch] if (_EnableCharacterSilksAnisotropy > 0.5f)
        {
            float2 silkUVs[4] = { i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, i.uv_b.zw };
            out_color.xyz += character_silk(silkUVs[(int)_AnisoSilkThreadUVSelect], ws_pos.xyz,
                native_body ? normal : i.normal, view, light, lightmap, out_color.xyz);
        }
        [branch] if (_UseCharacterShining_V2 > 0.5f)
        {
            float2 shiningUVs[4] = { i.uv_a.xy, i.uv_a.zw, i.uv_b.xy, i.uv_b.zw };
            float3 cameraOffset = _WorldSpaceCameraPos.xyz - unity_ObjectToWorld._m03_m13_m23;
            if (_ShiningMaterialBranch > 0.5f)
                out_color.xyz += character_cloth_shining(shiningUVs[(int)_ShiningUVSwitch_ID1], shiningUVs[(int)_ShiningUVSwitch_ID2],
                    shiningUVs[(int)_ShiningUVSwitch_ID3], shiningUVs[(int)_ShiningUVSwitch_ID4],
                    i.v_col, normalmap.b, normal, view, light, cameraOffset, _Time.y);
            else
                out_color.xyz += character_body_shining(shiningUVs[(int)_ShiningUVSwitch_ID1],
                    shiningUVs[(int)_ShiningUVSwitch_ID3], shiningUVs[(int)_ShiningUVSwitch_ID4],
                    float2(normalmap.b, lightmap.b), cameraOffset, _Time.y);
        }
        
        
                

        // New 5.0 content shit
        #if defined(parallax_glass)
            if(_UseGlassSpecularToggle) glass_color(out_color, i.uv_a, view, normal);
        #endif
        // #if defined(nyx_body)
            if(_EnableNyxBody && _BodyAffected) nyx_state_marking(out_color.xyz, uv_a.xy, i.uv_a.zw, uv_b.xy, uv_b.zw, normal, view, i.ss_pos);
        // #endif
        // Apply scene light color, only taking the main directional light color * ambient color settings
        CalcLighting(normal, out_color.xyz);

        
        
        // #if defined(nyx_body)
            if(_EnableNyxBody && !_BodyAffected) nyx_state_marking(out_color.xyz, uv_a.xy, i.uv_a.zw, uv_b.xy, uv_b.zw, normal, view, i.ss_pos);
        // #endif

        if(_UseFakePoint) out_color.xyz = fakePointLight(i.ws_pos.xyz, lightmap.w, out_color.xyz, _FakePointReflection, _FakePointFrequency, _FakePointFrequencyMin, _FakePointPosition, _FakePointColor, _FakePointRange, _FakePointSkinIntensity, _FakePointIntensity, _FakePointSkinSaturate);
        if(_UseFakePoint2) out_color.xyz = fakePointLight(i.ws_pos.xyz, lightmap.w, out_color.xyz, _FakePointReflection2, _FakePointFrequency2, _FakePointFrequencyMin2, _FakePointPosition2, _FakePointColor2, _FakePointRange2, _FakePointSkinIntensity2, _FakePointIntensity2, _FakePointSkinSaturate2);
        if(_UseFakePoint3) out_color.xyz = fakePointLight(i.ws_pos.xyz, lightmap.w, out_color.xyz, _FakePointReflection3, _FakePointFrequency3, _FakePointFrequencyMin3, _FakePointPosition3, _FakePointColor3, _FakePointRange3, _FakePointSkinIntensity3, _FakePointIntensity3, _FakePointSkinSaturate3);
        // Rim light moved to last thing done to the model so to ensure that all parts get it
        // previous versions it was added to the color before certain things and they didnt recieve any rim lights as a result
        // most notably was the star cock shit when set to be emissive
        float3 rim_light = (float3)0.0f;
        #if defined(use_rimlight)
            if(_UseRimLight) 
            {
                rim_light = rimlighting(i.ss_pos, normal, ws_pos, light, material_id, out_color.xyz, view);
                #if defined(can_shift)
                    if(_EnableRimHue) rim_light.xyz = hue_shift(rim_light.xyz, material_id, _RimHue, _RimHue2, _RimHue3, _RimHue4, _RimHue5, _GlobalRimHue, _AutomaticRimShift, _ShiftRimSpeed, rim_mask);
                #endif
                out_color.xyz = out_color.xyz + rim_light;
            }
        #endif
        
        // basic ass transparency
        if(_MainTexAlphaUse == 4)
        {
            out_color.w = diffuse.w;      
            if(_EnableDithering) ditherClip(ws_pos.xy, out_color.w);
        }
        if(_EnableAvatarDie && !_ApplyOnlyNyx)
        {
            avatar_death(i.uv_a.xy, diffuse.a, vface, out_color);
        }
        // stencils
        #if defined(is_stencil)
            if(_UseEyeStencil)
            {
                stencil_mask(i.ws_pos, out_color, lightmap, view, i.uv_a.xy);
            }
            else
            {
                discard;
            }
        #endif

        if(_EnableTonemapping) out_color = tonemapping(out_color);
        
        float4 emis_color = float4(diffuse.xyz, 1.0f);
        float4 emis_color_eye = float4(diffuse.xyz, 1.0f);
        
        emis_color = emission_color(emis_color, material_id);
        emis_color_eye = emission_color_eyes(emis_color, material_id);
        #if defined(can_shift)
            if(_EnableEmissionHue) emis_color.xyz = hue_shift(emis_color.xyz, material_id, _EmissionHue, _EmissionHue2, _EmissionHue3, _EmissionHue4, _EmissionHue5, _GlobalEmissionHue, _AutomaticEmissionShift, _ShiftEmissionSpeed, emission_mask);
            if(_EnableEmissionHue) emis_color_eye.xyz = hue_shift(emis_color_eye.xyz, material_id, _EmissionHue, _EmissionHue2, _EmissionHue3, _EmissionHue4, _EmissionHue5, _GlobalEmissionHue, _AutomaticEmissionShift, _ShiftEmissionSpeed, emission_mask);
        #endif
        #if defined(is_cock)
            if(_StarCloakEnable && _StarCockEmis)
            {
                emis_color.xyz = emis_color.xyz;
                emis_color.w = _EmissionScaler;
                mask = _StarCockType == 2 ? diffuse.w : 1.0f;
                emis_check = 1.0f;
            }
        #endif
        
        out_color.xyz = lerp(out_color.xyz, emis_color, ((mask * emis_color.w) * emis_check));
        out_color.xyz = lerp(out_color.xyz, emis_color_eye, (eye_mask * emis_check_eye) * emis_color_eye.w);

        [branch] if(_UseEyeHighlights > 0.5f)
        {
            // A UV hemisphere supplies curvature for flat pupil meshes. Keep it in the
            // skinned tangent frame so both the head and the camera can move independently.
            float3 eyeNormal = normalize(i.normal);
            float3 eyeTangent = i.tangent.xyz - eyeNormal * dot(i.tangent.xyz, eyeNormal);
            eyeTangent *= rsqrt(max(dot(eyeTangent, eyeTangent), 0.000001f));
            float3 eyeBitangent = cross(eyeNormal, eyeTangent) * i.tangent.w;
            float2 eyePlane = uv_a.xy * 2.0f - 1.0f;
            float eyeRadiusSquared = dot(eyePlane, eyePlane);
            float3 sphereNormal = normalize(eyeTangent * eyePlane.x + eyeBitangent * eyePlane.y
                + eyeNormal * sqrt(max(1.0f - eyeRadiusSquared, 0.001f)));
            float3 matcapNormal = normalize(lerp(eyeNormal, sphereNormal, _EyeMatcapSphericalNormalLerp));
            float packedReflectionStrength = 1.0f;
            [branch] if(_UseDecodedEyeMatcap < 0.5f && _UseEyePackedNormal > 0.5f && _UseEyeMatcap > 0.5f)
            {
                // Experimental RG decode in linear data space. B modes are alternative hypotheses.
                // Replace only the reflection normal; preserve layer UVs and the authored glint.
                float3 packedData = _BaseMatcapPackedTex.Sample(sampler_linear_clamp, uv_a.xy).rgb;
                float2 packedXY = packedData.rg * 2.0f - 1.0f;
                packedXY.y *= 1.0f - 2.0f * saturate(_EyePackedNormalFlipY);
                float3 packedNormal = normalize(float3(packedXY, sqrt(saturate(1.0f - dot(packedXY, packedXY)))));
                if(_EyePackedBMode > 0.5f && _EyePackedBMode < 1.5f)
                {
                    // B=0 means an unperturbed tangent-space normal, not the UV hemisphere.
                    packedNormal = normalize(lerp(float3(0.0f, 0.0f, 1.0f), packedNormal, saturate(packedData.b)));
                }
                else if(_EyePackedBMode > 1.5f)
                {
                    packedReflectionStrength = saturate(packedData.b);
                }
                matcapNormal = normalize(eyeTangent * packedNormal.x + eyeBitangent * packedNormal.y
                    + eyeNormal * packedNormal.z);
            }
            float2 matcapUV = mul((float3x3)UNITY_MATRIX_V, matcapNormal).xy * 0.5f + 0.5f;
            matcapUV = lerp(matcapUV, 1.0f - matcapUV, saturate(_EyeMatcapInvertUV.xy));
            float eyeHighlightMask = _EyeMaskWithMatcap.SampleLevel(sampler_linear_clamp, matcapUV, 0).r;
            float eyeCoverage = saturate((1.0f - eyeRadiusSquared) * 20.0f);

            // Adapt the separate mask/bright-core ramps in Gacha Setup's Highlight group.
            // This is a color overlay in the base pass, not an extra transparent draw call.
            // The authored glint uses pupil UVs; only the soft MatCap reflection follows the view.
            float authoredHighlightMask = _EyeMaskWithMatcap.SampleLevel(sampler_linear_clamp, uv_a.xy, 0).r;
            float highlightCoverage = saturate(authoredHighlightMask / 0.2f) * eyeCoverage;
            float highlightCore = saturate((authoredHighlightMask - 0.25f) / 0.45f);
            if(_UseEyeMatcap > 0.5f)
            {
                [branch] if(_UseDecodedEyeMatcap > 0.5f)
                {
                    // RGB contains the outer reflection; A is an iris-UV mask, not a MatCap-UV strength.
                    // Radius/feather are Odette's original 0.46/0.127 values for this comparison.
                    float2 irisPlane = (uv_a - 0.5f) / 0.46f;
                    float irisRadiusSquared = dot(irisPlane, irisPlane);
                    float discMask = 1.0f - smoothstep(1.0f - 0.127f, 1.0f, irisRadiusSquared);
                    float irisMask = discMask * saturate(_EyeMaskWithMatcap.Sample(sampler_linear_clamp, uv_a).a);
                    float2 spherePlane = irisPlane * (1.0f - 2.0f * step(0.5f, _EyeMatcapInvertUV.xy));
                    float3 outerSphere = normalize(eyeTangent * spherePlane.x + eyeBitangent * spherePlane.y
                        + eyeNormal * sqrt(max(1.0f - irisRadiusSquared, 0.000001f)));
                    float3 outerBaseNormal = eyeNormal;
                    if(_UseOriginalEyeFrame > 0.5f)
                    {
                        // Pupil_019 uses the head axes here, not the projected per-pixel eye frame.
                        float3 headRight = cross(_CharacterFaceWorldDirection, _CharacterHeadCenterXDirWS);
                        float3 headUp = -_CharacterHeadCenterXDirWS;
                        float3 headForward = cross(headRight, headUp);
                        float3 sphereLocal = normalize(float3(spherePlane, sqrt(max(0.0f, 1.0f - irisRadiusSquared))));
                        outerSphere = normalize(headRight * sphereLocal.x + headUp * sphereLocal.y + headForward * sphereLocal.z);
                        outerBaseNormal = i.normal;
                    }
                    float3 outerNormal = normalize(lerp(outerBaseNormal, outerSphere, _EyeMatcapSphericalNormalLerp));
                    outerNormal = irisRadiusSquared <= 1.0f ? outerNormal : outerBaseNormal;
                    float2 outerUV = mul((float3x3)UNITY_MATRIX_V, outerNormal).xy * 0.5f + 0.5f;
                    float3 reflection = _EyeMaskWithMatcap.Sample(sampler_linear_clamp, outerUV).rgb
                        * _EyeMatcapTint.rgb * _EyeMatcapIntensity;
                    // Odette's original EyeMatcapBlendMode is 1: add reflection modulated by iris color.
                    float3 reflectedColor = out_color.rgb + reflection * out_color.rgb;
                    // The source gates this block at mask > 0.01; it does not scale the reflection by it again.
                    float reflectionWeight = _UseOriginalEyeFrame > 0.5f ? (irisMask > 0.01f ? 1.0f : 0.0f) : irisMask;
                    out_color.rgb = lerp(out_color.rgb, reflectedColor, reflectionWeight);
                }
                else
                {
                    float3 reflection = _EyeMatcapTint.rgb * _EyeMatcapIntensity;
                    // Mask the overlay weight, not its color: B=0 must preserve the underlying iris.
                    out_color.rgb = lerp(out_color.rgb, reflection, saturate(eyeHighlightMask) * eyeCoverage * packedReflectionStrength);
                }
            }
            if(_UseHighlightRamp > 0.5f && _UseEyeRegionRouting < 0.5f)
            {
                float rampU = saturate(highlightCore / max(_HighlightRampWidth, 0.001f));
                // The 256x8 pupil ramp stores its color gradient in the top two rows; sample their center.
                float3 ramp = _PackedHighlightRampTex.SampleLevel(sampler_linear_clamp, float2(rampU, 0.875f), 0).rgb;
                float3 highlightColor = ramp * _EyeHighlightColor.rgb * _HighlightIntensity;
                out_color.rgb = lerp(out_color.rgb, highlightColor, highlightCoverage * highlightCore);
            }
        }

        // Pupil_019 lines 950-952: non-refracted caustics bypass the layers and outer reflection.
        [branch] if(_UseEyeCaustics > 0.5f && _CausticUseRefractedUV < 0.5f)
        {
            out_color.rgb += pupilCausticColor * pupilCausticMask;
        }

        #if defined(can_debug)
            if(_DebugMode) // debuuuuuug
            {
                if(_DebugLighting == 1) return float4(ndotl.xxx, 1.0f);
                if(_DebugLighting == 2) return float4(ndotv.xxx, 1.0f);
                if(_DebugLighting == 3) return float4(ndoth.xxx, 1.0f);
                if(_DebugLighting == 4) return float4(shadow.xxx, 1.0f);
                if(_DebugDiffuse == 1) return float4(diffuse.xyz, 1.0f);
                if(_DebugDiffuse == 2) return float4(diffuse.www, 1.0f);
                if(_DebugLightMap == 1) return float4(lightmap.xxx, 1.0f);
                if(_DebugLightMap == 2) return float4(lightmap.yyy, 1.0f);
                if(_DebugLightMap == 3) return float4(lightmap.zzz, 1.0f);
                if(_DebugLightMap == 4) return float4(lightmap.www, 1.0f);
                #if defined(faceishadow)
                    if(_DebugFaceMap == 1) return float4(facemap.xxx, 1.0f);
                    if(_DebugFaceMap == 2) return float4(facemap.yyy, 1.0f);
                    if(_DebugFaceMap == 3) return float4(facemap.zzz, 1.0f);
                    if(_DebugFaceMap == 4) return float4(facemap.www, 1.0f);
                #endif
                #if defined(use_bump)
                    if(_DebugNormalMap == 1) return float4(normalmap.xy, 1.0f, 1.0f);
                    if(_DebugNormalMap == 2) return float4(normalmap.zzz, 1.0f);
                #endif
                if(_DebugVertexColor == 1) return float4(i.v_col.xxx, 1.0f);
                if(_DebugVertexColor == 2) return float4(i.v_col.yyy, 1.0f);
                if(_DebugVertexColor == 3) return float4(i.v_col.zzz, 1.0f);
                if(_DebugVertexColor == 4) return float4(i.v_col.www, 1.0f);
                if(_DebugRimLight == 1) return float4(rim_light.xyz, 1.0f);
                if(_DebugNormalVector == 1) return float4(i.normal.xyz * 0.5f + 0.5f, 1.0f);
                if(_DebugNormalVector == 2) return float4(i.normal.xyz, 1.0f);
                if(_DebugNormalVector == 3) return float4(normal.xyz * 0.5f + 0.5f, 1.0f);
                if(_DebugNormalVector == 4) return float4(normal.xyz, 1.0f);
                if(_DebugTangent == 1) return float4(i.tangent.xyz, 1.0f);
                if(_DebugSpecular == 1) return float4(specular.xyz, 1.0f);
                if(_DebugEmission == 1) return float4((mask * emis_check).xxx, 1.0f);
                if(_DebugEmission == 2) return float4(emis_color.xyz, 1.0f);
                if(_DebugEmission == 3) return float4(emis_color.xyz * (mask * emis_check).xxx, 1.0f);
                if(_DebugFaceVector == 1) return float4(normalize(UnityObjectToWorldDir(_headForwardVector.xyz)).xyz, 1.0f);
                if(_DebugFaceVector == 2) return float4(normalize(UnityObjectToWorldDir(_headRightVector.xyz)).xyz, 1.0f);
                if((_DebugMaterialIDs > 0) && (_DebugMaterialIDs != 6))
                {
                    if(_DebugMaterialIDs == material_id)
                    {
                        return (float4)1.0f;
                    }
                    else 
                    {
                        return float4((float3)0.0f, 1.0f);
                    }
                }
                if(_DebugMaterialIDs == 6)
                {
                    float4 debug_color = float4(0.0f, 0.0f, 0.0f, 1.0f);
                    if(material_id == 1)
                    {
                        debug_color.xyz = float3(1.0f, 0.0f, 0.0f);
                    }
                    else if(material_id == 2)
                    {
                        debug_color.xyz = float3(0.0f, 1.0f, 0.0f);
                    }
                    else if(material_id == 3)
                    {
                        debug_color.xyz = float3(0.0f, 0.0f, 1.0f);
                    }
                    else if(material_id == 4)
                    {
                        debug_color.xyz = float3(1.0f, 0.0f, 1.0f);
                    }
                    else if(material_id == 5)
                    {
                        debug_color.xyz = float3(0.0f, 1.0f, 1.0f);
                    }
                    return debug_color;
                }
                if(_DebugLights == 1) return float4((float3)0.0f, 1.0f);
            }
        #endif

        
    #endif
    #ifdef _IS_PASS_LIGHT // Lighting shading pass, should only include the necessary lighting things needed
        if(_UseFaceMapNew) normal = float3(0.5f, 0.5f, 1.0f);
        #if defined(POINT) || defined(SPOT)
        light = normalize(_WorldSpaceLightPos0.xyz - ws_pos.xyz);
        #endif

        // SHADOW
        // since this pass doesnt want the colors of the shadows, just use the shadow only functions: 
        float ndotl = dot(normal, light) * unity_shadow;

        float3 shadow_area = (float3)1.0f;
        shadow_area = shadow_area_transition(lightmap.y, i.v_col.x, ndotl, material_id);
        // metalshadow = shadow_area_transition(lightmapao, vertexao, ndotl, material_id);
        if(_UseFaceMapNew) shadow_area = saturate(ndotl);
        // shadow = outshadow;

        float bright = lightmap.y > 0.9f && !_UseFaceMapNew;

        float light_intesnity = max(0.001f, (0.299f * _LightColor0.r + 0.587f * _LightColor0.g + 0.114f * _LightColor0.b));
        float3 light_pass_color = ((diffuse.xyz * 5.0f) * _LightColor0.xyz) * atten * shadow_area * 0.5f;
        float3 light_color = lerp(light_pass_color.xyz, lerp(0.0f, min(light_pass_color, light_pass_color / light_intesnity), _WorldSpaceLightPos0.w), _FilterLight); // prevents lights from becoming too intense
        #if defined(POINT) || defined(SPOT)
        out_color.xyz = (light_color * saturate(1.0f - bright)) * 0.5f;
        #elif defined(DIRECTIONAL)
        out_color.xyz = 0.0f; // dont let extra directional lights add onto the model, this will fuck a lot of shit up
        #endif
        
    #endif
    if(_UseWeapon) weapon_shit(out_color.xyz, diffuse.w, i.uv_a.zw, normal, view, ws_pos);
    return out_color; 
}

float4 ps_pupil_highlight(vs_out i) : SV_TARGET
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    clip(_UseEyeRegionRouting - 0.5f);
    clip(_UseEyeHighlights - 0.5f);
    clip(i.v_col.r - 0.5f);
    modelFadeClip(i.ss_pos);

    float2 highlightUV = i.uv_a.xy;
    if(_UseOriginalEyeFrame > 0.5f)
    {
        float3 headU, headV;
        bool positiveSide;
        pupil_head_frame(i.normal, mul(unity_ObjectToWorld, i.ws_pos).xyz, headU, headV, positiveSide);
        float3 lightDirection = normalize(_WorldSpaceLightPos0.xyz) + _LightDirectionOffset;
        float lightAngle = pupil_light_angle(float2(dot(headU, lightDirection), dot(headV, lightDirection)));
        float angle = _RotationAngle - lightAngle * _LightInfluence;
        angle = frac((angle + 3.14159274f) * 0.159154937f) * 6.28318548f - 3.14159274f;
        float sine, cosine;
        sincos(angle, sine, cosine);
        float2 centeredUV = highlightUV - _EyeCenterUV;
        highlightUV = float2(centeredUV.x * cosine + centeredUV.y * sine,
            centeredUV.y * cosine - centeredUV.x * sine) + _EyeCenterUV;
    }
    // Odette disables time jitter, but retains light-dependent rotation.
    float sdf = _SDFGradientMap.Sample(sampler_linear_clamp, highlightUV).r;
    if(_UseOriginalEyeFrame > 0.5f && _InvertSDFMap > 0.5f) sdf = 1.0f - sdf;
    float distanceFromEdge = sdf - saturate(_SDFCenterValue);
    clip(distanceFromEdge - 0.000001f);
    float3 highlight = 1.0f;
    if(_UseHighlightRamp > 0.5f)
    {
        float rampU = saturate(distanceFromEdge / max(_HighlightRampWidth, 0.01f));
        highlight = _PackedHighlightRampTex.Sample(sampler_linear_clamp, float2(rampU, 0.875f)).rgb;
    }
    return float4(highlight * _EyeHighlightColor.rgb * _HighlightIntensity, 1.0f);
}

float4 ps_edge(vs_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    modelFadeClip(i.ss_pos);
    if(_UseEyeRegionRouting > 0.5f) clip(0.5f - i.v_col.r);
    float4 out_color = (float4)1.0f;
    #if defined(can_shift)
        float outline_mask = packed_channel_picker(sampler_linear_repeat, _HueMaskTexture, i.uv_a.xy, _OutlineMaskSource);
        outline_mask = _UseHueMask ? outline_mask : 1.0f;
    #endif


    float4 diffuse = _MainTex.Sample(sampler_MainTex, i.uv_a.xy);
    float4 lightmap = _LightMapTex.Sample(sampler_linear_repeat, i.uv_a.xy);

    float material_ID = materialID(lightmap.w);

    float4 outline_color[5] =
    {
        float4(_OutlineColor.xyz, _OutLineIntensity.x), float4(_OutlineColor2.xyz, _OutLineIntensity2.x), float4(_OutlineColor3.xyz, _OutLineIntensity3.x), float4(_OutlineColor4.xyz, _OutLineIntensity4.x), float4(_OutlineColor5.xyz, _OutLineIntensity5.x)
    };
    
    float3 emission;
    out_color.xyz = outline_color[material_ID - 1].w * (diffuse.xyz * (float3)0.203f + (-outline_color[material_ID - 1].xyz)) + outline_color[material_ID - 1].xyz;
    
    // out_color.xyz = out_color.xyz * light_color + GI_color;
    CalcLighting(normalize(i.normal), out_color.xyz);

    if(_EnableOutlineGlow)
    {
        emission = outline_emission(out_color.xyz, material_ID);
        out_color.xyz = emission;
    }
    
    if(_MainTexAlphaUse == 1) clip(diffuse.w - _MainTexAlphaCutoff);
    if(_MainTexAlphaUse == 4) out_color.w = diffuse.w;
    #if defined(weapon_mode)
        if(_UseWeapon) weapon_shit(out_color.xyz, diffuse.w, i.uv_a.zw, i.normal, i.view, i.ws_pos);
    #endif
    #if defined(can_shift)
        if(_EnableOutlineHue) out_color.xyz = hue_shift(out_color.xyz, material_ID, _OutlineHue, _OutlineHue2, _OutlineHue3, _OutlineHue4, _OutlineHue5, _GlobalOutlineHue, _AutomaticOutlineShift, _ShiftOutlineSpeed, 1.0f);
    #endif
    if(_MultiLight && !_EnableOutlineGlow) out_color.xyz = out_color.xyz  * (UNITY_LIGHTMODEL_AMBIENT.xyz + _LightColor0.xyz);
    if(_EnableAvatarDie && !_ApplyOnlyNyx)
    {
        avatar_death(i.uv_a.xy, diffuse.w, !vface, out_color);
    }
    return out_color;
}

float4 ps_nyx(vs_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    modelFadeClip(i.ss_pos);
    #if defined(nyx_outline)
        if(_EnableNyxOutline)
        {
            // calculate lighting colors
            float3 normal = normalize(i.normal);
            float3 ambient_color = max(half3(0.05f, 0.05f, 0.05f), max(ShadeSH9(half4(0.0, 0.0, 0.0, 1.0)),ShadeSH9(half4(0.0, -1.0, 0.0, 1.0)).rgb));
            float3 light_color = max(ambient_color, _LightColor0.rgb);
            float3 GI_color = DecodeLightProbe(normal);
            GI_color = GI_color < float3(1,1,1) ? GI_color : float3(1,1,1);
            float GI_intensity = 0.299f * GI_color.r + 0.587f * GI_color.g + 0.114f * GI_color.b;
            GI_intensity = GI_intensity < 1 ? GI_intensity : 1.0f;
            float3 gi = (GI_color * GI_intensity * _GI_Intensity * smoothstep(1.0f ,0.0f, GI_intensity / 2.0f));

            // initialize output color
            float4 color = (float4)1.0f;
            // handle alpha
            float alpha = _MainTex.Sample(sampler_MainTex, i.uv_a.xy).w;
            if(_MainTexAlphaUse == 0) alpha = 1.0f;
            if(_MainTexAlphaUse == 1) clip(alpha - _MainTexAlphaCutoff);
            if(_MainTexAlphaUse == 4) color.w = alpha;

            // this is basically the same code as the body nyx stuff
            // create the screen space uv for the noise to sampled from
            float4 screen_uv;
            screen_uv = ((i.ss_pos.xyxy / i.ss_pos.wwww) * _ScreenParams.xyxy) / _ScreenParams.xxxx;
            screen_uv.yw = 1.0f - screen_uv.yw;
            float4 noise_uv = _Time.yyyy * (_NyxStateOutlineColorNoiseAnim.zwxy);
            noise_uv = frac(noise_uv);
            screen_uv = screen_uv * _NyxStateOutlineColorNoiseScale.xyxy + noise_uv;
            float noise_a = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, screen_uv.xy).x;
            screen_uv.xy = noise_a.xx * (float2)_NyxStateOutlineColorNoiseTurbulence + screen_uv.zw;
            float2 ramp_uv;
            float2 time_uv;
            ramp_uv.x = _NyxStateOutlineNoise.Sample(sampler_linear_repeat, screen_uv.xy).x;
            ramp_uv.y = float(0.75);
            time_uv.y = float(0.25);
            float3 nyx_ramp = _NyxStateOutlineColorRamp.Sample(sampler_linear_repeat, ramp_uv.xy, 0.0).xyz;
            time_uv.x = (_DayOrNight) ? 0 : 1;
            float3 time_ramp = _NyxStateOutlineColorRamp.Sample(sampler_linear_repeat, time_uv.xy, 0.0).xyz;
            nyx_ramp.xyz = time_ramp.xyz * nyx_ramp.xyz;
            nyx_ramp.xyz = nyx_ramp.xyz * (float3)_NyxStateOutlineColorScale * _NyxStateOutlineColor;
            if(_NyxStateRampType == 1) 
            {
                nyx_ramp = custom_ramp_color(ramp_uv.x);
            }
            float nyx_brightness = max(nyx_ramp.z, nyx_ramp.y);
            nyx_brightness = max(nyx_ramp.x, nyx_brightness);
            float bright_check = 1.0f < nyx_brightness;
            color.xyz = bright_check ? (nyx_ramp * (1.0f / nyx_brightness)) : nyx_ramp;


            // depending on user choice, apply outside sources of lighting
            if(_LineAffected) color.xyz = color.xyz * light_color + gi;
            // if disabled, all pixels are clipped, this is to catch any weird issues with the nyx mode shader feature
            if(!_EnableNyxOutline) clip(-1);
            // color.xyz = nyx_noise;
            if(_EnableAvatarDie)
            {
                avatar_death(i.uv_a.xy, alpha, !vface, color);
            }
            return color;
        }
        else
        {
            clip(-1);
        }
    #else // if nyx mode is disabled, all pixels from this pass should be discarded
        clip(-1);
    #endif
    return (float4)1.0f;
}

float4 ps_shadow(shadow_out i, bool vface : SV_ISFRONTFACE) : SV_TARGET
{
    modelFadeClip(i.pos.xy);
    // initialize uv 
    float2 uv = (!vface && _UseBackFaceUV2) ? i.uv_a.zw : i.uv_a.xy;

    float alpha = _MainTex.Sample(sampler_MainTex, uv).w;

    float4 out_color = (float4)0.0f;
    
    if(_MainTexAlphaUse == 1) clip(alpha - _MainTexAlphaCutoff);
    if(_MainTexAlphaUse == 4) out_color.w = alpha;
    if(_UseWeapon) weapon_shit(out_color.xyz, alpha, i.uv_a.zw, i.normal, i.view, i.ws_pos);
    return 0.0f;
}

// ====================================================================
