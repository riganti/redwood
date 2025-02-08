using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DotVVM.Framework.Binding.Expressions;
using DotVVM.Framework.Binding.Properties;
using DotVVM.Framework.Compilation;
using DotVVM.Framework.Compilation.ControlTree;
using DotVVM.Framework.Runtime.Caching;
using FastExpressionCompiler;

namespace DotVVM.Framework.Binding
{
    public class DotvvmBindingCacheHelper
    {
        private readonly IDotvvmCacheAdapter cache;
        private readonly BindingCompilationService compilationService;

        public DotvvmBindingCacheHelper(IDotvvmCacheAdapter cache, BindingCompilationService compilationService)
        {
            this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this.compilationService = compilationService;
        }

        /// <summary> Creates a new binding using the <paramref name="factory"/>, unless an existing cache entry is found. Entries are identified using the identifier and keys. By default, the cache is LRU with size=1000 </summary>
        public T CreateCachedBinding<T>(string identifier, object?[] keys, Func<T> factory) where T: IBinding
        {
            return this.cache.GetOrAdd(new CacheKey(typeof(T), identifier, keys), _ => {
                foreach (var k in keys)
                    CheckEqualsImplementation(k);
                return new DotvvmCachedItem<T>(factory(), DotvvmCacheItemPriority.High);
            });
        }

        /// <summary> Creates a new binding of type <typeparamref name="T"/> with the specified properties, unless an existing cache entry is found. Entries are identified using the identifier and keys. By default, the cache is LRU with size=1000 </summary>
        public T CreateCachedBinding<T>(string identifier, object[] keys, object[] properties) where T: IBinding
        {
            return CreateCachedBinding<T>(identifier, keys, () => (T)BindingFactory.CreateBinding(this.compilationService, typeof(T), properties));
        }

        /// <summary> Compiles a new `{value: ...code...}` binding which can be evaluated server-side and also client-side. The result is cached. </summary>
        public ValueBindingExpression CreateValueBinding(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding("ValueBinding:" + code, new object?[] { dataContext, parserOptions }, () =>
                new ValueBindingExpression(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions
                }));
        }

