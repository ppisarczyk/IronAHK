﻿using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Text;
using IronAHK.Scripting;
using NUnit.Framework;

namespace IronAHK.Tests
{
    [TestFixture, Category("Scripting")]
    public partial class Scripting
    {
        string path = string.Format("..{0}..{0}Scripting{0}Code{0}", Path.DirectorySeparatorChar.ToString());
        const string ext = ".ahk";

        bool TestScript(string source)
        {
            return HasPassed(RunScript(string.Concat(path, source, ext)));
        }

        bool HasPassed(string output)
        {
            if (string.IsNullOrEmpty(output))
                return false;

            const string pass = "pass";
            foreach (string remove in new string[] { pass, " ", "\n" })
                output = output.Replace(remove, string.Empty);

            return output.Length == 0;
        }

        string RunScript(string source)
        {
            var provider = new IACodeProvider();
            var options = new CompilerParameters { GenerateExecutable = false, GenerateInMemory = true, };
            options.ReferencedAssemblies.Add(typeof(IronAHK.Rusty.Core).Namespace + ".dll");
            var results = provider.CompileAssemblyFromFile(options, source);

            var buffer = new StringBuilder();
            var writer = new StringWriter(buffer);
            Console.SetOut(writer);

            results.CompiledAssembly.EntryPoint.Invoke(null, null);

            writer.Flush();
            string output = buffer.ToString();
            var stdout = new StreamWriter(Console.OpenStandardOutput());
            stdout.AutoFlush = true;
            Console.SetOut(stdout);

            return output;
        }
    }
}
