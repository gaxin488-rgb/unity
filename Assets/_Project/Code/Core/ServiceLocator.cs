using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfTheLost.Core
{
    /// <summary>
    /// Lightweight service locator.
    /// Systems register themselves once and any other system can retrieve them
    /// without introducing hard MonoBehaviour dependencies.
    /// <para>
    /// Usage:
    /// <code>
    /// // Register (typically in Awake)
    /// ServiceLocator.Register&lt;IAudioManager&gt;(this);
    ///
    /// // Retrieve
    /// var audio = ServiceLocator.Get&lt;IAudioManager&gt;();
    ///
    /// // Unregister (typically in OnDestroy)
    /// ServiceLocator.Unregister&lt;IAudioManager&gt;();
    /// </code>
    /// </para>
    /// </summary>
    public static class ServiceLocator
    {
        // ─── Registry ─────────────────────────────────────────────────────────

        private static readonly Dictionary<Type, object> _services
            = new Dictionary<Type, object>();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Register <paramref name="service"/> under the interface/type <typeparamref name="T"/>.
        /// Overwrites any previously registered instance and logs a warning.
        /// </summary>
        /// <typeparam name="T">The interface or base-type to register under.</typeparam>
        /// <param name="service">Concrete implementation to store.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is null.</exception>
        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            var key = typeof(T);

            if (_services.ContainsKey(key))
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service for {key.Name}.");

            _services[key] = service;
        }

        /// <summary>
        /// Retrieve the service registered under <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Requested interface or type.</typeparam>
        /// <returns>The registered instance cast to <typeparamref name="T"/>.</returns>
        /// <exception cref="ServiceNotFoundException">Thrown when no service is registered for <typeparamref name="T"/>.</exception>
        public static T Get<T>() where T : class
        {
            var key = typeof(T);

            if (_services.TryGetValue(key, out var raw) && raw is T service)
                return service;

            throw new ServiceNotFoundException(
                $"[ServiceLocator] No service registered for type '{key.Name}'. " +
                $"Make sure it is registered before being requested.");
        }

        /// <summary>
        /// Attempt to retrieve the service registered under <typeparamref name="T"/>
        /// without throwing if not found.
        /// </summary>
        /// <typeparam name="T">Requested interface or type.</typeparam>
        /// <param name="service">Populated with the registered instance, or null.</param>
        /// <returns><c>true</c> if the service was found; <c>false</c> otherwise.</returns>
        public static bool TryGet<T>(out T service) where T : class
        {
            var key = typeof(T);

            if (_services.TryGetValue(key, out var raw) && raw is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// Remove the service registered under <typeparamref name="T"/>.
        /// No-op if no service is registered.
        /// </summary>
        /// <typeparam name="T">Type to deregister.</typeparam>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>Remove every registered service (useful for integration tests / full resets).</summary>
        public static void Clear() => _services.Clear();

        /// <summary>Returns whether a service is currently registered for <typeparamref name="T"/>.</summary>
        public static bool IsRegistered<T>() where T : class
            => _services.ContainsKey(typeof(T));
    }

    // ─── Custom Exception ─────────────────────────────────────────────────────

    /// <summary>
    /// Thrown by <see cref="ServiceLocator.Get{T}"/> when no matching service is registered.
    /// </summary>
    public sealed class ServiceNotFoundException : Exception
    {
        public ServiceNotFoundException(string message) : base(message) { }
    }
}
