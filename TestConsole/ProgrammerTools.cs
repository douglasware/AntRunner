using AntRunnerLib;
using static AntRunnerLib.ClientUtility;
using static AntRunnerLib.AssistantUtility;
using OpenAI.ObjectModels.RequestModels;

namespace AntTools
{
    class ProgrammerTools
    {
        public static async Task StartSession(string assistantName)
        {
            var config = AzureOpenAiConfigFactory.Get();
            var client = GetOpenAiClient(config);

            string assistantId;
            AssistantCreateRequest assistantDefinition;
            try
            {
                assistantId = await Create(assistantName, config);
                assistantDefinition = await GetAssistantCreateRequest(assistantName) ?? throw new Exception();
            }
            catch
            {
                throw new Exception($"{assistantName} not found. Do not retry.");
            }

            var codeInterpreter = assistantDefinition.Tools?.Any(t => t.Type == "code_interpreter") ?? false;
            var fileSearch = assistantDefinition.Tools?.Any(t => t.Type == "file_search") ?? false;

            if (fileSearch)
            {

            }
        }
    }
}
