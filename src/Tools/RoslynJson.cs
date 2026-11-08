using System.Text.Json;
using System.Text.Json.Serialization;

namespace Snail.MCP.Roslyn.Tools;

/// <summary>The records this server's tools answer with, generated at build time and written as the data of the envelope.</summary>
/// <remarks>
/// Spelled as the MCP SDK wrote them when the tools returned the records themselves — camel case, an enum by its name, a
/// field without a value left out — so the data an agent reads did not change when the envelope came around it.
/// </remarks>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(WorkspaceReport))]
[JsonSerializable(typeof(UnloadReport))]
[JsonSerializable(typeof(IssuePage))]
[JsonSerializable(typeof(FileView))]
[JsonSerializable(typeof(SolutionOverview))]
[JsonSerializable(typeof(ArchitectureReport))]
[JsonSerializable(typeof(HealthReport))]
[JsonSerializable(typeof(IReadOnlyList<SymbolMatch>))]
[JsonSerializable(typeof(SymbolDetails))]
[JsonSerializable(typeof(SymbolSource))]
[JsonSerializable(typeof(ReferencePage))]
[JsonSerializable(typeof(TypeHierarchy))]
[JsonSerializable(typeof(GeneratedCodePage))]
[JsonSerializable(typeof(ExtractionPlan))]
[JsonSerializable(typeof(RenamePlan))]
[JsonSerializable(typeof(EditPlan))]
[JsonSerializable(typeof(ImpactReport))]
[JsonSerializable(typeof(AnalyzerPage))]
[JsonSerializable(typeof(CodeFixPage))]
[JsonSerializable(typeof(ValidationResult))]
[JsonSerializable(typeof(RegistrationReport))]
[JsonSerializable(typeof(AuditPage))]
[JsonSerializable(typeof(ScriptResult))]
internal sealed partial class RoslynJson : JsonSerializerContext;
