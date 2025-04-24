using AntRunnerLib;
using Microsoft.AspNetCore.Mvc;

namespace AntRunner.Services.Controllers
{
    [Route("assistants")]
    [ApiController]
    public class AssistantRunThreadController : ControllerBase
    {
        private readonly AzureOpenAiConfig _config;

        public AssistantRunThreadController()
        {
            // Load your AzureOpenAiConfig here
            _config = AzureOpenAiConfigFactory.Get();
        }

        [HttpPost("run/{assistantName}")]
        public async Task<IActionResult> RunThread(string assistantName, [FromBody] RunThreadRequest request)
        {
            if (string.IsNullOrWhiteSpace(assistantName))
            {
                return BadRequest("Assistant name is required");
            }

            if (request == null)
            {
                return BadRequest("Request body is required");
            }

            if (string.IsNullOrWhiteSpace(request.Instructions))
            {
                return BadRequest("The request has no instructions");
            }

            try
            {
                var output = await AssistantRunner.RunThread(assistantName, request.Instructions, request.Evaluator);
                if (!string.IsNullOrWhiteSpace(output))
                {
                    return Ok(output);
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

    public class RunThreadRequest
    {
        public string? Instructions { get; set; }
        public string? Evaluator { get; set; }
    }
}