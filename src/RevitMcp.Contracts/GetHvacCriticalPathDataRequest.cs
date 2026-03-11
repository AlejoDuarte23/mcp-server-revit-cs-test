namespace RevitMcp.Contracts;

public sealed record GetHvacCriticalPathDataRequest(
    long? SystemId,
    long? ElementId);
