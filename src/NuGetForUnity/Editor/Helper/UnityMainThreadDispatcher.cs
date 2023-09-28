using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using UnityEditor;

namespace NugetForUnity.Helper
{
    /// <summary>
    ///     Helper class to dispatch actions on the Unity main thread.
    /// </summary>
    internal static class UnityMainThreadDispatcher
    {
        private static readonly List<Action> PreInitializedActionQueue = new List<Action>();

        [CanBeNull]
        private static SynchronizationContext synchronizationContext = SynchronizationContext.Current;

        /// <summary>
        ///     Add a action to be executed on the Unity Main thread.
        /// </summary>
        /// <param name="action">The action that is executed on the main thread.</param>
        public static void Dispatch([NotNull] Action action)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            if (SynchronizationContext.Current != null)
            {
                // we are on main thread -> just execute
                action();
                return;
            }

            if (synchronizationContext == null)
            {
                PreInitializedActionQueue.Add(action);
                return;
            }

            synchronizationContext.Post(_ => action(), null);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (synchronizationContext == null)
            {
                synchronizationContext = SynchronizationContext.Current;
            }

            if (synchronizationContext == null)
            {
                throw new InvalidOperationException("SynchronizationContext.Current is null in a InitializeOnLoadMethod.");
            }

            foreach (var action in PreInitializedActionQueue)
            {
                action();
            }

            PreInitializedActionQueue.Clear();
        }
    }
}
