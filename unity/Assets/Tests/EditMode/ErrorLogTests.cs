using System.IO;
using NUnit.Framework;

namespace Oohra.Client.Tests
{
    /// <summary>Záznam chyb u testera: chyba se zapíše s časem, velký soubor se odloží.</summary>
    public class ErrorLogTests
    {
        private string _directory;

        [SetUp]
        public void CreateDirectory()
        {
            _directory = Path.Combine(Path.GetTempPath(), "oohra-chyby-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void DeleteDirectory()
        {
            Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void WritesErrorWithTimeAndStack()
        {
            var path = Path.Combine(_directory, "chyby.txt");

            ErrorLog.Write(path, "Exception: něco se pokazilo", "at Oohra.Client.Neco()");

            var text = File.ReadAllText(path);
            StringAssert.Contains("něco se pokazilo", text);
            StringAssert.Contains("Oohra.Client.Neco", text);
            StringAssert.Contains("protokol", text);
        }

        [Test]
        public void BigFileIsMovedAside()
        {
            var path = Path.Combine(_directory, "chyby.txt");
            File.WriteAllText(path, new string('x', 1100 * 1024));

            ErrorLog.Write(path, "Error: nová chyba", string.Empty);

            Assert.IsTrue(File.Exists(Path.Combine(_directory, "chyby-starsi.txt")));
            Assert.Less(new FileInfo(path).Length, 10000);
        }
    }
}
