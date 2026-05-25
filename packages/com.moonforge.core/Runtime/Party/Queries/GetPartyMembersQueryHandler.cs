using System.Collections.Generic;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Party.Queries;

public sealed class GetPartyMembersQueryHandler : IQueryHandler<GetPartyMembersQuery, IReadOnlyList<PartyMember>>
{
    public IReadOnlyList<PartyMember> Query(GameState gameState, GetPartyMembersQuery query)
    {
        IReadOnlyList<PartyMember> members = gameState.PartyState.Members;
        if (!query.ActiveOnly)
        {
            return members;
        }

        List<PartyMember> active = new();
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].IsActive)
            {
                active.Add(members[i]);
            }
        }

        return active;
    }
}
