using Microsoft.AspNetCore.Mvc;
using AntRunnerLib.Functions;

namespace AntRunner.Services
{

    [ApiController]
    [Route("sandbox")]
    public class GenericSandbox : ControllerBase
    {
        [HttpPost("run/{containerName}/{scriptType}")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScriptWithContainer(
            [FromBody] ScriptExecutionRequest request,
            string containerName,
            ScriptType scriptType)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                scriptType
            );

            return Ok(result);
        }
    }

    [Route("sandbox/bash")]
    [ApiController]
    public class BashScriptController : ControllerBase
    {
        [HttpPost("run")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScript([FromBody] ScriptExecutionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var containerName = Environment.GetEnvironmentVariable("BASH_CONTAINER_NAME");
            if (string.IsNullOrEmpty(containerName))
            {
                return StatusCode(500, "Bash container name is not configured.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                ScriptType.Bash
            );

            return Ok(result);
        }

        [HttpPost("run/{containerName}")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScriptWithContainer([FromBody] ScriptExecutionRequest request, string containerName)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                ScriptType.Bash
            );

            return Ok(result);
        }
    }

    [Route("sandbox/powershell")]
    [ApiController]
    public class PowerShellScriptController : ControllerBase
    {
        [HttpPost("run")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScript([FromBody] ScriptExecutionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var containerName = Environment.GetEnvironmentVariable("POWERSHELL_CONTAINER_NAME");
            if (string.IsNullOrEmpty(containerName))
            {
                return StatusCode(500, "PowerShell container name is not configured.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                ScriptType.PowerShell
            );

            return Ok(result);
        }

        [HttpPost("run/{containerName}")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScriptWithContainer([FromBody] ScriptExecutionRequest request, string containerName)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                ScriptType.PowerShell
            );

            return Ok(result);
        }
    }

    [Route("sandbox/python")]
    [ApiController]
    public class PythonScriptController : ControllerBase
    {
        [HttpPost("run")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScript([FromBody] ScriptExecutionRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var containerName = Environment.GetEnvironmentVariable("PYTHON_CONTAINER_NAME");
            if (string.IsNullOrEmpty(containerName))
            {
                return StatusCode(500, "Python container name is not configured.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                ScriptType.Python
            );

            return Ok(result);
        }

        [HttpPost("run/{containerName}")]
        public async Task<ActionResult<ScriptExecutionResult>> ExecuteScriptWithContainer([FromBody] ScriptExecutionRequest request, string containerName)
        {
            if (request == null)
            {
                return BadRequest("Request cannot be null.");
            }

            var result = await DockerScriptService.ExecuteDockerScript(
                request.Script,
                containerName,
                ScriptType.Python
            );

            return Ok(result);
        }
    }

    public class ScriptExecutionRequest
    {
        public string Script { get; set; } = string.Empty;
    }
}