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
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton UI component that displays temporary informational text messages on screen.
/// Messages appear for a brief duration (1.5 seconds) before automatically hiding.
/// </summary>
public class RCC_InfoLabel : RCC_Singleton<RCC_InfoLabel> {

    /// <summary>
    /// TextMeshProUGUI component used to render the info message on screen.
    /// </summary>
    private TextMeshProUGUI text;

    /// <summary>
    /// Timer tracking how long since the last message was shown. When below 1.5 seconds, the text is visible.
    /// </summary>
    private float timer = 1.5f;

    private void Awake() {
        m_Instance = this;
        //  Getting text component and disabling it.
        if (TryGetComponent(out text))
            text.enabled = false;

    }

    private void OnEnable() {

        if (text)
            text.text = "";

        timer = 1.5f;

    }

    private void Update() {

        //  If no text component found, return.
        if (!text)
            return;

        //  If timer is below 1.5, text is enabled. Otherwise disable.
        if (timer < 1.5f) {

            if (!text.enabled)
                text.enabled = true;

        } else {

            if (text.enabled)
                text.enabled = false;

        }

        //  Increasing timer.
        timer += Time.deltaTime;

    }

    /// <summary>
    /// Displays an informational message on screen for a brief duration.
    /// </summary>
    /// <param name="info">The text message to display.</param>
    public void ShowInfo(string info) {

        timer = 0f;

        //  If no text found, return.
        if (!text)
            return;

        //  Display info.
        text.text = info;

    }

    private void OnDisable() {

        timer = 1.5f;

        //  If no text found, return.
        if (!text)
            return;

        text.text = "";

    }

}
