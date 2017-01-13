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

using System.Threading.Tasks;
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
        private readonly IList<ConnectionMultiplexer> _connections;
        private int Quorum { get { return (_connections.Count / 2) + 1; } }
        
        public Redlock(params ConnectionMultiplexer[] connections)
        {
            _connections = connections.ToList().AsReadOnly();
        }

        public bool Lock(RedisKey resource, TimeSpan ttl, out Lock lockObject)
        {
            var task = LockAsync(resource, ttl);
            task.Wait();
            var result = task.Result;
            lockObject = result.Item2;
            return result.Item1;
        }

        public async Task<Tuple<bool, Lock>> LockAsync(RedisKey resource, TimeSpan ttl)
        {
            var val = CreateUniqueLockId();
            Lock lockObject = null;
            var successfull = await Retry(DefaultRetryCount, _defaultRetryDelay, async () =>
            {
                try
                {
                    var n = 0;
                    var startTime = DateTime.Now;

                    // Use keys
                    await for_each_redis_registered(
                        async connection =>
                        {
                            if (await LockInstance(connection, resource, val, ttl)) n += 1;
                        }
                    );

                    /*
                     * Add 2 milliseconds to the drift to account for Redis expires
                     * precision, which is 1 millisecond, plus 1 millisecond min drift 
                     * for small TTLs.        
                     */
                    var drift = Convert.ToInt32((ttl.TotalMilliseconds*ClockDriveFactor) + 2);
                    var validityTime = ttl - (DateTime.Now - startTime) - new TimeSpan(0, 0, 0, 0, drift);

                    if (n >= Quorum && validityTime.TotalMilliseconds > 0)
                    {
                        lockObject = new Lock(resource, val, validityTime);
                        return true;
                    }
                    await for_each_redis_registered(connection => UnlockInstance(connection, resource, val));
                    return false;
                }
                catch (Exception)
                {
                    return false;
                }
            });

            return Tuple.Create(successfull, lockObject);
        }

        public void Unlock(Lock lockObject)
        {
            UnlockAsync(lockObject).Wait();
        }

        public async Task UnlockAsync(Lock lockObject)
        {
            await for_each_redis_registered(async connection => await UnlockInstance(connection, lockObject.Resource, lockObject.Value));
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetType().FullName);

            sb.AppendLine("Registered Connections:");
            foreach (var item in _connections)
            {
                sb.AppendLine(item.GetEndPoints().First().ToString());
            }

            return sb.ToString();
        }

        //TODO: Refactor passing a ConnectionMultiplexer
        private static async Task<bool> LockInstance(ConnectionMultiplexer connection, string resource, byte[] val, TimeSpan ttl)
        {
            try
            {
                return await connection.GetDatabase().StringSetAsync(resource, val, ttl, When.NotExists);
            }
            catch (Exception)
            {
                return false;
            }
        }

        //TODO: Refactor passing a ConnectionMultiplexer
        private static async Task UnlockInstance(ConnectionMultiplexer connection, string resource, byte[] val)
        {
            RedisKey[] key = { resource };
            RedisValue[] values = { val };
            var redis = connection;
            await redis.GetDatabase().ScriptEvaluateAsync(
                UnlockScript,
                key,
                values
            );
        }

        private async Task for_each_redis_registered(Func<ConnectionMultiplexer, Task> action)
        {
            foreach (var connection in _connections)
            {
                await action(connection);
            }
        }

        private static async Task<bool> Retry(int retryCount, TimeSpan retryDelay, Func<Task<bool>> action)
        {
            var maxRetryDelay = (int)retryDelay.TotalMilliseconds;
            var rnd = new Random();
            var currentRetry = 0;

            while (currentRetry++ < retryCount)
            {
                if (await action()) return true;
                Thread.Sleep(rnd.Next(maxRetryDelay));
            }
            return false;
        }

        private static byte[] CreateUniqueLockId()
        {
            return Guid.NewGuid().ToByteArray();
        }

    }
}
