using System;
using System.IO;

namespace AntRunner.ToolCalling.AssistantDefinitions.Storage
{
    internal static class EmbeddedResourceStorage
    {
        internal static string? GetManifest(string assistantName)
        {
            var assistantDir = Path.Combine(AppContext.BaseDirectory, "Assistants", assistantName);

            var namedFile = Path.Combine(assistantDir, $"{assistantName}.json");
            if (File.Exists(namedFile))
                return File.ReadAllText(namedFile);

            var fallback = Path.Combine(assistantDir, "manifest.json");
            if (File.Exists(fallback))
                return File.ReadAllText(fallback);

            return null;
        }

        internal static string? GetInstructions(string assistantName)
        {
            var assistantDir = Path.Combine(AppContext.BaseDirectory, "Assistants", assistantName);

            var namedFile = Path.Combine(assistantDir, $"{assistantName}.md");
            if (File.Exists(namedFile))
                return File.ReadAllText(namedFile);

            var fallback = Path.Combine(assistantDir, "instructions.md");
            if (File.Exists(fallback))
                return File.ReadAllText(fallback);

            return null;
        }
    }
}