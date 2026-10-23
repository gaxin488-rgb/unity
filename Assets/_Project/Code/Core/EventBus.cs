using System;
using System.Collections.Generic;
using UnityEngine;

namespace EchoesOfTheLost.Core
{
    /// <summary>
    /// Marker interface for all game events.
    /// Implement this on value types (structs) for zero-allocation publishing.
    /// </summary>
    public interface IGameEvent { }

    /// <summary>
    /// Static, thread-safe, generic publish/subscribe event bus.
    /// <para>
    /// Usage:
    /// <code>
    /// // Subscribe
    /// EventBus.Subscribe&lt;PlayerDiedEvent&gt;(OnPlayerDied);
    ///
    /// // Publish
    /// EventBus.Publish(new PlayerDiedEvent { Position = transform.position });
    ///
    /// // Unsubscribe (always do this in OnDestroy)
    /// EventBus.Unsubscribe&lt;PlayerDiedEvent&gt;(OnPlayerDied);
    /// </code>
    /// </para>
    /// </summary>
    public static class EventBus
    {
        // ─── Internal registry ────────────────────────────────────────────────

        /// <summary>
        /// Per-event-type handler list.
        /// The inner object stores a <c>List&lt;Action&lt;T&gt;&gt;</c> for each T.
        /// </summary>
        private static readonly Dictionary<Type, object> _handlers
            = new Dictionary<Type, object>();

        private static readonly object _lock = new object();

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Subscribe <paramref name="handler"/> to events of type <typeparamref name="T"/>.
        /// Safe to call multiple times — duplicate handlers are tolerated (they fire once per registration).
        /// </summary>
        /// <typeparam name="T">An <see cref="IGameEvent"/> type.</typeparam>
        /// <param name="handler">Callback invoked when T is published.</param>
        public static void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var list = GetOrCreate<T>();
                list.Add(handler);
            }
        }

        /// <summary>
        /// Unsubscribe <paramref name="handler"/> from events of type <typeparamref name="T"/>.
        /// No-op if the handler was not registered.
        /// </summary>
        /// <typeparam name="T">An <see cref="IGameEvent"/> type.</typeparam>
        /// <param name="handler">Previously registered callback.</param>
        public static void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null) return;

            lock (_lock)
            {
                if (_handlers.TryGetValue(typeof(T), out var raw) && raw is List<Action<T>> list)
                    list.Remove(handler);
            }
        }

        /// <summary>
        /// Publish an event of type <typeparamref name="T"/> to all current subscribers.
        /// Handlers are invoked on the calling thread. Exceptions in individual handlers
        /// are caught and logged so that remaining subscribers still receive the event.
        /// </summary>
        /// <typeparam name="T">An <see cref="IGameEvent"/> type.</typeparam>
        /// <param name="evt">The event payload.</param>
        public static void Publish<T>(T evt) where T : IGameEvent
        {
            List<Action<T>> snapshot;

            lock (_lock)
            {
                if (!_handlers.TryGetValue(typeof(T), out var raw) || raw is not List<Action<T>> list)
                    return;

                // Snapshot so handlers can safely subscribe/unsubscribe during dispatch.
                snapshot = new List<Action<T>>(list);
            }

            foreach (var handler in snapshot)
            {
                try
                {
                    handler(evt);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventBus] Exception in handler for {typeof(T).Name}: {ex}");
                }
            }
        }

        /// <summary>
        /// Remove all subscribers for every event type.
        /// Call this when tearing down a scene that registered local handlers.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _handlers.Clear();
            }
        }

        /// <summary>
        /// Remove all subscribers for a specific event type <typeparamref name="T"/>.
        /// </summary>
        public static void Clear<T>() where T : IGameEvent
        {
            lock (_lock)
            {
                _handlers.Remove(typeof(T));
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static List<Action<T>> GetOrCreate<T>() where T : IGameEvent
        {
            var key = typeof(T);
            if (!_handlers.TryGetValue(key, out var raw) || raw is not List<Action<T>> list)
            {
                list = new List<Action<T>>();
                _handlers[key] = list;
            }
            return list;
        }
    }
}
