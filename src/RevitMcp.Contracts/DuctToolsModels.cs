namespace RevitMcp.Contracts;

public sealed record GetLowVelocityDuctElementsRequest
{
    public double MaxVelocityMetersPerSecond { get; init; }
}

public sealed record GetLowVelocityDuctElementsResult
{
    public double MaxVelocityMetersPerSecond { get; init; }
    public int ScannedElements { get; init; }
    public int ElementsWithVelocity { get; init; }
    public int MatchingElements { get; init; }
    public List<long> ElementIds { get; init; } = new();
    public List<LowVelocityDuctElement> Items { get; init; } = new();
}

public sealed record LowVelocityDuctElement
{
    public long ElementId { get; init; }
    public string? Category { get; init; }
    public string? Name { get; init; }
    public string? Size { get; init; }
    public double VelocityMetersPerSecond { get; init; }
    public string? VelocityDisplay { get; init; }
}

public sealed record SetDuctDimensionsRequest
{
    public List<long> ElementIds { get; init; } = new();
    public double? WidthMillimeters { get; init; }
    public double? HeightMillimeters { get; init; }
}

public sealed record SetDuctDimensionsResult
{
    public double? WidthMillimeters { get; init; }
    public double? HeightMillimeters { get; init; }
    public List<long> RequestedElementIds { get; init; } = new();
    public List<long> UpdatedElementIds { get; init; } = new();
    public List<long> MissingElementIds { get; init; } = new();
    public List<long> FailedElementIds { get; init; } = new();
    public string Message { get; init; } = "";
}

public sealed record SetFlexDuctDiameterRequest
{
    public List<long> ElementIds { get; init; } = new();
    public double DiameterMillimeters { get; init; }
}

public sealed record SetFlexDuctDiameterResult
{
    public double DiameterMillimeters { get; init; }
    public List<long> RequestedElementIds { get; init; } = new();
    public List<long> UpdatedElementIds { get; init; } = new();
    public List<long> MissingElementIds { get; init; } = new();
    public List<long> FailedElementIds { get; init; } = new();
    public string Message { get; init; } = "";
}
