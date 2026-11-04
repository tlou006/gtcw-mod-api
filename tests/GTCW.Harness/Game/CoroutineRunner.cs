using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GTCW.Harness.Game
{
    /// <summary>
    /// Runs a test coroutine, nested waits and all, and reports how it ended.
    /// </summary>
    /// <remarks>
    /// Unity's own <c>StartCoroutine</c> logs an exception and silently stops,
    /// which would turn a failed assertion into a hang. This steps the
    /// enumerators itself: a nested <c>IEnumerator</c> runs in place, anything
    /// else (<c>null</c>, a <c>WaitForSeconds</c>) is handed to Unity.
    /// </remarks>
    internal static class CoroutineRunner
    {
        public static IEnumerator Run(IEnumerator body, float timeoutSeconds, Action<Exception?> finished)
        {
            // A List used as a stack, not Stack<T>: Unity's netstandard facade
            // forwards Stack<T> to mscorlib, where .NET Framework does not have
            // it, and GTCW.Harness must load cleanly in the engine tier too.
            var stack = new List<IEnumerator> { body };
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;

            while (stack.Count > 0)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    finished(new TimeoutException($"exceeded its {timeoutSeconds:0} s limit"));
                    yield break;
                }

                IEnumerator top = stack[stack.Count - 1];
                bool moved;
                try
                {
                    moved = top.MoveNext();
                }
                catch (Exception ex)
                {
                    finished(ex);
                    yield break;
                }

                if (!moved)
                {
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                if (top.Current is IEnumerator nested)
                {
                    stack.Add(nested);
                    continue;
                }

                yield return top.Current;
            }

            finished(null);
        }
    }
}
