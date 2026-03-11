using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Mechanical;
using RevitMcp.Contracts;

namespace RevitMcp.Core.Services;

public sealed class HvacCriticalPathService
{
    public HvacCriticalPathDataResponse GetCriticalPathData(Document doc, GetHvacCriticalPathDataRequest request)
    {
        if (request.SystemId is null && request.ElementId is null)
        {
            throw new InvalidOperationException("Either system_id or element_id is required.");
        }

        var system = ResolveMechanicalSystem(doc, request)
            ?? throw new InvalidOperationException("Could not resolve a mechanical system from the provided input.");

        if (!system.IsWellConnected)
        {
            throw new InvalidOperationException("The mechanical system is not well connected, so Revit critical-path values are not valid.");
        }

        var sectionNumbers = system.GetCriticalPathSectionNumbers();
        var sections = new List<HvacPathSectionDto>();
        var elements = new List<HvacPathElementDto>();

        for (var index = 0; index < sectionNumbers.Count; index++)
        {
            var sectionNumber = sectionNumbers[index];
            var section = system.GetSectionByNumber(sectionNumber);
            if (section is null)
            {
                continue;
            }

            var sequence = index + 1;
            var elementIds = section.GetElementIds()
                .Select(id => id.Value)
                .ToList();

            sections.Add(new HvacPathSectionDto(
                SectionNumber: sectionNumber,
                Sequence: sequence,
                Flow: SafeGet(() => section.Flow),
                Velocity: SafeGet(() => section.Velocity),
                ElementIds: elementIds));

            foreach (var elementId in section.GetElementIds())
            {
                var element = doc.GetElement(elementId);
                if (element is null)
                {
                    continue;
                }

                var typeId = element.GetTypeId();
                var typeElement = typeId != ElementId.InvalidElementId ? doc.GetElement(typeId) : null;

                elements.Add(new HvacPathElementDto(
                    ElementId: element.Id.Value,
                    UniqueId: element.UniqueId,
                    SectionNumber: sectionNumber,
                    Sequence: sequence,
                    Category: element.Category?.Name ?? string.Empty,
                    Name: element.Name ?? string.Empty,
                    TypeId: typeId != ElementId.InvalidElementId ? typeId.Value : -1,
                    TypeName: typeElement?.Name ?? string.Empty,
                    ElementKind: GetElementKind(element),
                    Length: GetLength(element),
                    Diameter: GetDoubleParameter(element, BuiltInParameter.RBS_CURVE_DIAMETER_PARAM),
                    Width: GetDoubleParameter(element, BuiltInParameter.RBS_CURVE_WIDTH_PARAM),
                    Height: GetDoubleParameter(element, BuiltInParameter.RBS_CURVE_HEIGHT_PARAM),
                    PressureDrop: SafeGet(() => section.GetPressureDrop(elementId)),
                    PrimaryConnectorProfile: GetPrimaryConnectorProfile(element)));
            }
        }

        return new HvacCriticalPathDataResponse(
            SystemId: system.Id.Value,
            SystemName: system.Name ?? string.Empty,
            IsWellConnected: system.IsWellConnected,
            TotalCriticalPathPressureLoss: SafeGet(() => system.PressureLossOfCriticalPath()),
            LengthUnit: "internal_feet",
            PressureDropUnit: "internal_hvac_pressure",
            Sections: sections,
            Elements: elements);
    }

    private static MechanicalSystem? ResolveMechanicalSystem(Document doc, GetHvacCriticalPathDataRequest request)
    {
        if (request.SystemId is not null)
        {
            return doc.GetElement(new ElementId(request.SystemId.Value)) as MechanicalSystem;
        }

        if (request.ElementId is null)
        {
            return null;
        }

        var element = doc.GetElement(new ElementId(request.ElementId.Value));
        if (element is null)
        {
            return null;
        }

        if (element is MechanicalSystem system)
        {
            return system;
        }

        if (element is MEPCurve curve)
        {
            return curve.MEPSystem as MechanicalSystem ?? ResolveMechanicalSystem(curve.ConnectorManager);
        }

        if (element is FamilyInstance familyInstance)
        {
            return ResolveMechanicalSystem(familyInstance.MEPModel?.ConnectorManager);
        }

        return null;
    }

    private static MechanicalSystem? ResolveMechanicalSystem(ConnectorManager? connectorManager)
    {
        if (connectorManager is null)
        {
            return null;
        }

        foreach (Connector connector in connectorManager.Connectors)
        {
            if (connector.MEPSystem is MechanicalSystem system)
            {
                return system;
            }
        }

        return null;
    }

    private static ConnectorProfileDto? GetPrimaryConnectorProfile(Element element)
    {
        ConnectorManager? connectorManager = element switch
        {
            MEPCurve curve => curve.ConnectorManager,
            FamilyInstance familyInstance => familyInstance.MEPModel?.ConnectorManager,
            _ => null
        };

        if (connectorManager is null)
        {
            return null;
        }

        foreach (Connector connector in connectorManager.Connectors)
        {
            return new ConnectorProfileDto(
                Shape: connector.Shape.ToString(),
                Width: SafeGetNullable(() => connector.Width),
                Height: SafeGetNullable(() => connector.Height),
                Radius: SafeGetNullable(() => connector.Radius));
        }

        return null;
    }

    private static string GetElementKind(Element element)
    {
        return element.Category?.Id.Value switch
        {
            (long)BuiltInCategory.OST_DuctCurves => "duct",
            (long)BuiltInCategory.OST_FlexDuctCurves => "flex_duct",
            (long)BuiltInCategory.OST_DuctFitting => "duct_fitting",
            (long)BuiltInCategory.OST_DuctAccessory => "duct_accessory",
            (long)BuiltInCategory.OST_DuctTerminal => "duct_terminal",
            _ => element.GetType().Name
        };
    }

    private static double? GetLength(Element element)
    {
        return GetDoubleParameter(element, BuiltInParameter.CURVE_ELEM_LENGTH)
            ?? GetDoubleParameter(element, BuiltInParameter.RBS_CURVE_LENGTH_PARAM);
    }

    private static double? GetDoubleParameter(Element element, BuiltInParameter parameterId)
    {
        var parameter = element.get_Parameter(parameterId);
        if (parameter is null || parameter.StorageType != StorageType.Double)
        {
            return null;
        }

        return parameter.AsDouble();
    }

    private static double SafeGet(Func<double> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return 0.0;
        }
    }

    private static double? SafeGetNullable(Func<double> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
