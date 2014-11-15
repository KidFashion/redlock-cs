using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MbUnit.Framework;

using Redlock.CSharp;
using StackExchange.Redis;

namespace Redlock.CSharp.Tests
{
    [TestFixture]
    public class SingleServerLockTests
    {
        private const string resourceName = "MyResourceName";

        [Test]
        public void TestWhenLockedAnotherLockRequestIsRejected()
        {
            var dlm = new Redlock(ConnectionMultiplexer.Connect("127.0.0.1:6379"));

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
            var dlm = new Redlock(ConnectionMultiplexer.Connect("127.0.0.1:6379"));

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
