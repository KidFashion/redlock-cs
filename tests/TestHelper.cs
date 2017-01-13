using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Redlock.CSharp.Tests
{
    public static class TestHelper
    {
        public static Process StartRedisServer(long port)
        {
            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var fileName = Path.Combine(assemblyDir, "redis-server.exe");

            // Launch Server
            var process = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = "--port " + port,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            try
            {
                process.Start();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                Console.WriteLine($"Attempt to launch {fileName} failed.");
                Console.WriteLine("Directory listing:");
                foreach (var file in Directory.GetFiles(assemblyDir))
                {
                    Console.WriteLine($"\t{file}");
                }
                throw;
            }

            return process;
        }
    }
}