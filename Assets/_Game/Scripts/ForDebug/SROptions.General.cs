using System.ComponentModel;
using Save;

// ReSharper disable once CheckNamespace
public partial class SROptions
{
    private const string GeneralCategory = "General";

    [Category(GeneralCategory)]
    public void AddCurrency()
    {
        int half = int.MaxValue / 2;
        int current = SaveManager.Gold;
        SaveManager.Gold = current > int.MaxValue - half ? int.MaxValue : current + half;
        SaveManager.Save();
    }

    // Editable field in SRDebugger: set the total driven distance to test distance-milestone unlocks.
    // Writing it fires SaveManager.OnDistanceDrivenChanged, which refreshes the menu on its own.
    [Category(GeneralCategory)]
    public int DistanceDrivenKm
    {
        get => SaveManager.DistanceDrivenKm;
        set
        {
            SaveManager.DistanceDrivenKm = value < 0 ? 0 : value;
            SaveManager.Save();
        }
    }

    [Category(GeneralCategory)]
    public void ClearPlayerPrefs()
    {
        SaveManager.ResetAll();
    }
}
