﻿#region LICENSE
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
using System.Threading.Tasks;

namespace Redlock.CSharp
{
    public class Redlock
    {

        public Redlock(params IConnectionMultiplexer[] list)
        { 
            foreach(var item in list)
                this.redisMasterDictionary.Add(item.GetEndPoints().First().ToString(),item);
        }

        const int DefaultRetryCount = 3;
        readonly TimeSpan DefaultRetryDelay = new TimeSpan(0, 0, 0, 0, 200);
        const double ClockDriveFactor = 0.01;

        protected int Quorum { get { return (redisMasterDictionary.Count / 2) + 1; } }

        /// <summary>
        /// String containing the Lua unlock script.
        /// </summary>
        const String UnlockScript = @"
            if redis.call(""get"",KEYS[1]) == ARGV[1] then
                return redis.call(""del"",KEYS[1])
            else
                return 0
            end";


        protected static byte[] CreateUniqueLockId()
        {
            return Guid.NewGuid().ToByteArray();
        }


        protected Dictionary<String,IConnectionMultiplexer> redisMasterDictionary = new Dictionary<string,IConnectionMultiplexer>();

        protected bool LockInstance(IConnectionMultiplexer redisServer, string resource, byte[] val, TimeSpan ttl)
        {
            
            bool succeeded;
            try
            {
                succeeded = redisServer.GetDatabase().StringSet(resource, val, ttl, When.NotExists);
            }
            catch (Exception)
            {
                succeeded = false;
            }
            return succeeded;
        }

        protected void UnlockInstance(IConnectionMultiplexer redisServer, string resource, byte[] val)
        {
            RedisKey[] key = { resource };
            RedisValue[] values = { val };
            redisServer.GetDatabase().ScriptEvaluate(
                UnlockScript, 
                key,
                values
                );
        }

        public bool Lock(RedisKey resource, TimeSpan ttl, out Lock lockObject)
        {
            var val = CreateUniqueLockId();
            Lock innerLock = null;
            bool successfull = retry(DefaultRetryCount, DefaultRetryDelay, () =>
            {
                try
                {
                    int n = 0;
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
                     * precision, which is 1 millisecond, plus 1 millisecond min drift 
                     * for small TTLs.        
                     */
                    var drift = Convert.ToInt32((ttl.TotalMilliseconds * ClockDriveFactor) + 2);
                    var validity_time = ttl - (DateTime.Now - startTime) - new TimeSpan(0, 0, 0, 0, drift);

                    if (n >= Quorum && validity_time.TotalMilliseconds > 0)
                    {
                        innerLock = new Lock(resource, val, validity_time);
                        return true;
                    }
                    else
                    {
                        for_each_redis_registered(
                            redis =>
                            {
                                UnlockInstance(redis, resource, val);
                            }
                        );
                        return false;
                    }
                }
                catch (Exception)
                { return false; }
            });

            lockObject = innerLock;
            return successfull;
        }

        protected void for_each_redis_registered(Action<IConnectionMultiplexer> action)
        {
            foreach (var item in redisMasterDictionary)
            {
                action(item.Value);
            }
        }

        protected bool retry(int retryCount, TimeSpan retryDelay, Func<bool> action)
        {
            int maxRetryDelay = (int)retryDelay.TotalMilliseconds;
            Random rnd = new Random();
            int currentRetry = 0;

            while (currentRetry++ < retryCount)
            {
                if (action()) return true;
                Thread.Sleep(rnd.Next(maxRetryDelay));
            }
            return false;
        }

        public void Unlock(Lock lockObject)
        {
            for_each_redis_registered(redis =>
                {
                    UnlockInstance(redis, lockObject.Resource, lockObject.Value);
                });
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(this.GetType().FullName);

            sb.AppendLine("Registered Connections:");
            foreach(var item in redisMasterDictionary)
            {
                sb.AppendLine(item.Value.GetEndPoints().First().ToString());
            }

            return sb.ToString();
        }
    }
}
