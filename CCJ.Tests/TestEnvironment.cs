using System;
using System.IO;
using CCJ.Models;
using NUnit.Framework;

namespace CCJ.Tests
{
    /// <summary>
    /// Runs once around the whole test run. Points CCJ's settings and error log at a throwaway
    /// folder, so no test can read or change the real %APPDATA%\CCJ\jobs.json or app.log.
    /// </summary>
    [SetUpFixture]
    public class TestEnvironment
    {
        private string _dataDir;
        private string _previous;

        [OneTimeSetUp]
        public void Before()
        {
            _previous = Environment.GetEnvironmentVariable(AppPaths.DataDirVariable);
            _dataDir = Path.Combine(Path.GetTempPath(), "CCJ_TestData_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataDir);
            Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, _dataDir);
        }

        [OneTimeTearDown]
        public void After()
        {
            Environment.SetEnvironmentVariable(AppPaths.DataDirVariable, _previous);
            ProgressTestSupport.DeleteQuietly(_dataDir);
        }
    }
}
