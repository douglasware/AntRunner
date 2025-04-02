using Microsoft.AspNetCore.Mvc;
using AntRunnerLib.Functions;

namespace AntRunner.Services
{
    [Route("api/[controller]")]
    [ApiController]
    public class DriveToolsController : ControllerBase
    {
        /// <summary>
        /// Lists all available drives and their details.
        /// </summary>
        /// <returns>A list of DriveDetails objects representing the drives.</returns>
        [HttpGet("drives")]
        public ActionResult<List<DriveDetails>> ListDrives()
        {
            var drives = DriveTools.ListDrives();
            return Ok(drives);
        }

        /// <summary>
        /// Lists all items (files and directories) in a specified path.
        /// </summary>
        /// <param name="path">The path to search for items.</param>
        /// <param name="recurse">Whether to search recursively through subdirectories.</param>
        /// <param name="searchPattern">The search pattern to match against the names of files and directories. Default is "*".</param>
        /// <returns>A list of ItemDetails objects representing the items.</returns>
        [HttpGet("items")]
        public ActionResult<List<ItemDetails>> ListItems(string path, bool recurse, string searchPattern = "*")
        {
            if (string.IsNullOrEmpty(path))
            {
                return BadRequest("Path cannot be null or empty.");
            }

            var items = DriveTools.ListItems(path, recurse, searchPattern);
            return Ok(items);
        }
    }
}