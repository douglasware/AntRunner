using System;
using System.Collections.Generic;
using System.IO;

namespace DriveManagerLib
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
    }

    public class DriveDetails
    {
        public string Name { get; set; } = string.Empty;
        public string DriveType { get; set; } = string.Empty;
        public long TotalSize { get; set; }
        public long AvailableFreeSpace { get; set; }
        public bool IsReady { get; set; }
    }
}