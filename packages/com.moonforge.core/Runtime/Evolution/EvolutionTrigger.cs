namespace Moonforge.Core.Evolution;

/// <summary>
/// How an <see cref="Data.Definitions.EvolutionDefinition"/> is activated. <see cref="LevelUp"/>
/// fires automatically when the actor's level crosses the required threshold via a
/// <see cref="Progression.Events.LevelUpEvent"/>; <see cref="Manual"/> requires the game to
/// dispatch <see cref="Commands.TriggerEvolutionCommand"/> explicitly (use for item-driven
/// or scripted evolutions).
/// </summary>
public enum EvolutionTrigger
{
    LevelUp = 0,
    Manual = 1
}
