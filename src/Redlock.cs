#region LICENSE
/*
 *   Copyright 2014 Angelo Simone Scotto <scotto.a@gmail.com>
 * 
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 * 
 *       http://www.apache.org/licenses/LICENSE-2.0
 * 
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 * 
 * */
#endregion

using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Redlock.CSharp
{
    public class Redlock
    {

        /// <summary>
        /// String containing the Lua unlock script.
        /// </summary>
        const String UnlockScript = @"
            if redis.call(""get"",KEYS[1]) == ARGV[1] then
                return redis.call(""del"",KEYS[1])
            else
                return 0
            end";

        private const int DefaultRetryCount = 3;
        private const double ClockDriveFactor = 0.01;
        private readonly TimeSpan _defaultRetryDelay = TimeSpan.FromMilliseconds(200);
        private readonly Dictionary<String, ConnectionMultiplexer> _redisMasterDictionary;
        private int Quorum { get { return (_redisMasterDictionary.Count / 2) + 1; } }
        
        public Redlock(params ConnectionMultiplexer[] list)
        {
            _redisMasterDictionary = list.ToDictionary(c => c.GetEndPoints().First().ToString(), c => c);
        }


        private static byte[] CreateUniqueLockId()
        {
            return Guid.NewGuid().ToByteArray();
        }
        
        public bool Lock(RedisKey resource, TimeSpan ttl, out Lock lockObject)
        {
            var val = CreateUniqueLockId();
            Lock innerLock = null;
            var successfull = Retry(DefaultRetryCount, _defaultRetryDelay, () =>
            {
                try
                {
                    var n = 0;
                    var startTime = DateTime.Now;

                    // Use keys
                    for_each_redis_registered(
                        redis =>
                        {
                            if (LockInstance(redis, resource, val, ttl)) n += 1;
                        }
                    );

                    /*
                     * Add 2 milliseconds to the drift to account for Redis expires
                     * precision, which is 1 milliescond, plus 1 millisecond min drift 
                     * for small TTLs.        
                     */
                    var drift = Convert.ToInt32((ttl.TotalMilliseconds * ClockDriveFactor) + 2);
                    var validityTime = ttl - (DateTime.Now - startTime) - new TimeSpan(0, 0, 0, 0, drift);

                    if (n >= Quorum && validityTime.TotalMilliseconds > 0)
                    {
                        innerLock = new Lock(resource, val, validityTime);
                        return true;
                    }
                    for_each_redis_registered(redis => UnlockInstance(redis, resource, val));
                    return false;
                }
                catch (Exception)
                { return false; }
            });

            lockObject = innerLock;
            return successfull;
        }

        public void Unlock(Lock lockObject)
        {
            for_each_redis_registered(redis => UnlockInstance(redis, lockObject.Resource, lockObject.Value));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetType().FullName);

            sb.AppendLine("Registered Connections:");
            foreach (var item in _redisMasterDictionary)
            {
                sb.AppendLine(item.Value.GetEndPoints().First().ToString());
            }

            return sb.ToString();
        }

        //TODO: Refactor passing a ConnectionMultiplexer
        private bool LockInstance(string redisServer, string resource, byte[] val, TimeSpan ttl)
        {

            bool succeeded;
            try
            {
                var redis = _redisMasterDictionary[redisServer];
                succeeded = redis.GetDatabase().StringSet(resource, val, ttl, When.NotExists);
            }
            catch (Exception)
            {
                succeeded = false;
            }
            return succeeded;
        }

        //TODO: Refactor passing a ConnectionMultiplexer
        private void UnlockInstance(string redisServer, string resource, byte[] val)
        {
            RedisKey[] key = { resource };
            RedisValue[] values = { val };
            var redis = _redisMasterDictionary[redisServer];
            redis.GetDatabase().ScriptEvaluate(
                UnlockScript,
                key,
                values
            );
        }

        private void for_each_redis_registered(Action<String> action)
        {
            foreach (var item in _redisMasterDictionary)
            {
                action(item.Key);
            }
        }

        private static bool Retry(int retryCount, TimeSpan retryDelay, Func<bool> action)
        {
            var maxRetryDelay = (int)retryDelay.TotalMilliseconds;
            var rnd = new Random();
            var currentRetry = 0;

            while (currentRetry++ < retryCount)
            {
                if (action()) return true;
                Thread.Sleep(rnd.Next(maxRetryDelay));
            }
            return false;
        }

    }
}
