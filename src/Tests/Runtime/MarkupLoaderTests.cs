using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Configuration;
using DotVVM.Framework.Controls;
using DotVVM.Framework.Hosting;
using DotVVM.Framework.Runtime;
using DotVVM.Framework.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DotVVM.Framework.Tests.Runtime
{
    [TestClass]
    public class MarkupLoaderTests: IDisposable
    {
        readonly string compilationPageResource = DotvvmTestHelper.DebugConfig.RouteTable[DotvvmCompilationPageConfiguration.DefaultRouteName].VirtualPath;

        readonly List<string> tempFiles = [];

        [TestMethod]
        public void EmbeddedResource()
        {
            var loader = new EmbeddedMarkupFileLoader();
            var file = loader.GetMarkup(DotvvmTestHelper.DebugConfig, compilationPageResource);

            XAssert.StartsWith("@viewModel DotVVM.Framework.Diagnostics.CompilationPageViewModel", file.ReadContent());
        }

#if DotNetCore
        [TestMethod]
        public void EmbeddedResourceLazyAllocation()
        {
            var loader = new EmbeddedMarkupFileLoader();
            // warmup for assembly loading and such
            loader.GetMarkup(DotvvmTestHelper.DebugConfig, compilationPageResource);

            // GetMarkup allocates constant memory, as it is being called repeatedly if file reloading is enabled
            var a = GC.GetAllocatedBytesForCurrentThread();
            var file = loader.GetMarkup(DotvvmTestHelper.DebugConfig, compilationPageResource);
            var b = GC.GetAllocatedBytesForCurrentThread();
            XAssert.InRange(b - a, 0, 1000);

            // ReadContent actually reads the file and allocates the string
            a = GC.GetAllocatedBytesForCurrentThread();
            var content = file.ReadContent();
            b = GC.GetAllocatedBytesForCurrentThread();
            XAssert.InRange(content.Length, 1000, int.MaxValue);
            XAssert.InRange(b - a, content.Length * 2, content.Length * 5);
        }
#endif

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void FileReloading(bool debug)
        {
            var directory = MakeTempDir();
            var file = Path.Combine(directory, "test.dotcontrol");
            File.WriteAllText(file, "@viewModel string\n\n<dot:TextBox Text=Initial />");
            var changedTime1 = File.GetLastWriteTimeUtc(file);

            var config = debug ? DotvvmTestHelper.DebugConfig : DotvvmTestHelper.DefaultConfig;

            var controlBuilder = config.ServiceProvider.GetRequiredService<IControlBuilderFactory>();
            var builder0 = controlBuilder.GetControlBuilder(file);
            Assert.AreEqual(typeof(string), builder0.descriptor.DataContextType);

            var builderUnchanged = controlBuilder.GetControlBuilder(file);

            Assert.AreSame(builder0.builder, builderUnchanged.builder); // same Lazy instance

            File.WriteAllText(file, "@viewModel int\n\n<dot:TextBox Text=Changed />");

            var builderChanged = controlBuilder.GetControlBuilder(file);
            var control = builderChanged.builder.Value.BuildControl(config.ServiceProvider.GetRequiredService<IControlBuilderFactory>(), config.ServiceProvider);
            if (debug)
            {
                var changedTime2 = File.GetLastWriteTimeUtc(file);
                if (changedTime1 == changedTime2)
                    Assert.Inconclusive($"File system resolution is probably too low ({changedTime1:o} == {changedTime2:o}), ignores changes or something.");

                Assert.AreEqual(typeof(int), builderChanged.descriptor.DataContextType);
                Assert.AreNotSame(builder0.builder, builderChanged.builder); // different Lazy instance
                XAssert.Equal(["Changed"], control.GetThisAndAllDescendants().OfType<TextBox>().Select(c => c.Text));
            }
            else
            {
                // not reloaded in Release mode by default
                Assert.AreEqual(typeof(string), builderChanged.descriptor.DataContextType);
                Assert.AreSame(builder0.builder, builderChanged.builder); // different Lazy instance
                XAssert.Equal(["Initial"], control.GetThisAndAllDescendants().OfType<TextBox>().Select(c => c.Text));
            }
        }

        public string MakeTempDir()
        {
            var path = Path.Combine(Path.GetTempPath(), "dotvvm-tests-tmp-" + Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            tempFiles.Add(path);
            return path;
        }

        public void Dispose()
        {
            foreach (var file in tempFiles)
            {
                if (Directory.Exists(file))
                    Directory.Delete(file, true);
                else if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }
}
