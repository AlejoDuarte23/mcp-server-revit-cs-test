namespace RevitMcp.Contracts;

public sealed record ColorElementsByIdRequest
{
    public List<long> ElementIds { get; init; } = new();
    public byte Red { get; init; }
    public byte Green { get; init; }
    public byte Blue { get; init; }
}

public sealed record ColorElementsByIdResult
{
    public long ViewId { get; init; }
    public string ViewName { get; init; } = "";
    public byte Red { get; init; }
    public byte Green { get; init; }
    public byte Blue { get; init; }
    public List<long> RequestedElementIds { get; init; } = new();
    public List<long> AppliedElementIds { get; init; } = new();
    public List<long> MissingElementIds { get; init; } = new();
    public List<long> FailedElementIds { get; init; } = new();
    public string Message { get; init; } = "";
}
