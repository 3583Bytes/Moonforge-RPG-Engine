using System.Collections.Generic;

namespace Moonforge.Core.Data.Definitions;

public sealed class PriceOptionDefinition
{
    public PriceOptionDefinition(IReadOnlyList<PriceComponentDefinition>? components = null)
    {
        Components = components ?? System.Array.Empty<PriceComponentDefinition>();
    }

    public IReadOnlyList<PriceComponentDefinition> Components { get; }
}
