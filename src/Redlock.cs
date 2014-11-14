using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Redlock.CSharp
{
    public class Redlock
    {
        const int DefaultRetryCount = 3;
        readonly TimeSpan DefaultRetryDelay = new TimeSpan(0, 0, 200);

        /// <summary>
        /// String containing the Lua unlock script.
        /// </summary>
        const String UnlockScript = @"
            if redis.call(""get"",KEYS[1]) == ARGV[1] then
                return redis.call(""del"",KEYS[1])
            else
                return 0
            end";


        public static byte[] CreateUniqueLockId()
        {
            return Guid.NewGuid().ToByteArray();
        }


        private ConnectionMultiplexer redis;



        public bool LockInstance(string resource, byte[] val, TimeSpan ttl)
        {
            bool succeeded;
            try
            {
                succeeded = redis.GetDatabase().StringSet(resource, val, ttl, When.NotExists);
            }
            catch (Exception)
            {
                succeeded = false;
            }
            return succeeded;
        }

        public void UnlockInstance(string resource, byte[] val)
        {
            /// Use hash?
            RedisKey[] key = { resource };
            RedisValue[] values = { val };
            redis.GetDatabase().ScriptEvaluate(
                UnlockScript, 
                key,
                values
                );
        }


    }
}
