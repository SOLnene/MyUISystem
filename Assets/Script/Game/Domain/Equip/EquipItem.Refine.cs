using Game.Domain.Enhance;

public partial class EquipItem
{
    WeaponRefineRules RefineRules => CloudGameSaveSession.Current?.IsReady == true &&
        CloudGameSaveSession.Current.Source == GameContext.Instance.ActiveSaveDirectory
        ? CloudGameSaveSession.Current.RefineConfig?.rules : null;

    internal int RefineMaterialLimit => RefineRules?.MaxMaterials ?? 4;

    internal void ApplyRefinementState(int refinementLevel)
    {
        // Preserve the instance and subscriptions used by the equipped character and result animation.
        RefinementLevel = refinementLevel;
        RefreshBaseStats(); refineRP.Value = refinementLevel;
    }
}
