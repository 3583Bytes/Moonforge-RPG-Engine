using System.Collections.Generic;
using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Party.Queries;

public sealed class GetPartyMembersQuery : IQuery<IReadOnlyList<PartyMember>>
{
    public GetPartyMembersQuery(bool activeOnly = false)
    {
        ActiveOnly = activeOnly;
    }

    public bool ActiveOnly { get; }
}
