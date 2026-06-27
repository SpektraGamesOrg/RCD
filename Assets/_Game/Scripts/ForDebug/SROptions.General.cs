using System.ComponentModel;
using Milestones;
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

    // Editable field in SRDebugger: how many distance milestones have been claimed (rewarded). Lower it
    // (e.g. to 0) to re-arm milestones, then bump DistanceDrivenKm above a threshold to make them pending
    // again, and Claim them below.
    [Category(GeneralCategory)]
    public int MilestonesClaimed
    {
        get => SaveManager.DistanceMilestonesClaimed;
        set
        {
            SaveManager.DistanceMilestonesClaimed = value < 0 ? 0 : value;
            SaveManager.Save();
        }
    }

    // Read-only: how many reached-but-unclaimed milestones are queued, waiting for the pop-up to claim.
    [Category(GeneralCategory)]
    public int PendingMilestones => DistanceMilestoneManager.PendingCount;

    // Claims the oldest pending milestone at its base reward (simulates the pop-up "Claim" button / timer).
    [Category(GeneralCategory)]
    public void ClaimNextMilestone()
    {
        DistanceMilestoneManager.ClaimNextPending();
    }

    // Claims the oldest pending milestone at the 3X reward (simulates a successful "3X" rewarded ad).
    [Category(GeneralCategory)]
    public void ClaimNextMilestone3X()
    {
        DistanceMilestoneManager.ClaimNextPendingWithAdBonus();
    }

    [Category(GeneralCategory)]
    public void ClearPlayerPrefs()
    {
        SaveManager.ResetAll();
    }
}
