// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Xunit;
using System.Text;

namespace System.IO.FileSystem.DriveInfoTests
{
    public class DriveInfoWindowsTests
    {
        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void TestConstructor()
        {
            string[] invalidInput = { ":\0", ":", "://", @":\", ":/", @":\\", "Az", "1", "a1", @"\\share", @"\\", "c ", string.Empty, " c" };
            string[] variableInput = { "{0}", "{0}", "{0}:", "{0}:", @"{0}:\", @"{0}:\\", "{0}://" };

            // Test Invalid input
            foreach (var input in invalidInput)
            {
                Assert.Throws<ArgumentException>(() => { new DriveInfo(input); });
            }

            // Test Null
            Assert.Throws<ArgumentNullException>(() => { new DriveInfo(null); });

            // Test Valid DriveLetter
            var validDriveLetter = GetValidDriveLettersOnMachine().First();
            foreach (var input in variableInput)
            {
                string name = string.Format(input, validDriveLetter);
                DriveInfo dInfo = new DriveInfo(name);
                Assert.Equal(string.Format(@"{0}:\", validDriveLetter), dInfo.Name);
            }
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void TestGetDrives()
        {
            var validExpectedDrives = GetValidDriveLettersOnMachine();
            var validActualDrives = DriveInfo.GetDrives();

            // Test count
            Assert.Equal(validExpectedDrives.Count(), validActualDrives.Count());

            for (int i = 0; i < validActualDrives.Count(); i++)
            {
                // Test if the driveletter is correct
                Assert.Contains(validActualDrives[i].Name[0], validExpectedDrives);
            }
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void TestDriveFormat()
        {
            var validDrive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).First();
            const int volNameLen = 50;
            StringBuilder volumeName = new StringBuilder(volNameLen);
            const int fileSystemNameLen = 50;
            StringBuilder fileSystemName = new StringBuilder(fileSystemNameLen);
            int serialNumber, maxFileNameLen, fileSystemFlags;
            bool r = GetVolumeInformation(validDrive.Name, volumeName, volNameLen, out serialNumber, out maxFileNameLen, out fileSystemFlags, fileSystemName, fileSystemNameLen);
            var fileSystem = fileSystemName.ToString();
            if (r)
            {
                Assert.Equal(fileSystem, validDrive.DriveFormat);
            }
            else
            {
                Assert.Throws<IOException>(() => validDrive.DriveFormat);
            }

            // Test Invalid drive
            var invalidDrive = new DriveInfo(GetInvalidDriveLettersOnMachine().First().ToString());
            Assert.Throws<DriveNotFoundException>(() => invalidDrive.DriveFormat);
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void TestDriveType()
        {
            var validDrive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).First();
            var expectedDriveType = GetDriveType(validDrive.Name);
            Assert.Equal((DriveType)expectedDriveType, validDrive.DriveType);

            // Test Invalid drive
            var invalidDrive = new DriveInfo(GetInvalidDriveLettersOnMachine().First().ToString());
            Assert.Equal(invalidDrive.DriveType, DriveType.NoRootDirectory);
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void TestValidDiskSpaceProperties()
        {
            bool win32Result;
            long fbUser = -1;
            long tbUser;
            long fbTotal;
            DriveInfo drive;

            drive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).First();
            if (drive.IsReady)
            {
                win32Result = GetDiskFreeSpaceEx(drive.Name, out fbUser, out tbUser, out fbTotal);
                Assert.True(win32Result);

                if (fbUser != drive.AvailableFreeSpace)
                    Assert.True(drive.AvailableFreeSpace >= 0);

                // valid property getters shouldn't throw
                string name = drive.Name;
                string format = drive.DriveFormat;
                Assert.Equal(name, drive.ToString());

                // totalsize should not change for a fixed drive.
                Assert.Equal(tbUser, drive.TotalSize);

                if (fbTotal != drive.TotalFreeSpace)
                    Assert.True(drive.TotalFreeSpace >= 0);
            }
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void TestInvalidDiskProperties()
        {
            string invalidDriveName = GetInvalidDriveLettersOnMachine().First().ToString();
            var invalidDrive = new DriveInfo(invalidDriveName);

            Assert.Throws<DriveNotFoundException>(() => invalidDrive.AvailableFreeSpace);
            Assert.Throws<DriveNotFoundException>(() => invalidDrive.DriveFormat);
            Assert.Equal(DriveType.NoRootDirectory, invalidDrive.DriveType);
            Assert.False(invalidDrive.IsReady);
            Assert.Equal(invalidDriveName + ":\\", invalidDrive.Name);
            Assert.Equal(invalidDriveName + ":\\", invalidDrive.ToString());
            Assert.Equal(invalidDriveName + ":\\", invalidDrive.RootDirectory.FullName);
            Assert.Throws<DriveNotFoundException>(() => invalidDrive.TotalFreeSpace);
            Assert.Throws<DriveNotFoundException>(() => invalidDrive.TotalSize);
            Assert.Throws<DriveNotFoundException>(() => invalidDrive.VolumeLabel);
            Assert.Throws<DriveNotFoundException>(() => invalidDrive.VolumeLabel = null);
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void GetVolumeLabel_Returns_CorrectLabel()
        {
            int serialNumber, maxFileNameLen, fileSystemFlags;
            int volNameLen = 50;
            int fileNameLen = 50;
            StringBuilder volumeName = new StringBuilder(volNameLen);
            StringBuilder fileSystemName = new StringBuilder(fileNameLen);

            // Get Volume Label - valid drive
            var validDrive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).First();
            bool r = GetVolumeInformation(validDrive.Name, volumeName, volNameLen, out serialNumber, out maxFileNameLen, out fileSystemFlags, fileSystemName, fileNameLen);
            if (r)
            {
                Assert.Equal(volumeName.ToString(), validDrive.VolumeLabel);
            }
            else // if we can't compare the volumeName, we should at least check that getting it doesn't throw
            {
                var name = validDrive.VolumeLabel;
            }
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void SetVolumeLabel_Roundtrips()
        {
            DriveInfo drive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).First();
            string currentLabel = drive.VolumeLabel;
            try
            {
                drive.VolumeLabel = currentLabel; // shouldn't change the state of the drive regardless of success
            }
            catch (UnauthorizedAccessException) { }
            Assert.Equal(drive.VolumeLabel, currentLabel);
        }

        [Fact]
        [PlatformSpecific(PlatformID.Windows)]
        public void VolumeLabelOnNetworkOrCdRom_Throws()
        {
            // Test setting the volume label on a Network or CD-ROM
            var noAccessDrive = DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Network || d.DriveType == DriveType.CDRom);
            foreach (var adrive in noAccessDrive)
            {
                if (adrive.IsReady)
                {
                    Exception e = Assert.ThrowsAny<Exception>(() => { adrive.VolumeLabel = null; });
                    Assert.True(
                        e is UnauthorizedAccessException || 
                        e is IOException ||
                        e is SecurityException);
                }
            }
        }

        [DllImport("api-ms-win-core-file-l1-1-0.dll", SetLastError = true)]
        internal static extern int GetLogicalDrives();

        [DllImport("api-ms-win-core-file-l1-1-0.dll", EntryPoint = "GetVolumeInformationW", CharSet = CharSet.Unicode, SetLastError = true, BestFitMapping = false)]
        internal static extern bool GetVolumeInformation(string drive, StringBuilder volumeName, int volumeNameBufLen, out int volSerialNumber, out int maxFileNameLen, out int fileSystemFlags, StringBuilder fileSystemName, int fileSystemNameBufLen);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", SetLastError = true)]
        internal static extern int GetDriveType(string drive);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", SetLastError = true)]
        internal static extern bool GetDiskFreeSpaceEx(String drive, out long freeBytesForUser, out long totalBytes, out long freeBytes);

        private IEnumerable<char> GetValidDriveLettersOnMachine()
        {
            uint mask = (uint)GetLogicalDrives();
            Assert.NotEqual<uint>(mask, 0);

            var bits = new BitArray(new int[] { (int)mask });
            for (int i = 0; i < bits.Length; i++)
            {
                var letter = (char)('A' + i);
                if (bits[i])
                    yield return letter;
            }
        }

        private IEnumerable<char> GetInvalidDriveLettersOnMachine()
        {
            uint mask = (uint)GetLogicalDrives();
            Assert.NotEqual<uint>(mask, 0);

            var bits = new BitArray(new int[] { (int)mask });
            for (int i = 0; i < bits.Length; i++)
            {
                var letter = (char)('A' + i);
                if (!bits[i])
                {
                    if (char.IsLetter(letter))
                        yield return letter;
                }
            }
        }
    }
}
