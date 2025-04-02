using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.Buffered;

namespace AntRunnerLib.Functions
{
    /// <summary>
    /// Enumeration for different script types.
    /// </summary>
    public enum ScriptType
    {
        Bash,
        PowerShell,
        Python
    }

    /// <summary>
    /// Class representing the result of script execution.
    /// </summary>
    public class ScriptExecutionResult
    {
        /// <summary>
        /// Gets or sets the standard output.
        /// </summary>
        public string StandardOutput { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the standard error.
        /// </summary>
        public string StandardError { get; set; } = string.Empty;
    }

    /// <summary>
    /// Service to execute scripts inside Docker containers.
    /// </summary>
    public class DockerScriptService
    {
        /// <summary>
        /// Executes a script inside a Docker container and returns the result.
        /// </summary>
        /// <param name="script">The script to execute.</param>
        /// <param name="containerName">The name of the Docker container.</param>
        /// <param name="scriptType">The type of the script (Bash, PowerShell, Python).</param>
        /// <param name="filename">Optional. The name of the file to save the result. If not provided, a GUID will be used.</param>
        /// <returns>A <see cref="ScriptExecutionResult"/> containing the standard output, standard error, and any execution exception.</returns>
        public static async Task<ScriptExecutionResult> ExecuteDockerScriptAsync(string script, string containerName, ScriptType scriptType)
        {
            var stdOutBuffer = new StringBuilder();
            var stdErrBuffer = new StringBuilder();
            Exception? executionException = null;

            // Validate that the script has content
            if (string.IsNullOrWhiteSpace(script))
            {
                stdErrBuffer.AppendLine("Script cannot be null or empty.");
                return new ScriptExecutionResult
                {
                    StandardOutput = stdOutBuffer.ToString(),
                    StandardError = stdErrBuffer.ToString(),
                };
            }

            // Ensure the script uses Unix-style line endings
            script = script.Replace("\r\n", "\n").Replace("\r", "\n");

            // Generate a unique GUID for the working directory
            var guid = Guid.NewGuid().ToString();
            var workingDirectory = $"/app/shared/{guid}";

            try
            {
                string command = scriptType switch
                {
                    ScriptType.Bash => $"bash -c \"mkdir -p {workingDirectory} && cd {workingDirectory} && {script}\"",
                    ScriptType.PowerShell => $"pwsh -c \"New-Item -ItemType Directory -Path {workingDirectory} -Force; Set-Location {workingDirectory}; {script}\"",
                    ScriptType.Python => $"python -c \"import os; os.makedirs('{workingDirectory}', exist_ok=True); os.chdir('{workingDirectory}'); {script}\"",
                    _ => throw new ArgumentOutOfRangeException(nameof(scriptType), scriptType, null)
                };

                var result = await Cli.Wrap("docker")
                    .WithArguments($"exec -w /app {containerName} {command}")
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                    .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErrBuffer))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().ConfigureAwait(false);

                // If the operation completed successfully, read files from the working directory
                if (result.ExitCode == 0)
                {
                    var filesList = await ListFilesInWorkingDirectoryAsync(containerName, workingDirectory, guid).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(filesList))
                    {
                        stdOutBuffer.AppendLine("New Files:");
                        stdOutBuffer.AppendLine(filesList);
                    }
                }
            }
            catch (Exception ex)
            {
                stdErrBuffer.AppendLine($"Error running script in Docker container {containerName}:");
                stdErrBuffer.AppendLine(ex.ToString());
                executionException = ex;
            }

            if (stdOutBuffer.Length == 0 && stdErrBuffer.Length == 0)
            {
                stdOutBuffer.AppendLine("The operation completed successfully");
            }

            var scriptExecutionResult = new ScriptExecutionResult
            {
                StandardOutput = stdOutBuffer.ToString(),
                StandardError = stdErrBuffer.ToString()
            };

            // Save the result to a location set in the environment
            var basePath = Environment.GetEnvironmentVariable("DOCKER_EXEC_LOG_PATH") ?? "/tmp/scripts";
            var scriptFolder = Path.Combine(basePath, scriptType.ToString(), executionException == null ? "success" : "failure");
            Directory.CreateDirectory(scriptFolder);
            var resultFilename = $"{Guid.NewGuid()}.json";
            var resultPath = Path.Combine(scriptFolder, resultFilename);

            // Create a log object to include inputs and response
            var logObject = new
            {
                Script = script,
                ContainerName = containerName,
                ScriptType = scriptType,
                Result = scriptExecutionResult
            };

            var jsonResult = JsonSerializer.Serialize(logObject, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(resultPath, jsonResult).ConfigureAwait(false);

            return scriptExecutionResult;
        }

        /// <summary>
        /// Lists the files in the specified working directory inside the Docker container.
        /// </summary>
        /// <param name="containerName">The name of the Docker container.</param>
        /// <param name="workingDirectory">The working directory to list files from.</param>
        /// <param name="guid">The GUID associated with the working directory.</param>
        /// <returns>A string containing the list of files, one per line.</returns>
        private static async Task<string> ListFilesInWorkingDirectoryAsync(string containerName, string workingDirectory, string guid)
        {
            var stdOutBuffer = new StringBuilder();

            try
            {
                var result = await Cli.Wrap("docker")
                    .WithArguments($"exec {containerName} ls -1 {workingDirectory}")
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOutBuffer))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync().ConfigureAwait(false);

                var files = stdOutBuffer.ToString().Trim().Split('\n');
                var formattedFilesList = new StringBuilder();

                foreach (var file in files)
                {
                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        formattedFilesList.AppendLine($"{guid}/{file}");
                    }
                }

                return formattedFilesList.ToString().Trim();
            }
            catch (Exception ex)
            {
                stdOutBuffer.Clear();
                stdOutBuffer.AppendLine($"Error listing files in Docker container {containerName}:");
                stdOutBuffer.AppendLine(ex.ToString());
            }

            return stdOutBuffer.ToString().Trim();
        }
    }
}