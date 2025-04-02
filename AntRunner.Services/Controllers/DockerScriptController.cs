using Microsoft.AspNetCore.Mvc;
using AntRunnerLib.Functions;

namespace AntRunner.Services
{
    [Route("api/[controller]")]
    [ApiController]
    public class DockerScriptController : ControllerBase
    {
        [HttpPost("execute")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScript([FromBody] ScriptExecutionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var result = await DockerScriptService.ExecuteDockerScriptAsync(
                request.Script,
                request.ContainerName,
                request.ScriptType,
                request.Filename
            );

            if (result.ExecutionException != null)
            {
                return StatusCode(500, result);
            }

            return Ok(result);
        }
    }

    public class ScriptExecutionRequest
    {
        public string Script { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public ScriptType ScriptType { get; set; }
        public string? Filename { get; set; }
    }
}