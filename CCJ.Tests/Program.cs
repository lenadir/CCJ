using System;
using System.Reflection;
using NUnitLite;

namespace CCJ.Tests
{
    /// <summary>
    /// There is no vstest.console.exe on the target machine, so the tests are a self running
    /// console application. "CCJ.Tests.exe" runs everything; pass --help for NUnitLite options.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            return new AutoRun(Assembly.GetExecutingAssembly()).Execute(args);
        }
    }
}
