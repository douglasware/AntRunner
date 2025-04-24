namespace TestConsole
{
    public class FileContentType
    {
        public string? ContentType { get; set; }
        public bool IsBinary { get; set; }
    }

    public class FileTypeHelper
    {
        private static readonly Dictionary<string, FileContentType> _fileExtensionMappings = new()
        {
            { "aspx", new FileContentType { ContentType = "text/html", IsBinary = false } },
            { "master", new FileContentType { ContentType = "text/html", IsBinary = false } },
            { "ascx", new FileContentType { ContentType = "text/html", IsBinary = false } },
            { "cs", new FileContentType { ContentType = "text/plain", IsBinary = false } },
            { "xsd", new FileContentType { ContentType = "application/xml", IsBinary = false } },
            { "htm", new FileContentType { ContentType = "text/html", IsBinary = false } },
            { "mdf", new FileContentType { ContentType = "application/octet-stream", IsBinary = true } },
            { "config", new FileContentType { ContentType = "application/xml", IsBinary = false } },
            { "asmx", new FileContentType { ContentType = "text/xml", IsBinary = false } },
            { "xml", new FileContentType { ContentType = "application/xml", IsBinary = false } },
            { "sln", new FileContentType { ContentType = "text/plain", IsBinary = false } },
            { "csproj", new FileContentType { ContentType = "text/plain", IsBinary = false } },
            { "c", new FileContentType { ContentType = "text/x-c", IsBinary = false } },
            { "cpp", new FileContentType { ContentType = "text/x-c++", IsBinary = false } },
            { "css", new FileContentType { ContentType = "text/css", IsBinary = false } },
            { "csv", new FileContentType { ContentType = "text/csv", IsBinary = false } },
            { "docx", new FileContentType { ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document", IsBinary = true } },
            { "gif", new FileContentType { ContentType = "image/gif", IsBinary = true } },
            { "go", new FileContentType { ContentType = "text/x-go", IsBinary = false } },
            { "html", new FileContentType { ContentType = "text/html", IsBinary = false } },
            { "java", new FileContentType { ContentType = "text/x-java-source", IsBinary = false } },
            { "jpeg", new FileContentType { ContentType = "image/jpeg", IsBinary = true } },
            { "jpg", new FileContentType { ContentType = "image/jpeg", IsBinary = true } },
            { "js", new FileContentType { ContentType = "application/javascript", IsBinary = false } },
            { "json", new FileContentType { ContentType = "application/json", IsBinary = false } },
            { "md", new FileContentType { ContentType = "text/markdown", IsBinary = false } },
            { "pdf", new FileContentType { ContentType = "application/pdf", IsBinary = true } },
            { "php", new FileContentType { ContentType = "application/x-httpd-php", IsBinary = false } },
            { "pkl", new FileContentType { ContentType = "application/octet-stream", IsBinary = true } },
            { "png", new FileContentType { ContentType = "image/png", IsBinary = true } },
            { "pptx", new FileContentType { ContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation", IsBinary = true } },
            { "py", new FileContentType { ContentType = "text/x-python", IsBinary = false } },
            { "rb", new FileContentType { ContentType = "text/x-ruby", IsBinary = false } },
            { "tar", new FileContentType { ContentType = "application/x-tar", IsBinary = true } },
            { "tex", new FileContentType { ContentType = "application/x-tex", IsBinary = false } },
            { "ts", new FileContentType { ContentType = "video/mp2t", IsBinary = true } },
            { "txt", new FileContentType { ContentType = "text/plain", IsBinary = false } },
            { "webp", new FileContentType { ContentType = "image/webp", IsBinary = true } },
            { "xlsx", new FileContentType { ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", IsBinary = true } },
            { "zip", new FileContentType { ContentType = "application/zip", IsBinary = true } }
        };

        public static FileContentType GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).TrimStart('.').ToLower();

            var contentType = _fileExtensionMappings.FirstOrDefault(pair => pair.Key == extension).Value;

            if (contentType == null)
            {
                throw new InvalidOperationException($"No content type mapping found for file extension: {extension}");
            }

            return contentType;
        }
    }

    public class FileDetails
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }

        public static FileDetails Get(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);

            if (fileInfo.Length > 15 * 1024 * 1024)
            {
                throw new InvalidOperationException("File size exceeds the limit of 15 MB.");
            }

            var contentType = FileTypeHelper.GetContentType(filePath);

            var fileDetails = new FileDetails
            {
                Name = fileInfo.Name,
                Path = fileInfo.FullName,
                Size = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime
            };

            if (contentType.IsBinary)
            {
                var fileBytes = File.ReadAllBytes(filePath);
                fileDetails.Content = $"data:{contentType.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
            }
            else
            {
                fileDetails.Content = File.ReadAllText(filePath);
            }

            return fileDetails;
        }

        public static void WriteFile(string path, string content)
        {
            var contentType = FileTypeHelper.GetContentType(path);

            // Ensure the directory exists
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (contentType.IsBinary)
            {
                string base64Content;
                if (content.StartsWith("data:") && content.Contains(";base64,"))
                {
                    base64Content = content.Substring(content.IndexOf(";base64,") + 8);
                }
                else
                {
                    base64Content = content;
                }

                byte[] fileBytes;
                try
                {
                    fileBytes = Convert.FromBase64String(base64Content);
                }
                catch (FormatException)
                {
                    throw new InvalidOperationException("The provided content is not a valid base64 string.");
                }

                File.WriteAllBytes(path, fileBytes);
            }
            else
            {
                File.WriteAllText(path, content);
            }
        }
    }
}