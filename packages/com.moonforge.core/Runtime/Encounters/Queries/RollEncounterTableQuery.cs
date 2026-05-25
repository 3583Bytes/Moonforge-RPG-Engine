using Moonforge.Core.Runtime.Queries;

namespace Moonforge.Core.Encounters.Queries;

public sealed class RollEncounterTableQuery : IQuery<EncounterRollResult>
{
    public RollEncounterTableQuery(string tableId)
    {
        TableId = tableId;
    }

    public string TableId { get; }
}
