using Microsoft.AspNetCore.Mvc;
using AntRunnerLib.Functions;

namespace AntRunner.Services
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileToolsController : ControllerBase
    {
        /// <summary>
        /// Gets the content type and binary status for a given file based on its extension.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>A FileContentType object containing the content type and binary status.</returns>
        [HttpGet("content-type")]
        public ActionResult<FileContentType> GetContentType(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest("File path cannot be null or empty.");
            }

            try
            {
                var contentType = FileTypeHelper.GetContentType(filePath);
                return Ok(contentType);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Retrieves the details of a file at the given path.
        /// </summary>
        /// <param name="filePath">The path of the file.</param>
        /// <returns>A FileDetails object containing the file's details.</returns>
        [HttpGet("details")]
        public ActionResult<FileDetails> GetFileDetails(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return BadRequest("File path cannot be null or empty.");
            }

            try
            {
                var fileDetails = FileDetails.Get(filePath);
                return Ok(fileDetails);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Writes content to a file at the given path.
        /// </summary>
        /// <param name="request">The request containing the path and content.</param>
        /// <returns>An IActionResult indicating the result of the operation.</returns>
        [HttpPost("write")]
        public IActionResult WriteFile([FromBody] WriteFileRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.Path) || string.IsNullOrEmpty(request.Content))
            {
                return BadRequest("Request, path, and content cannot be null or empty.");
            }

            try
            {
                FileDetails.WriteFile(request.Path, request.Content);
                return Ok("File written successfully.");
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }

    public class WriteFileRequest
    {
        public string Path { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}