using SPTarkov.Server.Core.Models.Spt.Mod;
using SemanticVersioning;

namespace JeroBackpack;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.jero.jerobackpack";
    public string Name { get; init; } = "JeroBackpack";
    public string Author { get; init; } = "silviohmartins";
    public List<string>? Contributors { get; init; }
    public global::SemanticVersioning.Version Version { get; init; } = new global::SemanticVersioning.Version("3.0.0");
    public global::SemanticVersioning.Range SptVersion { get; init; } = new global::SemanticVersioning.Range("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public global::System.Collections.Generic.Dictionary<string, global::SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/silviohmartins/JeroBackpack";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}