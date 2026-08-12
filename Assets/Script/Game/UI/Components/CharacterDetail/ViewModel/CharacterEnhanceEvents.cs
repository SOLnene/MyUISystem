namespace Game.UI.Components.CharacterDetail
{
    internal readonly struct CharacterQuickFillCompletedEvent : IEvent
    {
        public readonly int TotalExp;

        public CharacterQuickFillCompletedEvent(int totalExp)
        {
            TotalExp = totalExp;
        }
    }

    internal readonly struct CharacterEnhanceCompletedEvent : IEvent
    {
        public readonly EnhanceResultData Result;

        public CharacterEnhanceCompletedEvent(EnhanceResultData result)
        {
            Result = result;
        }
    }
}
