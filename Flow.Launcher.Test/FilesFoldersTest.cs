using Flow.Launcher.Plugin.SharedCommands;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.IO;

namespace Flow.Launcher.Test
{
    [TestFixture]
    
    public class FilesFoldersTest
    {
        // Testcases from https://stackoverflow.com/a/31941905/20703207
        // Disk
        [TestCase(@"c:", @"c:\foo", true)]
        [TestCase(@"c:\", @"c:\foo", true)]
        // Slash
        [TestCase(@"c:\foo\bar\", @"c:\foo\", false)]
        [TestCase(@"c:\foo\bar", @"c:\foo\", false)]
        [TestCase(@"c:\foo", @"c:\foo\bar", true)]
        [TestCase(@"c:\foo\", @"c:\foo\bar", true)]
        // File
        [TestCase(@"c:\foo", @"c:\foo\a.txt", true)]
        [TestCase(@"c:\foo", @"c:/foo/a.txt", true)]
        [TestCase(@"c:\FOO\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\foo\", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo\", false)]
        [TestCase(@"c:\foo\", @"c:\foo.txt", false)]
        // Prefix
        [TestCase(@"c:\foo", @"c:\foobar", false)]
        [TestCase(@"C:\Program", @"C:\Program Files\", false)]
        [TestCase(@"c:\foobar", @"c:\foo\a.txt", false)]
        [TestCase(@"c:\foobar\", @"c:\foo\a.txt", false)]
        // Edge case
        [TestCase(@"c:\foo", @"c:\foo\..\bar\baz", false)]
        [TestCase(@"c:\bar", @"c:\foo\..\bar\baz", true)]
        [TestCase(@"c:\barr", @"c:\foo\..\bar\baz", false)]
        public void GivenTwoPaths_WhenCheckPathContains_ThenShouldBeExpectedResult(string parentPath, string path, bool expectedResult)
        {
            ClassicAssert.AreEqual(expectedResult, FilesFolders.PathContains(parentPath, path));
        }

        // Equality
        [TestCase(@"c:\foo", @"c:\foo", false)]
        [TestCase(@"c:\foo\", @"c:\foo", false)]
        [TestCase(@"c:\foo", @"c:\foo\", false)]
        [TestCase(@"c:\foo", @"c:\foo", true)]
        [TestCase(@"c:\foo\", @"c:\foo", true)]
        [TestCase(@"c:\foo", @"c:\foo\", true)]
        public void GivenTwoPathsAreTheSame_WhenCheckPathContains_ThenShouldBeExpectedResult(string parentPath, string path, bool expectedResult)
        {
            ClassicAssert.AreEqual(expectedResult, FilesFolders.PathContains(parentPath, path, allowEqual: expectedResult));
        }

        [Test]
        public void GivenDirectoryWithReadOnlyFiles_WhenForceDeleteDirectory_ThenShouldDeleteCompletely()
        {
            // Arrange: Create a test directory structure with read-only files and subdirectories
            var testPath = Path.Combine(Path.GetTempPath(), "FlowLauncherTest_" + Path.GetRandomFileName());
            var subDirPath = Path.Combine(testPath, "SubFolder");
            
            Directory.CreateDirectory(testPath);
            Directory.CreateDirectory(subDirPath);

            // Create files with normal attributes
            var normalFile = Path.Combine(testPath, "normal.txt");
            File.WriteAllText(normalFile, "normal file");

            // Create read-only file
            var readOnlyFile = Path.Combine(testPath, "readonly.txt");
            File.WriteAllText(readOnlyFile, "readonly file");
            File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

            // Create hidden file
            var hiddenFile = Path.Combine(subDirPath, "hidden.txt");
            File.WriteAllText(hiddenFile, "hidden file");
            File.SetAttributes(hiddenFile, FileAttributes.Hidden);

            // Verify setup
            ClassicAssert.IsTrue(Directory.Exists(testPath));
            ClassicAssert.IsTrue(Directory.Exists(subDirPath));
            ClassicAssert.IsTrue(File.Exists(readOnlyFile));
            ClassicAssert.IsTrue(File.Exists(hiddenFile));

            // Act: Force delete the directory
            FilesFolders.ForceDeleteDirectory(testPath);

            // Assert: Verify directory and all contents are deleted
            ClassicAssert.IsFalse(Directory.Exists(testPath), "Directory should be completely deleted");
            ClassicAssert.IsFalse(File.Exists(readOnlyFile), "Read-only file should be deleted");
            ClassicAssert.IsFalse(File.Exists(hiddenFile), "Hidden file should be deleted");
        }

        [Test]
        public void GivenNonExistentDirectory_WhenForceDeleteDirectory_ThenShouldNotThrowException()
        {
            // Arrange: Use a path that doesn't exist
            var nonExistentPath = Path.Combine(Path.GetTempPath(), "NonExistent_" + Path.GetRandomFileName());

            // Act & Assert: Should not throw exception
            Assert.DoesNotThrow(() => FilesFolders.ForceDeleteDirectory(nonExistentPath));
        }
    }
}
