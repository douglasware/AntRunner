using Microsoft.AspNetCore.Mvc;
using AntRunnerLib.Functions;

namespace AntRunner.Services
{
    [Route("sandbox")]
    [ApiController]
    public class DockerScriptController : ControllerBase
    {
        [HttpPost("run")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScript([FromBody] ScriptExecutionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                request.ContainerName,
                request.ScriptType
            );

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