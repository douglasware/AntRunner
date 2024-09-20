using AntRunnerLib;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;

namespace AntRunnerFunctions
{
    /// <summary>
    /// REST API for running assistants
    /// </summary>
    public static class WebApi
    {

        /// <summary>
        /// HTTP trigger to start a new orchestration instance with the provided assistant options.
        /// </summary>
        /// <param name="req">The HTTP request data.</param>
        /// <param name="client">The durable task client.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>The HTTP response data.</returns>
        [Function(nameof(RunAssistantAsync))]
        public static async Task<HttpResponseData> RunAssistantAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            [DurableClient] DurableTaskClient client,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("RunAssistant");

            // Read and validate the request content
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            AssistantRunOptions? assistantRunOptions;

            try
            {
                assistantRunOptions = JsonSerializer.Deserialize<AssistantRunOptions>(requestBody);
                if (assistantRunOptions == null)
                {
                    throw new Exception($"Can't serialze body to AssistantRunOptions: {requestBody}");
                }

                if (req.Headers.Contains("Authorization")) assistantRunOptions.OauthUserAccessToken = req.Headers.TryGetValues("Authorization", out IEnumerable<string>? values) ? values.First() : null;

                // Validate the deserialized object
                var validationContext = new ValidationContext(assistantRunOptions, serviceProvider: null, items: null);
                Validator.ValidateObject(assistantRunOptions, validationContext, validateAllProperties: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invalid request body.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body.");
                return badRequestResponse;
            }

            // Schedule the new orchestration instance
            string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
                nameof(AssistantRunner.AssistantsRunnerOrchestrator), assistantRunOptions);

            logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

            return await client.CreateCheckStatusResponseAsync(req, instanceId);
        }

        /// <summary>
        /// HTTP trigger to run an assitant with the provided options.
        /// </summary>
        /// <param name="req">The HTTP request data.</param>
        /// <param name="executionContext">The function execution context.</param>
        /// <returns>The HTTP response data.</returns>
        [Function(nameof(RunAssistant))]
        public static async Task<HttpResponseData> RunAssistant(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            ILogger logger = executionContext.GetLogger("RunAssistant");

            // Read and validate the request content
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            logger.LogInformation("RunAssistant got request: {requestBody}", requestBody);

            try
            {
                var assistantRunOptions = JsonSerializer.Deserialize<AssistantRunOptions>(requestBody);

                var config = AzureOpenAiConfigFactory.Get();
                if (assistantRunOptions == null)
                {
                    throw new Exception($"Can't serialze body to AssistantRunOptions: {requestBody}");
                }

                if (req.Headers.Contains("Authorization")) assistantRunOptions.OauthUserAccessToken = req.Headers.TryGetValues("Authorization", out IEnumerable<string>? values) ? values.First() : null;

                // Validate the deserialized object
                var validationContext = new ValidationContext(assistantRunOptions, serviceProvider: null, items: null);
                Validator.ValidateObject(assistantRunOptions, validationContext, validateAllProperties: true);

                var assistantId = await AssistantUtility.GetAssistantId(assistantRunOptions!.AssistantName, config, false);
                if (assistantId == null)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync($"{assistantRunOptions!.AssistantName} not found");
                    return notFoundResponse;
                }

                var output = await AntRunnerLib.AssistantRunner.RunThread(assistantRunOptions, config, false);

                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(output);
                return successResponse;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invalid request body.");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body.");
                return badRequestResponse;
            }
        }
    }
}
