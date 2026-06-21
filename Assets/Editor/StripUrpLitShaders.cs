using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

class StripUrpLitShaders : IPreprocessShaders
{
    public int callbackOrder => 0;

    static readonly HashSet<string> shadersToStrip = new HashSet<string>
    {
           "Universal Render Pipeline/Lit",
    "Universal Render Pipeline/Simple Lit",
    "Shader Graphs/PhysicalMaterial3DsMax",
     "Hidden/TerrainEngine/Details/UniversalPipeline/BillboardWavingDoublePass",
        "Hidden/TerrainEngine/Details/UniversalPipeline/WavingDoublePass",
        "Hidden/TerrainEngine/Details/UniversalPipeline/Vertexlit",
        "ProBuilder6/Standart Vertex Color",
        "Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion",
        "Universal Render Pipeline/Particles/Lit",
        "Universal Render Pipeline/Particles/Simple Lit"
        // Ýhtiyaca göre:
        // "Universal Render Pipeline/Complex Lit",
        // "Universal Render Pipeline/Baked Lit",
    };

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet,
                                IList<ShaderCompilerData> data)
    {
        if (shadersToStrip.Contains(shader.name))
            data.Clear(); // bu shader'ýn tüm varyantlarýný at
    }
}