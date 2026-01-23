using System;
using System.Collections.Generic;

namespace Larchik.Application.Valuations;

public class ValuationResult
{
    public Dictionary<Guid, PositionCost> Positions { get; } = new();
    public Dictionary<Guid, decimal> RealizedByInstrument { get; } = new();
}
