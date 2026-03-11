using System.Collections.Generic;

namespace RevitMcp.Contracts;

public sealed record HvacCriticalPathDataResponse(
    long SystemId,
    string SystemName,
    bool IsWellConnected,
    double? TotalCriticalPathPressureLoss,
    string LengthUnit,
    string PressureDropUnit,
    IReadOnlyList<HvacPathSectionDto> Sections,
    IReadOnlyList<HvacPathElementDto> Elements);

public sealed record HvacPathSectionDto(
    int SectionNumber,
    int Sequence,
    double Flow,
    double Velocity,
    IReadOnlyList<long> ElementIds);

public sealed record HvacPathElementDto(
    long ElementId,
    string UniqueId,
    int SectionNumber,
    int Sequence,
    string Category,
    string Name,
    long TypeId,
    string TypeName,
    string ElementKind,
    double? Length,
    double? Diameter,
    double? Width,
    double? Height,
    double? PressureDrop,
    ConnectorProfileDto? PrimaryConnectorProfile);

public sealed record ConnectorProfileDto(
    string Shape,
    double? Width,
    double? Height,
    double? Radius);
