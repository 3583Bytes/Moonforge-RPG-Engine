using Moonforge.Core.Combat.Events;
using Moonforge.Core.Party.Events;
using Moonforge.Core.Runtime.Commands;
using Moonforge.Core.Runtime.Events;
using Moonforge.Core.Runtime.Results;

namespace Moonforge.Core.Combat.Reactors
{

    /// <summary>
    /// Opts party-joining actors into persistent PP tracking automatically. Watches
    /// <see cref="PartyMemberAddedEvent"/> (any add command) and
    /// <see cref="BattleActorCapturedEvent"/> (capture path bypasses the party command and
    /// mutates the roster directly). Untracked actors stay untracked — wild enemies don't
    /// pollute <see cref="ActorSkillPpState"/>.
    /// </summary>
    public sealed class SkillPpAutoTrackReactor : IDomainEventReactor
    {
        public DomainResult React(GameState gameState, DomainEvent domainEvent, CommandContext context)
        {
            switch (domainEvent)
            {
                case PartyMemberAddedEvent added:
                    gameState.ActorSkillPpState.EnsureTracked(added.ActorId);
                    break;

                case BattleActorCapturedEvent captured:
                    gameState.ActorSkillPpState.EnsureTracked(captured.CapturedActorId);
                    if (captured.CapturedSkillPp is not null)
                    {
                        foreach (System.Collections.Generic.KeyValuePair<string, int> pp in captured.CapturedSkillPp)
                        {
                            gameState.ActorSkillPpState.SetSkillPp(captured.CapturedActorId, pp.Key, pp.Value);
                        }
                    }
                    break;
            }

            return DomainResult.Success();
        }
    }
}
