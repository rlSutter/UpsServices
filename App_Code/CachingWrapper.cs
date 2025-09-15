using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Caching;
using System.Data;

namespace CachingWrapper
{
    public enum CachePriority
    {
        Default,
        NotRemovable
    }

    public class LocalCache
    {
        // Gets a reference to the default MemoryCache instance. 
        private static ObjectCache cache = MemoryCache.Default;
        private CacheItemPolicy policy = null;
        private CacheEntryRemovedCallback callback = null;

        public void AddToCache(String CacheKeyName, Object CacheItem, CachePriority MyCacheItemPriority)
        {
            // 
            callback = new CacheEntryRemovedCallback(this.CachedItemRemovedCallback);
            policy = new CacheItemPolicy();
            policy.Priority = (MyCacheItemPriority == CachePriority.Default) ? CacheItemPriority.Default : CacheItemPriority.NotRemovable;
            policy.AbsoluteExpiration = DateTimeOffset.Now.AddDays(1.00);
            policy.RemovedCallback = callback;

            // Add inside cache 
            cache.Set(CacheKeyName, CacheItem, policy);
        }

        public Object GetCachedItem(String CacheKeyName)
        {
            // 
            return cache[CacheKeyName] as Object;
        }

        public System.Data.DataTable GetCachedDataTable(string CacheKeyName)
        {
            return cache[CacheKeyName] as System.Data.DataTable;
        }

        public void RemoveCachedItem(String CacheKeyName)
        {
            // 
            if (cache.Contains(CacheKeyName))
            {
                cache.Remove(CacheKeyName);
            }
        }

        private void CachedItemRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            // Log these values from arguments list 
            String strLog = String.Concat("Reason: ", arguments.RemovedReason.ToString(), " | Key-Name: ", arguments.CacheItem.Key, " | Value-Object: ",
            arguments.CacheItem.Value.ToString());
        }
    }
}