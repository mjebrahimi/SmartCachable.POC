using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace SmartCachable
{
    public static class DbContextExtensions
    {
        public static List<string> BeforeSaveChanges(this DbContext dbContext)
        {
            var categories = dbContext.ChangeTracker.Entries<CategoryEntity>().ToList();
            var products = dbContext.ChangeTracker.Entries<ProductEntity>().ToList();
            var identityTokens = dbContext.ChangeTracker.Entries<IdentityTokenEntity>().ToList();
            var users = dbContext.ChangeTracker.Entries<UserEntity>().ToList();
            var businesses = dbContext.ChangeTracker.Entries<BusinessEntity>().ToList();

            var keys = new List<string>();
            keys.AddRange(FindInvalidatedCacheKeys(categories));
            keys.AddRange(FindInvalidatedCacheKeys(products));
            keys.AddRange(FindInvalidatedCacheKeys(identityTokens));
            keys.AddRange(FindInvalidatedCacheKeys(users));
            keys.AddRange(FindInvalidatedCacheKeys(businesses));
            return keys.Distinct().ToList();
        }

        public static void AfterSaveChanges(this DbContext dbContext, List<string> invalidatedCacheKeys)
        {
            if (invalidatedCacheKeys == null || invalidatedCacheKeys.Count == 0)
                return;

            var cacheManager = dbContext.GetInfrastructure().GetRequiredService<CacheManager>();
            foreach (var key in invalidatedCacheKeys)
            {
                cacheManager.Remove(key);
            }
        }

        private static IEnumerable<string> FindInvalidatedCacheKeys<T>(List<EntityEntry<T>> entries) where T : class
        {
            foreach (var entry in entries)
            {
                var changeKind = entry.State.ToChangeKing();
                if (changeKind == null)
                    continue;

                foreach (var cacheRule in CacheRules<T>.Rules)
                {
                    if (cacheRule.Predicate(entry.Entity, changeKind.Value))
                    {
                        yield return cacheRule.CacheKey;
                    }
                }
            }
        }

        private static ChangeKind? ToChangeKing(this EntityState entityState)
        {
            return entityState switch
            {
                EntityState.Added => ChangeKind.Insert,
                EntityState.Modified => ChangeKind.Update,
                EntityState.Deleted => ChangeKind.Delete,
                _ => null
            };
        }
    }

    public class CacheManager
    {
        private readonly IMemoryCache memoryCache;
        private readonly CacheKeyGenerator cacheKeyGenerator;

        public CacheManager(IMemoryCache memoryCache, CacheKeyGenerator cacheKeyGenerator)
        {
            this.memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            this.cacheKeyGenerator = cacheKeyGenerator ?? throw new ArgumentNullException(nameof(cacheKeyGenerator));
        }

        public async Task<List<T>> GetAsync<T>(Func<Task<List<T>>> retrieve, object keyValues, Action<CacheRuleConfigure> ruleConfig, int cacheMinutes = 15)
        {
            var key = cacheKeyGenerator.GenerateKey<T>(keyValues);
            var cacheRuleConfigure = new CacheRuleConfigure(key);
            ruleConfig(cacheRuleConfigure);

            var hitFromDatabase = false;
            var result = await memoryCache.GetOrCreateAsync(key, async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheMinutes);
                var result = await retrieve();
                hitFromDatabase = true;
                return result;
            });

            Console.WriteLine($"Hit From {(hitFromDatabase ? "Database" : "Cache")} ({result.Count} {result.FirstOrDefault()?.GetType().Name})" + Environment.NewLine);
            //Console.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));

            return result;
        }

        public void Remove<T>(object values)
        {
            var key = cacheKeyGenerator.GenerateKey<T>(values);
            Console.WriteLine($"Cache removed by key: {key}" + Environment.NewLine);
            memoryCache.Remove(key);
        }

        public void Remove(string key)
        {
            Console.WriteLine($"Cache removed by key: {key}" + Environment.NewLine);
            memoryCache.Remove(key);
        }
    }

    public class CacheKeyGenerator
    {
        public string GenerateKey<T>(object keyValues)
        {
            var keyValuesStr = GetKeyValues(keyValues);
            if (string.IsNullOrEmpty(keyValuesStr) is false)
            {
                keyValuesStr = "." + keyValuesStr;
            }

            return $"EFCore.SmartCachable.{typeof(T).FullName}{keyValuesStr}";

            //Example: EFCore.SmartCachable.MyNameSpace.ProductEntity
            //Example: EFCore.SmartCachable.MyNameSpace.ProductEntity.BusinessId-1_CategoryId-2
        }

        private static string GetKeyValues(object keyValues)
        {
            if (keyValues == null)
                return "";

            var props = keyValues.GetType().GetProperties();
            var array = props.Select(prop => prop.Name + "-" + prop.GetValue(keyValues, null)).ToArray();
            return string.Join("_", array);

            //var list = new List<string>();
            //foreach (PropertyDescriptor propertyDescriptor in TypeDescriptor.GetProperties(keyValues))
            //{
            //    var value = propertyDescriptor.GetValue(keyValues);
            //    list.Add($"{propertyDescriptor.Name}-{value}");
            //}
            //return string.Join("_", list);
        }
    }

    public class CacheRuleConfigure
    {
        private readonly string cacheKey;
        public CacheRuleConfigure(string cacheKey)
        {
            this.cacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
        }

        public CacheRuleConfigure AddCacheRule<TEntity>(Func<TEntity, ChangeKind, bool> predicate)
        {
            var cacheRule = new CacheRule<TEntity>(cacheKey, predicate);

            //workaround for uniqueness
            if (CacheRules<TEntity>.Rules.Any(c => c.GetHashCode() == cacheRule.GetHashCode()) is false)
                CacheRules<TEntity>.Rules.Add(cacheRule);

            return this;
        }
    }

    public static class CacheRules<TEntity>
    {
        public static ConcurrentBag<CacheRule<TEntity>> Rules { get; } = new();
    }

    //workaournd for uniqueness
    public struct CacheRule<TEntity>
    {
        public CacheRule(string cacheKey, Func<TEntity, ChangeKind, bool> predicate)
        {
            CacheKey = cacheKey ?? throw new ArgumentNullException(nameof(cacheKey));
            Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public Func<TEntity, ChangeKind, bool> Predicate { get; }
        public string CacheKey { get; }
    }

    public enum ChangeKind
    {
        Insert,
        Update,
        Delete
    }
}
