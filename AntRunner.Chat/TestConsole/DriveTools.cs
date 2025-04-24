namespace TestConsole
{
    public static class DriveTools
    {
        public static List<DriveDetails> ListDrives()
        {
            var drives = DriveInfo.GetDrives();
            var driveDetailsList = new List<DriveDetails>();

            foreach (var drive in drives)
            {
                if (drive.IsReady)
                {
                    var driveDetails = new DriveDetails
                    {
                        Name = drive.Name ?? string.Empty,
                        DriveType = drive.DriveType.ToString(),
                        TotalSize = drive.TotalSize,
                        AvailableFreeSpace = drive.AvailableFreeSpace,
                        IsReady = drive.IsReady
                    };

                    driveDetailsList.Add(driveDetails);
                }
            }

            return driveDetailsList;
        }

        public static List<ItemDetails> ListItems(string path, bool recurse, string searchPattern = "*")
        {
            var items = new List<ItemDetails>();
            var searchOption = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            foreach (var dir in Directory.EnumerateDirectories(path, searchPattern, searchOption))
            {
                try
                {
                    var dirInfo = new DirectoryInfo(dir);
                    var item = new ItemDetails
                    {
                        Name = dirInfo.Name,
                        Path = dirInfo.FullName,
                        IsDirectory = true,
                        Size = null
                    };
                    items.Add(item);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (PathTooLongException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }
            }

            foreach (var file in Directory.EnumerateFiles(path, searchPattern, searchOption))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    var item = new ItemDetails
                    {
                        Name = fileInfo.Name,
                        Path = fileInfo.FullName,
                        IsDirectory = false,
                        Size = fileInfo.Length
                    };
                    items.Add(item);
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
                catch (PathTooLongException)
                {
                    continue;
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
            }

            return items;
        }
    }

    public class DriveDetails
    {
        public string Name { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long AvailableFreeSpace { get; set; }
        public bool IsReady { get; set; }
    }

    public class ItemDetails
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long? Size { get; set; } // Nullable for directories
    }
}