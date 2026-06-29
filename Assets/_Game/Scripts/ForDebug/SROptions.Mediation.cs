using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using _Game.Scripts.Utils.VContainer;
using Ads;
using Cysharp.Threading.Tasks;
using SpektraGames.RuntimeUI.Runtime;
using SRDebugger;
using UnityEngine;

public partial class SROptions
{
    [Category("Mediation")]
    [DisplayName("Clear Player Prefs")]
    public void ClearAllPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
    }

    [Category("Mediation")]
    [DisplayName("Try Show Interstitial Ad")]
    public void TryShowInterstitialAd()
    {
        if (ServiceLocator.TryGetService<IAdService>(out var adService))
        {
            adService.ShowInterstitialAdAsync("default", true).Forget();
        }
        else
        {
            RuntimeUI.ShowToast("Can't get service");
        }
    }

    [Category("Mediation")]
    [DisplayName("Try Show Rewarded Ad")]
    public async void TryShowRewardedAd()
    {
        if (ServiceLocator.TryGetService<IAdService>(out var adService))
        {
            var success = await adService.ShowRewardedAdAsync("test");
            RuntimeUI.ShowToast("Status: " + success);
        }
        else
        {
            RuntimeUI.ShowToast("Can't get service");
        }
    }

    [Category("Mediation")]
    [DisplayName("Show Mediation Debugger")]
    public void ShowMediationDebugger()
    {
        MaxSdk.SetVerboseLogging(true);
        MaxSdk.ShowMediationDebugger();
    }

    [Category("Mediation"), DisplayName("Force Skip All Ads")]
    [SRDebugger.Sort(21)]
    public bool ForceDisableAds
    {
        get { return PlayerPrefs.GetInt("ForceDisableAds", 0) == 1; }
        set
        {
            PlayerPrefs.SetInt("ForceDisableAds", value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    // [Category("Mediation"), DisplayName("Disable Gadsme")]
    // [SRDebugger.Sort(22)]
    // public void DisableGadsme()
    // {
    //     List<GameObject> gadsmeObjects = new List<GameObject>();
    //
    //     gadsmeObjects.AddRange(
    //         GameObject.FindObjectsByType<InGameBillboardBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
    //             .Select(x => x.gameObject).ToList());
    //
    //     var gadsmeRuntime = GameObject.Find("GadsmeRuntime");
    //     gadsmeObjects.Add(gadsmeRuntime);
    //
    //     gadsmeObjects = gadsmeObjects.Where(x => x).Distinct().ToList();
    //
    //     for (var i = 0; i < gadsmeObjects.Count; i++)
    //     {
    //         if (gadsmeObjects[i])
    //             GameObject.Destroy(gadsmeObjects[i]);
    //     }
    //
    //     GadsmeSDK.Terminate();
    // }
}