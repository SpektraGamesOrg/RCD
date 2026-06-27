//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


using UnityEngine;
using System.Collections;
using System.Collections.Generic;

#if BCG_URP
using UnityEngine.Rendering.Universal;
#endif

#if BCG_URP

/// <summary>
/// Represents an under-vehicle neon glow slot. Wraps a URP DecalProjector and exposes a SetNeonMaterial method that swaps in the chosen neon material at runtime. The scale, pivot, and draw distance of the projector are auto-configured in OnValidate; a default neon material from RCC_Settings is applied if the projector has no material assigned.
/// </summary>
[RequireComponent(typeof(DecalProjector))]
public class RCC_Customizer_Neon : RCC_Core
{
    /// <summary>
    /// Decal projector component used to render the neon effect on the vehicle surface.
    /// </summary>
    private DecalProjector neonRenderer;

    /// <summary>
    /// Sets target material of the neon.
    /// </summary>
    /// <param name="material"></param>
    public void SetNeonMaterial(Material material)
    {
        //  Getting the mesh renderer.
        if (!neonRenderer)
            neonRenderer = GetComponentInChildren<DecalProjector>();

        //  Return if renderer not found.
        if (!neonRenderer)
            return;

        //  Setting material of the renderer.
        neonRenderer.material = material;
    }

    public void OnValidate()
    {
        if (!TryGetComponent(out DecalProjector dp))
            return;

        dp.scaleMode = DecalScaleMode.InheritFromHierarchy;
        dp.pivot = Vector3.zero;
        dp.drawDistance = 500f;

        if (!RCC_Settings.Instance)
            return;

        if (dp.material == null)
            dp.material = RCC_Settings.Instance.defaultNeonMaterial;

        if (dp.material.name.Contains("Default"))
            dp.material = RCC_Settings.Instance.defaultNeonMaterial;
    }
}

#else
/// <summary>
/// Represents an under-vehicle neon glow slot. Built-in / HDRP variant — neons are URP-only in RCC, so SetNeonMaterial is a no-op outside URP. Define BCG_URP to enable the DecalProjector-backed implementation.
/// </summary>
public class RCC_Customizer_Neon : RCC_Core {

    /// <summary>
    /// Sets target material of the neon.
    /// </summary>
    /// <param name="material"></param>
    public void SetNeonMaterial(Material material) {

        //Debug.LogError("Neons are working with URP only!");
        return;

    }

}
#endif