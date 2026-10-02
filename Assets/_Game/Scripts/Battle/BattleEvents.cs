namespace MergeLegion.Battle
{
    public enum BattlePhase { Prepare, Fighting, Result }

    public readonly struct BattlePhaseEvent
    {
        public readonly BattlePhase Phase;
        public BattlePhaseEvent(BattlePhase phase) { Phase = phase; }
    }

    public readonly struct SkillUsedEvent { }
}
