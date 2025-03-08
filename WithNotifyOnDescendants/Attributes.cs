using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace WithNotifyOnDescendants.Proto
{
    /// <summary>
    /// Attribute to mark properties that should be ignored in the notification system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IgnoreNODAttribute : Attribute { }

    /// <summary>
    /// Attribute that specifies a property to watch for value creation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class WaitForValueCreatedAttribute : Attribute
    {
        public WaitForValueCreatedAttribute(string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentNullException(nameof(propertyName));
            IsValueCreatedPropertyName = propertyName;
        }
        public string IsValueCreatedPropertyName { get; }
    }

    /// <summary>
    /// Manages multiple LazyProxy instances, ensuring that lazy-initialized properties 
    /// are tracked and notified upon creation.
    /// </summary>

    public class LazyProxyDictionary
    {
        public Task watch
        {
            get
            {
                if (_watch is null)
                {
                    _watch = Task.Run(async () =>
                    {
                        //while (token?.IsCancellationRequested != true)
                        //{
                        //    await Task.Delay(pollSpan ?? TimeSpan.FromSeconds(0.5), token ?? new CancellationToken());
                        //    if (lazy.IsValueCreated) return;
                        //}
                    });
                    _watch.ConfigureAwait(true)
                    .GetAwaiter()
                    .OnCompleted(() =>
                    {
                        //onPropertyChanged(name ?? "Unknown");
                    });
                }
                return _watch;
            }
        }
        Task? _watch = default;

        /// <summary>
        /// Retrieves or creates a LazyProxy for the given lazy-initialized object and 
        /// returns whether its value has been created.
        /// </summary>
        /// <param name="lazy">The lazy-initialized object.</param>
        /// <param name="notify">Action to invoke when value creation is detected.</param>
        /// <param name="pollSpan">Polling interval for monitoring value creation.</param>
        /// <param name="token">Cancellation token to stop monitoring.</param>
        /// <returns>True if the value has been created, false otherwise.</returns>
        public bool this[
            object lazy, 
            Action notify,
            TimeSpan? pollSpan = null,
            CancellationToken? token = null]
        {
            get
            {
                if(!Proxies.TryGetValue(lazy, out var proxy))
                {
                    proxy = new LazyProxy(lazy, notify);
                    Proxies[lazy] = proxy;
                }
                return proxy.IsValueCreated;
            }
        }
        private readonly Dictionary<object, LazyProxy> Proxies = new();
    }
    /// <summary>
    /// Represents a proxy that monitors a Lazy<T> instance and invokes a notification 
    /// when its value is created.
    /// 
    /// Problem: Lazy<T> does not provide notifications when its value is created, 
    ///          making it difficult to detect when a property becomes available.
    /// 
    /// Solution: Implements a LazyProxy mechanism that watches for the value to be 
    ///           created and triggers a notification when it happens.
    /// </summary>
    /// <param name="lazy">The lazy-initialized object.</param>
    /// <param name="notify">Action to invoke when value creation is detected.</param>
    /// <param name="pollSpan">Polling interval for monitoring value creation.</param>
    /// <param name="token">Cancellation token to stop monitoring.</param>
    /// <returns>True if the value has been created, false otherwise.</returns>
    public class LazyProxy
    {
        public static LazyProxyDictionary Notify { get; } = new();
        public LazyProxy(
            object lazy,
            Action notify,
            TimeSpan? pollSpan = null,
            CancellationToken? token = null)
        {
            if (lazy.GetType().GetProperty("IsValueCreated") is { } pi)
            {
                _lazy = lazy;
                _isValueCreated = () =>
                    (_lazy is null)
                    ? false
                    : Equals(pi.GetValue(_lazy), true);

                Task.Run(async () =>
                {
                    while (token?.IsCancellationRequested != true)
                    {
                        await Task.Delay(pollSpan ?? TimeSpan.FromSeconds(0.5), token ?? new CancellationToken());
                        if (_isValueCreated()) return;
                    }
                })
                .ConfigureAwait(true)
                .GetAwaiter()
                .OnCompleted(() =>
                {
                    if(token?.IsCancellationRequested != true)
                    {
                        notify?.Invoke();
                    }
                });
            }
        }
        private readonly object? _lazy;
        private readonly Func<bool>? _isValueCreated = null;
        public bool IsValueCreated => _isValueCreated?.Invoke() == true;
    }
}
