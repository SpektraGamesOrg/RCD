//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton manager that creates and manages one <see cref="RCC_Skidmarks"/> instance per ground material type.
/// Wheels call <see cref="AddSkidMark"/> to add skidmark sections to the correct ground material's mesh.
/// </summary>
public class RCC_SkidmarksManager : RCC_Singleton<RCC_SkidmarksManager> {

    /// <summary>
    /// Array of skidmark renderers, one per ground material defined in RCC_GroundMaterials.
    /// </summary>
    private RCC_Skidmarks[] skidmarks;

    /// <summary>
    /// Current mark chain indices per ground material, tracking the last added mark for continuity.
    /// </summary>
    private int[] skidmarksIndexes;

    /// <summary>
    /// The last ground material index used, to detect ground type changes and break mark chains.
    /// </summary>
    private int _lastGroundIndex = 0;

    private void Awake() {

        m_Instance = this;
        
        //  Creating new skidmarks and initializing them with given ground materials in RCC Ground Materials.
        if (!GroundMaterials || GroundMaterials.frictions == null || GroundMaterials.frictions.Length < 1)
            return;

        skidmarks = new RCC_Skidmarks[GroundMaterials.frictions.Length];
        skidmarksIndexes = new int[skidmarks.Length];

        for (int i = 0; i < skidmarks.Length; i++) {

            if (!GroundMaterials.frictions[i].skidmark)
                continue;

            skidmarks[i] = Instantiate(GroundMaterials.frictions[i].skidmark, Vector3.zero, Quaternion.identity);
            skidmarks[i].transform.name = skidmarks[i].transform.name + "_" + GroundMaterials.frictions[i].groundMaterial.name;
            skidmarks[i].transform.SetParent(transform, true);

        }

    }

    /// <summary>
    /// Adds a skidmark section to the appropriate ground material's mesh. Called by skidding wheels.
    /// </summary>
    /// <param name="pos">World position of the contact point.</param>
    /// <param name="normal">Surface normal at the contact point.</param>
    /// <param name="intensity">Opacity of the mark (0-1).</param>
    /// <param name="width">Width of the skidmark in meters.</param>
    /// <param name="lastIndex">Previous mark index in this chain, or -1 for a new chain.</param>
    /// <param name="groundIndex">Index of the ground material type from RCC_GroundMaterials.</param>
    /// <returns>The index of the newly added mark, or -1 if the ground type changed.</returns>
    public int AddSkidMark(Vector3 pos, Vector3 normal, float intensity, float width, int lastIndex, int groundIndex) {

        //  Bounds and null safety checks.
        if (skidmarks == null || groundIndex < 0 || groundIndex >= skidmarks.Length)
            return -1;

        if (_lastGroundIndex != groundIndex) {

            _lastGroundIndex = groundIndex;
            return -1;

        }

        if (!skidmarks[groundIndex])
            return -1;

        skidmarksIndexes[groundIndex] = skidmarks[groundIndex].AddSkidMark(pos, normal, intensity, width, lastIndex);

        return skidmarksIndexes[groundIndex];

    }

    /// <summary>
    /// Cleans all skidmarks.
    /// </summary>
    public void CleanSkidmarks() {

        if (skidmarks == null)
            return;

        for (int i = 0; i < skidmarks.Length; i++) {

            if (skidmarks[i])
                skidmarks[i].Clean();

        }

    }

    /// <summary>
    /// Cleans skidmarks for a specific ground material type.
    /// </summary>
    /// <param name="index">Ground material index to clean.</param>
    public void CleanSkidmarks(int index) {

        if (skidmarks == null || index < 0 || index >= skidmarks.Length)
            return;

        if (skidmarks[index])
            skidmarks[index].Clean();

    }

}
