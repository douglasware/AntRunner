using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.DotNet.Interactive;
using InteractiveKernel = Microsoft.DotNet.Interactive.Kernel;

public static class Settings
{
    private const string DefaultConfigFile = "config/settings.json";
    private const bool StoreConfigOnFile = true;
    private const string DefaultModel = "gpt-4o";

    private static readonly Dictionary<string, string> SettingsKeys = new Dictionary<string, string>
    {
        { "TypeKey", "type" },
        { "ModelKey", "AZURE_OPENAI_DEPLOYMENT" },
        { "AzureOpenAIResourceName", "AZURE_OPENAI_RESOURCE" },
        { "ApiKey", "AZURE_OPENAI_API_KEY" },
        { "OrgKey", "org" },
        { "ApiVersionKey", "AZURE_OPENAI_API_VERSION" },
        { "AssistantDefinitionsPath", "ASSISTANTS_BASE_FOLDER_PATH" },
        { "SearchApiKey", "SEARCH_API_KEY" }
    };

    private static readonly Dictionary<string, string> SettingsDefaults = new Dictionary<string, string>
    {
        { "TypeKey", "azure" },
        { "ModelKey", DefaultModel },
        { "ApiVersionKey", "2024-05-01-preview" },
        { "OrgKey", "NONE" },
        { "AssistantDefinitionsPath", $"{Directory.GetCurrentDirectory()}\\AssistantDefinitions" }
    };

    private static readonly Dictionary<string, string> SettingsPrompts = new Dictionary<string, string>
    {
        { "TypeKey", $"Please enter the type (azure/openai) (default:{SettingsDefaults["TypeKey"]})" },
        { "ModelKey", $"Please enter your Azure OpenAI model deployment name, e.g. gpt-4o (default:{SettingsDefaults["ModelKey"]})" },
        { "AzureOpenAIResourceName", "Please enter your Azure OpenAI service name, e.g. my-openai-service" },
        { "ApiKey", "Please enter your Azure OpenAI API key" },
        { "AssistantDefinitionsPath", $"Please enter the assistant definitions path (default:{SettingsDefaults["AssistantDefinitionsPath"]})" },
        { "OrgKey", "Please enter your OpenAI Organization Id (enter 'NONE' to skip)" },
        { "ApiVersionKey", $"Please enter your Azure OpenAI API version (default:{SettingsDefaults["ApiVersionKey"]})" },
        { "SearchApiKey", "Please enter your search API key" }
    };

    // Prompt user for a specific setting
    public static async Task<string> AskSetting(string settingKey, bool _useAzureOpenAI = true, string configFile = DefaultConfigFile)
    {
        if (!SettingsKeys.ContainsKey(settingKey))
        {
            throw new ArgumentException($"Invalid setting key: {settingKey}");
        }

        var settings = ReadSettings(_useAzureOpenAI, configFile);

        // Prompt user for the specific setting
        if (string.IsNullOrWhiteSpace(settings[settingKey]))
        {
            var prompt = SettingsPrompts.ContainsKey(settingKey) ? SettingsPrompts[settingKey] : $"Please enter your {SettingsKeys[settingKey]}";
            if (settingKey == "ApiKey" || settingKey == "SearchApiKey")
            {
                settings[settingKey] = (await InteractiveKernel.GetPasswordAsync(prompt)).GetClearTextPassword();
            }
            else
            {
                try
                {
                    settings[settingKey] = await InteractiveKernel.GetInputAsync(prompt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("1");
                    Console.WriteLine(SettingsDefaults[settingKey]);
                    if(SettingsDefaults[settingKey] != null && string.IsNullOrWhiteSpace(settings[settingKey]))
                    {
                        Console.WriteLine("2");
                        settings[settingKey] = SettingsDefaults[settingKey];
                    }
                    else throw;
                }
            }
        }

        WriteSettings(configFile, settings);

        // Set the environment variable
        Environment.SetEnvironmentVariable(SettingsKeys[settingKey], settings[settingKey]);

        // Print report
        Console.WriteLine("Settings: " + (string.IsNullOrWhiteSpace(settings[settingKey])
            ? $"ERROR: {SettingsKeys[settingKey]} is empty"
            : $"OK: {SettingsKeys[settingKey]} configured [{configFile}]"));

        return settings[settingKey];
    }

    // Load settings from file
    public static Dictionary<string, string> LoadFromFile(string configFile = DefaultConfigFile)
    {
        if (!File.Exists(configFile))
        {
            Console.WriteLine("Configuration not found: " + configFile);
            Console.WriteLine("\nPlease run the Setup Notebook (0-AI-settings.ipynb) to configure your AI backend first.\n");
            throw new Exception("Configuration not found, please setup the notebooks first using notebook 0-AI-settings.pynb");
        }

        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configFile));
            return config;
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
            return new Dictionary<string, string>();
        }
    }

    // Get environment variables dictionary
    public static Dictionary<string, string> GetEnvironmentVariables(bool _useAzureOpenAI = true, string configFile = DefaultConfigFile)
    {
        var settings = ReadSettings(_useAzureOpenAI, configFile);
        var envVariables = new Dictionary<string, string>();

        foreach (var key in settings.Keys)
        {
            if (!string.IsNullOrWhiteSpace(settings[key]) && SettingsKeys.ContainsKey(key))
            {
                envVariables[SettingsKeys[key]] = settings[key];
            }
        }

        return envVariables;
    }

    // Delete settings file
    public static void Reset(string configFile = DefaultConfigFile)
    {
        if (!File.Exists(configFile)) { return; }

        try
        {
            File.Delete(configFile);
            Console.WriteLine("Settings deleted. Run the notebook again to configure your AI backend.");
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
        }
    }

    // Read and return settings from file
    private static Dictionary<string, string> ReadSettings(bool _useAzureOpenAI, string configFile)
    {
        var settings = new Dictionary<string, string>();

        try
        {
            if (File.Exists(configFile))
            {
                settings = LoadFromFile(configFile);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
        }

        // Fill in missing settings with default values
        foreach (var key in SettingsKeys.Keys)
        {
            if (!settings.ContainsKey(key))
            {
                settings[key] = "";
            }
        }

        // If the preference in the notebook is different from the value on file, then reset
        if ((_useAzureOpenAI && settings["TypeKey"] != "azure") || (!_useAzureOpenAI && settings["TypeKey"] != "openai"))
        {
            Reset(configFile);
            foreach (var key in SettingsKeys.Keys)
            {
                settings[key] = "";
            }
            settings["TypeKey"] = _useAzureOpenAI ? "azure" : "openai";
        }

        // Set environment variables
        foreach (var key in settings.Keys)
        {
            Environment.SetEnvironmentVariable(SettingsKeys[key], settings[key]);
        }

        return settings;
    }

    // Write settings to file
    private static void WriteSettings(string configFile, Dictionary<string, string> settings)
    {
        try
        {
            if (StoreConfigOnFile)
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(configFile, JsonSerializer.Serialize(settings, options));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Something went wrong: " + e.Message);
        }

        // If asked then delete the credentials stored on disk
        if (!StoreConfigOnFile && File.Exists(configFile))
        {
            try
            {
                File.Delete(configFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Something went wrong: " + e.Message);
            }
        }
    }
}
