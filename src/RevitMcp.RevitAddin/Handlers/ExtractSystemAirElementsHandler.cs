using Autodesk.Revit.UI;
using RevitMcp.Core.Services;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Handlers;

public sealed class ExtractSystemAirElementsHandler : IRevitCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string Name => "extract_system_air_elements";

    public object Execute(UIApplication uiApp, JsonElement args)
    {
        var doc = uiApp.ActiveUIDocument?.Document
            ?? throw new InvalidOperationException("No active document.");

        var systemName = GetRequiredString(args, "systemName");
        var outputDir = GetOptionalString(args, "outputDir");

        var service = new SystemAirElementsExtractionService();
        var result = service.ExtractAndColor(doc, systemName);

        var filePath = SaveResultToDisk(result, systemName, outputDir);
        return new
        {
            Message = $"Data was extracted in the following path: {filePath}",
            FilePath = filePath,
            MatchedElements = result.Summary.MatchedElements,
            ColoredElements = result.Summary.ColoredElements
        };
    }

    private static string SaveResultToDisk(
        SystemAirElementsExtractionResult result,
        string systemName,
        string? outputDir)
    {
        var baseDir = ResolveOutputDirectory(outputDir);
        Directory.CreateDirectory(baseDir);

        var safeSystemName = MakeSafeFileName(systemName);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var fileName = $"system-air-elements-{safeSystemName}-{timestamp}.json";
        var filePath = Path.Combine(baseDir, fileName);

        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(filePath, json);
        return filePath;
    }

    private static string ResolveOutputDirectory(string? outputDir)
    {
        if (!string.IsNullOrWhiteSpace(outputDir))
        {
            return outputDir.Trim();
        }

        var envDir = Environment.GetEnvironmentVariable("REVIT_MCP_EXPORT_DIR");
        if (!string.IsNullOrWhiteSpace(envDir))
        {
            return envDir.Trim();
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RevitMcp",
            "exports");
    }

    private static string MakeSafeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "system" : result;
    }

    private static string GetRequiredString(JsonElement args, string propertyName)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        throw new ArgumentException($"'{propertyName}' is required.");
    }

    private static string? GetOptionalString(JsonElement args, string propertyName)
    {
        if (args.ValueKind == JsonValueKind.Object &&
            args.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }
}
