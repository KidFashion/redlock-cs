using System;
using System.Collections.Generic;
using System.Linq;
using MbUnit.Framework;
using StackExchange.Redis;
using System.Diagnostics;

namespace Redlock.CSharp.Tests
{
    [TestFixture]
    public class MultiServerLockTests
    {
        private const string ResourceName = "MyResourceName";
        private readonly List<Process> _redisProcessList = new List<Process>();
       
        [FixtureSetUp]
        public void Setup()
        {
            var fileName = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"..\..\..\packages\Redis-32.2.6.12.1\tools\redis-server.exe");

            // Launch Server
            var redis = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = "--port 6379",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            // Configure the process using the StartInfo properties.
            redis.Start();
            _redisProcessList.Add(redis);

            redis = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = "--port 6380",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            // Configure the process using the StartInfo properties.
            redis.Start();
            _redisProcessList.Add(redis);

            redis = new Process
            {
                StartInfo =
                {
                    FileName = fileName,
                    Arguments = "--port 6381",
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            };

            // Configure the process using the StartInfo properties.
            redis.Start();

            _redisProcessList.Add(redis);
        }

        [FixtureTearDown]
        public void Teardown()
        {
            foreach (var process in _redisProcessList.Where(process => !process.HasExited))
            {
                process.Kill();
            }

            _redisProcessList.Clear();
        }

        [Test]
        public void TestWhenLockedAnotherLockRequestIsRejected()
        {
            var dlm = new Redlock(ConnectionMultiplexer.Connect("127.0.0.1:6379"), ConnectionMultiplexer.Connect("127.0.0.1:6380"), ConnectionMultiplexer.Connect("127.0.0.1:6381"));

            Lock lockObject;
            Lock newLockObject;

            var locked = dlm.Lock(ResourceName, new TimeSpan(0, 0, 10), out lockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            locked = dlm.Lock(ResourceName, new TimeSpan(0, 0, 10), out newLockObject);
            Assert.IsFalse(locked, "lock taken, it shouldn't be possible");
            dlm.Unlock(lockObject);
        }

        [Test]
        public void TestThatSequenceLockedUnlockedAndLockedAgainIsSuccessfull()
        {
            var dlm = new Redlock(ConnectionMultiplexer.Connect("127.0.0.1:6379"), ConnectionMultiplexer.Connect("127.0.0.1:6380"), ConnectionMultiplexer.Connect("127.0.0.1:6381"));
            Lock lockObject = null;
            Lock newLockObject;

            var locked = dlm.Lock(ResourceName, new TimeSpan(0, 0, 10), out lockObject);
            Assert.IsTrue(locked, "Unable to get lock");
            dlm.Unlock(lockObject);
            locked = dlm.Lock(ResourceName, new TimeSpan(0, 0, 10), out newLockObject);
            Assert.IsTrue(locked, "Unable to get lock");

            dlm.Unlock(newLockObject);


        }
    }
}