        /// <summary> Compiles a new `{value: ...code...}` binding which can be evaluated server-side and also client-side. The result is implicitly converted to <typeparamref name="TResult" />. The result is cached. </summary>
        public ValueBindingExpression<TResult> CreateValueBinding<TResult>(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding($"ValueBinding<{typeof(TResult).ToCode()}>:{code}", new object?[] { dataContext, parserOptions }, () =>
                new ValueBindingExpression<TResult>(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions,
                    new ExpectedTypeBindingProperty(typeof(TResult))
                }));
        }

        /// <summary> Compiles a new `{resource: ...code...}` binding which can be evaluated server-side. The result is cached. <see cref="ResourceBindingExpression.ResourceBindingExpression(BindingCompilationService, IEnumerable{object})" /> </summary>
        public ResourceBindingExpression CreateResourceBinding(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding("ResourceBinding:" + code, new object?[] { dataContext, parserOptions }, () =>
                new ResourceBindingExpression(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions
                }));
        }

        /// <summary> Compiles a new `{resource: ...code...}` binding which can be evaluated server-side. The result is implicitly converted to <typeparamref name="TResult" />. The result is cached. </summary>
        public ResourceBindingExpression<TResult> CreateResourceBinding<TResult>(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding($"ResourceBinding<{typeof(TResult).ToCode()}>:{code}", new object?[] { dataContext, parserOptions }, () =>
                new ResourceBindingExpression<TResult>(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions,
                    new ExpectedTypeBindingProperty(typeof(TResult))
                }));
        }

        /// <summary> Compiles a new `{command: ...code...}` binding which can be evaluated server-side and also client-side. The result is cached. Note that command bindings might be easier to create using the <see cref="CommandBindingExpression.CommandBindingExpression(BindingCompilationService, Func{object[], System.Threading.Tasks.Task}, string)" /> constructor. </summary>
        public CommandBindingExpression CreateCommand(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding($"Command:{code}", new object?[] { dataContext, parserOptions }, () =>
                new CommandBindingExpression(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions
                }));
        }

        /// <summary> Compiles a new `{command: ...code...}` binding which can be evaluated server-side and also client-side. The result is implicitly converted to <typeparamref name="TResult" />. The result is cached. Note that command bindings might be easier to create using the <see cref="CommandBindingExpression.CommandBindingExpression(BindingCompilationService, Func{object[], System.Threading.Tasks.Task}, string)" /> constructor. </summary>
        public CommandBindingExpression<TResult> CreateCommand<TResult>(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding($"Command<{typeof(TResult).ToCode()}>:{code}", new object?[] { dataContext, parserOptions }, () =>
                new CommandBindingExpression<TResult>(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions,
                    new ExpectedTypeBindingProperty(typeof(TResult))
                }));
        }

        /// <summary> Compiles a new `{staticCommand: ...code...}` binding which can be evaluated server-side and also client-side. The result is cached. </summary>
        public StaticCommandBindingExpression CreateStaticCommand(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding($"StaticCommand:{code}", new object?[] { dataContext, parserOptions }, () =>
                new StaticCommandBindingExpression(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions
                }));
        }

        /// <summary> Compiles a new `{staticCommand: ...code...}` binding which can be evaluated server-side and also client-side. The result is implicitly converted to <typeparamref name="TResult" />. The result is cached. </summary>
        public StaticCommandBindingExpression<TResult> CreateStaticCommand<TResult>(string code, DataContextStack dataContext, BindingParserOptions? parserOptions = null)
        {
            return CreateCachedBinding($"StaticCommand<{typeof(TResult).ToCode()}>:{code}", new object?[] { dataContext, parserOptions }, () =>
                new StaticCommandBindingExpression<TResult>(compilationService, new object?[] {
                    dataContext,
                    new OriginalStringBindingProperty(code),
                    parserOptions,
                    new ExpectedTypeBindingProperty(typeof(TResult))
                }));
        }

        internal static void CheckEqualsImplementation(object? k)
        {
            // whitelist for some common singletons
            if (k is null || k is DotvvmProperty) return;

            var t = k.GetType();
            var eqMethod = t.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance, null, new [] { typeof(object) }, null);
            if (eqMethod?.GetBaseDefinition().DeclaringType != typeof(object) || eqMethod.DeclaringType == typeof(object))
            {
                throw new Exception($"Instance of type {t} cannot be used as a cache key, because it does not have Object.Equals method overridden. If you really want to use referential equality (you are using a singleton as a cache key or something like that), you can wrap in Tuple<T>.");
            }
        }

        class CacheKey: IEquatable<CacheKey>
        {
            private readonly object?[] keys;
            private readonly Type type;
            private readonly string id;

            public CacheKey(Type type, string id, object?[] keys)
            {
                this.type = type ?? throw new ArgumentNullException(nameof(type));
                this.id = id ?? throw new ArgumentNullException("Cache identifier can't be null", nameof(id));
                this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
            }
            public bool Equals(CacheKey? other)
            {
                if (other == null || other.keys.Length != keys.Length || other.type != type || id != other.id) return false;
                for (int i = 0; i < keys.Length; i++)
                {
                    if (!object.Equals(other.keys[i], this.keys[i]))
                        return false;
                }
                return true;
            }
            public override bool Equals(object? obj) => Equals(obj as CacheKey);
            public override int GetHashCode()
            {
                var hash = 234567643 ^ keys.Length ^ id.GetHashCode() ^ type.GetHashCode();
                foreach (var k in keys)
                {
                    hash *= 17;
                    hash += k?.GetHashCode() ?? 0;
                }
                return hash;
            }
        }
    }
}
