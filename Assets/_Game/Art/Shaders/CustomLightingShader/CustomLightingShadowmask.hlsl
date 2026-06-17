#ifndef CUSTOM_LIGHTING_SHADOWMASK_INCLUDED
#define CUSTOM_LIGHTING_SHADOWMASK_INCLUDED

// =====================================================================
//  Shader Graph Custom Function (File modu) icin shadowmask + main light
//  Unity 6 / URP 17 uyumlu. Unlit Graph'ta kullanmak icin Blackboard'a
//  su Boolean keyword'leri ekle (Multi-Compile, Global):
//    _MAIN_LIGHT_SHADOWS, _MAIN_LIGHT_SHADOWS_CASCADE, _SHADOWS_SOFT,
//    SHADOWS_SHADOWMASK, LIGHTMAP_SHADOW_MIXING
// =====================================================================

// ---------------------------------------------------------------------
// 1) Shadowmask ornekle.
//    LightmapUV girisi: UV node -> Channel "UV1" baglanir.
//    - Statik (lightmap'li) objelerde shadowmask texture'dan okur.
//    - Dinamik objelerde otomatik unity_ProbesOcclusion'a duser
//      (UV'ye gerek yok, bu yuzden 0 gecmek sorun degil).
//    Bu node'u SADECE BIR KEZ ornekle, sonucu Main Light'a besle.
// ---------------------------------------------------------------------
void Shadowmask_float(float2 LightmapUV, out half4 Shadowmask)
{
#ifdef SHADERGRAPH_PREVIEW
    Shadowmask = half4(1, 1, 1, 1);
#else
    #if defined(LIGHTMAP_ON)
        float2 uvLM = LightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
    #else
        float2 uvLM = float2(0, 0);
    #endif
    Shadowmask = SAMPLE_SHADOWMASK(uvLM);
#endif
}

// ---------------------------------------------------------------------
// 2) Main light'i shadowmask ile al.
//    Asagidaki GetMainLight(shadowCoord, WorldPos, Shadowmask) overload'u
//    realtime golge ile baked shadowmask'i KAMERA MESAFESINE gore
//    harmanlar -> "Distance Shadowmask" davranisini saglayan kisim budur.
//    Inputs : WorldPos (Position World), Shadowmask (yukaridaki node'dan)
//    Outputs: Direction, Color, ShadowAtten
// ---------------------------------------------------------------------
void MainLightShadowmask_float(float3 WorldPos, half4 Shadowmask,
    out float3 Direction, out float3 Color, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction   = normalize(float3(0.5, 0.5, -0.5));
    Color       = float3(1, 1, 1);
    ShadowAtten = 1;
#else
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(WorldPos));
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    #endif

    Light mainLight = GetMainLight(shadowCoord, WorldPos, Shadowmask);

    Direction   = mainLight.direction;
    Color       = mainLight.color;
    ShadowAtten = mainLight.shadowAttenuation; // distance fade dahil harmanlanmis golge
#endif
}

#endif
