using System;
using System.Reflection;
using System.Runtime.Serialization;

namespace GTCW.Harness.GameState
{
    /// <summary>
    /// Unity objects with no engine behind them, for the engine tier only.
    /// </summary>
    /// <remarks>
    /// Built without a constructor, so every field starts at its default and a
    /// test sets only what the method under test reads. Unity's "is it alive"
    /// test is <c>m_CachedPtr != 0</c>, so one field makes <c>== null</c> treat
    /// the object as real. Inside the real game, use real objects: a fake there
    /// has no native counterpart and any engine call on it will fail.
    /// </remarks>
    public static class Fakes
    {
        private static readonly FieldInfo CachedPtr =
            typeof(UnityEngine.Object).GetField("m_CachedPtr", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException("UnityEngine.Object", "m_CachedPtr");

        public static T Make<T>() where T : UnityEngine.Object
        {
            var instance = (T)FormatterServices.GetUninitializedObject(typeof(T));
            CachedPtr.SetValue(instance, new IntPtr(1));
            return instance;
        }
    }
}
