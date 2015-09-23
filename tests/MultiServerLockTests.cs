using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using StackExchange.Redis;
using System.Diagnostics;

namespace Redlock.CSharp.Tests
{
    [TestFixture]
    public class MultiServerLockTests
    {
        private const string ResourceName = "MyResourceName";
        private readonly List<Process> _redisProcessList = new List<Process>();
       
        [SetUp]
        public void Setup()
        {
            _redisProcessList.Add(TestHelper.StartRedisServer(6379));
            _redisProcessList.Add(TestHelper.StartRedisServer(6380));
            _redisProcessList.Add(TestHelper.StartRedisServer(6381));
        }

        [TearDown]
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
