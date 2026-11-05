using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Internal;

public static class RunCoreTests
{
    public static int Main(string[] args)
    {
        var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
        runner.Load(Assembly.GetExecutingAssembly(), new Dictionary<string, object> { ["NumberOfTestWorkers"] = 0 });
        var result = runner.Run(TestListener.NULL, TestFilter.Empty);
        File.WriteAllText(args[0], result.ToXml(true).OuterXml);
        Console.WriteLine($"Core NUnit (.NET): passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
        return result.FailCount == 0 && result.PassCount > 0 && result.SkipCount == 0 ? 0 : 1;
    }
}
