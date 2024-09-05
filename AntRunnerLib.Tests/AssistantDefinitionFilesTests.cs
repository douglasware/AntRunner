using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AntRunnerLib.AssistantDefinitions.Tests
{
    [TestClass]
    public class AssistantDefinitionFilesTests
    {
        // Test data constants
        private const string AssistantName = "Blob Pirate";
        private const string TestDataPath = @".\TestData\Assistants\Blob Pirate\";

        [TestMethod]
        public async Task GetManifest_ReturnsExpectedManifest()
        {
            var manifest = await AssistantDefinitionFiles.GetManifest(AssistantName);
            var expectedManifest = File.ReadAllText(Path.Combine(TestDataPath, "manifest.json"));

            Assert.AreEqual(expectedManifest, manifest);
        }

        [TestMethod]
        public async Task GetActionAuth_ReturnsExpectedActionAuth()
        {
            var auth = await AssistantDefinitionFiles.GetActionAuth(AssistantName);
            var expectedAuth = File.ReadAllText(Path.Combine(TestDataPath, "OpenAPI\\auth.json"));

            Assert.AreEqual(expectedAuth, auth);
        }

        [TestMethod]
        public async Task GetInstructions_ReturnsExpectedInstructions()
        {
            var instructions = await AssistantDefinitionFiles.GetInstructions(AssistantName);
            var expectedInstructions = File.ReadAllText(Path.Combine(TestDataPath, "instructions.md"));

            Assert.AreEqual(expectedInstructions, instructions);
        }

        [TestMethod]
        public async Task GetFilesInOpenAPIFolder_ReturnsExpectedFiles()
        {
            var files = (await AssistantDefinitionFiles.GetFilesInOpenAPIFolder(AssistantName))?.Select(Path.GetFileName).OrderBy(f => f).ToList();
            var expectedFiles = Directory.GetFiles(Path.Combine(TestDataPath, "OpenAPI")).Select(Path.GetFileName).Where(f => !f.Equals("auth.json", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f).ToList();

            CollectionAssert.AreEqual(expectedFiles, files);
        }

        [TestMethod]
        public async Task GetFilesInCodeInterpreterFolder_ReturnsExpectedFiles()
        {
            var files = (await AssistantDefinitionFiles.GetFilesInCodeInterpreterFolder(AssistantName))?.Select(Path.GetFileName).OrderBy(f => f).ToList();
            var expectedFiles = Directory.GetFiles(Path.Combine(TestDataPath, "CodeInterpreter")).Select(Path.GetFileName).OrderBy(f => f).ToList();

            CollectionAssert.AreEqual(expectedFiles, files);
        }

        [TestMethod]
        public async Task GetFilesInVectorStoreFolder_ReturnsExpectedFiles()
        {
            var vectorStoreName = "McKessonTranscripts";
            var files = (await AssistantDefinitionFiles.GetFilesInVectorStoreFolder(AssistantName, vectorStoreName))?.Select(Path.GetFileName).OrderBy(f => f).ToList();
            var expectedFiles = Directory.GetFiles(Path.Combine(TestDataPath, "VectorStores", vectorStoreName)).Select(Path.GetFileName).OrderBy(f => f).ToList();

            CollectionAssert.AreEqual(expectedFiles, files);
        }

        [TestMethod]
        public async Task GetFile_ReturnsExpectedFileData()
        {
            var filePath = @"VectorStores\McKessonTranscripts\211101-MCK-Q2FY22-Earnings-Call-Transcript.md";
            var storagePath = @"Blob Pirate\VectorStores\McKessonTranscripts\211101-MCK-Q2FY22-Earnings-Call-Transcript.md";
            var fileData = await AssistantDefinitionFiles.GetFile(storagePath);
            var expectedFileData = File.ReadAllBytes(Path.Combine(TestDataPath, filePath));

            CollectionAssert.AreEqual(expectedFileData, fileData);
        }

        [TestMethod]
        public async Task GetFileFromVectorStoreFolder_ReturnsExpectedFileData()
        {
            var vectorStoreName = "McKessonTranscripts";
            var files = (await AssistantDefinitionFiles.GetFilesInVectorStoreFolder(AssistantName, vectorStoreName))?.OrderBy(f => f).ToList();
            Assert.IsNotNull(files, "Vector store folder files should not be null");
            Assert.IsTrue(files.Any(), "Vector store folder should contain files");

            var filePath = files.First();
            var referenceFilePath = filePath.Replace($"{AssistantName}/", "");
            var fileData = await AssistantDefinitionFiles.GetFile(filePath);
            var expectedFileData = File.ReadAllBytes(Path.Combine(TestDataPath, referenceFilePath));

            CollectionAssert.AreEqual(expectedFileData, fileData);
        }

        [TestMethod]
        public async Task GetFileFromCodeInterpreterFolder_ReturnsExpectedFileData()
        {
            var files = (await AssistantDefinitionFiles.GetFilesInCodeInterpreterFolder(AssistantName))?.OrderBy(f => f).ToList();
            Assert.IsNotNull(files, "Code Interpreter folder files should not be null");
            Assert.IsTrue(files.Any(), "Code Interpreter folder should contain files");

            var filePath = files.First();
            var referenceFilePath = filePath.Replace($"{AssistantName}/", "");
            var fileData = await AssistantDefinitionFiles.GetFile(filePath);
            var expectedFileData = File.ReadAllBytes(Path.Combine(TestDataPath, referenceFilePath));

            CollectionAssert.AreEqual(expectedFileData, fileData);
        }
    }
}