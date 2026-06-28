//----------------------------------------------
//            Realistic Car Controller
//
// Copyright © 2014 - 2025 BoneCracker Games
// https://www.bonecrackergames.com
// Ekrem Bugra Ozdoganlar
//
//----------------------------------------------


using TMPro;
using UnityEngine;

/// <summary>
/// Singleton UI component that displays tooltip descriptions when hovering over level buttons.
/// Used in the AIO demo scene selector to show what each demo scene demonstrates.
/// </summary>
public class RCC_UI_Tooltip : RCC_Singleton<RCC_UI_Tooltip> {

    /// <summary>
    /// TextMeshProUGUI component used to render the tooltip text on screen.
    /// </summary>
    private TextMeshProUGUI text;

    private void Awake() {

        m_Instance = this;
        
        //  Getting text component and disabling it.
        if (TryGetComponent(out text))
            text.enabled = false;

    }

    /// <summary>
    /// Displays a tooltip description on screen.
    /// </summary>
    /// <param name="description">The text to display.</param>
    public void Show(string description) {

        if (!text)
            return;

        text.text = description;
        text.enabled = true;

    }

    /// <summary>
    /// Hides the tooltip and clears the text.
    /// </summary>
    public void Hide() {

        if (!text)
            return;

        text.text = "";
        text.enabled = false;

    }

    private void OnDisable() {

        if (!text)
            return;

        text.text = "";
        text.enabled = false;

    }

}
