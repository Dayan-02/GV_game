// Run through Unity Pipeline run_script; kept outside Assets to avoid a domain reload.
public static class BuildCombatPresentation
{
    public static void ApplyHandling()
    {
        CombatHandlingDeathSetup.Apply();
    }

    public static void Build()
    {
        OpeningVideoSetup.BuildWindows();
    }
}
