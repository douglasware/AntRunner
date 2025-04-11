using AntRunnerLib;
using Microsoft.AspNetCore.Mvc;

namespace AntRunner.Services.Controllers
{
    [Route("chat")]
    [ApiController]
    public class ChatRunThreadController : ControllerBase
    {
        private readonly AzureOpenAiConfig _config;

        public ChatRunThreadController()
        {
            // Load your AzureOpenAiConfig here
            _config = AzureOpenAiConfigFactory.Get();
        }

        [HttpPost("run/{assistantName}")]
        public async Task<IActionResult> RunThread(string assistantName, [FromBody] ChatRunOptions chatRunOptions)
        {
            if (string.IsNullOrWhiteSpace(assistantName))
            {
                return BadRequest("Assistant name is required");
            }

            if (chatRunOptions == null)
            {
                return BadRequest("Request body is required");
            }

            if (string.IsNullOrWhiteSpace(chatRunOptions.Instructions))
            {
                return BadRequest("The request has no instructions");
            }

            chatRunOptions.AssistantName = assistantName;

            try
            {
                var output = await ChatRunner.RunThread(chatRunOptions, _config);
                if (output != null)
                {
                    return Ok(output.LastMessage);
                }

                return BadRequest("Unable to process request");
            }
            catch (Exception ex)
            {
                // Handle exceptions accordingly
                return StatusCode(500, ex.Message);
            }
        }
    }
}