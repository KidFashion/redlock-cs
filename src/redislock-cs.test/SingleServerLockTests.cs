﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NUnit.Framework;

using Redlock.CSharp;
using StackExchange.Redis;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Redlock.CSharp.Tests
{
    [TestFixture]
    public class SingleServerLockTests
    {
        private const string resourceName = "MyResourceName";

        private const string ServerA_Key = "ConnectionString_ServerA";

#if TOBEREMOVED
        //
        private List<Process> redisProcessList = new List<Process>();
        [OneTimeSetUp]
        public void setup()
        {
            // Launch Server
            Process redis = new Process();

            // Configure the process using the StartInfo properties.
            redis.StartInfo.FileName = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)+ @"..\..\..\packages\Redis-32.2.6.12.1\tools\redis-server.exe");
            redis.StartInfo.Arguments = "--port 6379";
            redis.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            redis.Start();

            redisProcessList.Add(redis);
        }

        [OneTimeTearDown]
        public void teardown()
        {
            foreach (var process in redisProcessList)
            {
                if (!process.HasExited) process.Kill();
            }

            redisProcessList.Clear();
        }
#endif
        [Test]
        public void TestWhenLockedAnotherLockRequestIsRejected()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder
                .AddJsonFile("app.json", true);
            var configRoot = configBuilder.Build();

            var dlm = new Redlock(ConnectionMultiplexer.Connect(configRoot[ServerA_Key]));

            Lock lockObject;
            Lock newLockObject;
            var locked = dlm.Lock(resourceName, new TimeSpan(0, 0, 10), out lockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            locked = dlm.Lock(resourceName, new TimeSpan(0, 0, 10), out newLockObject);
            Assert.IsFalse(locked, "lock taken, it shouldn't be possible");
            dlm.Unlock(lockObject);
        }

        [Test]
        public void TestThatSequenceLockedUnlockedAndLockedAgainIsSuccessfull()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder
                .AddJsonFile("app.json", true);
            var configRoot = configBuilder.Build();

            var dlm = new Redlock(ConnectionMultiplexer.Connect(configRoot[ServerA_Key]));

            Lock lockObject;
            Lock newLockObject;
            var locked = dlm.Lock(resourceName, new TimeSpan(0, 0, 10), out lockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            dlm.Unlock(lockObject);
            locked = dlm.Lock(resourceName, new TimeSpan(0, 0, 10), out newLockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            dlm.Unlock(newLockObject);
        }

    }
}
