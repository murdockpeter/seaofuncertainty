using System;
using System.Collections.Generic;
using UnityEngine;

namespace SeaOfUncertainty.Editor
{
    /// <summary>
    /// Shared assertion sink for the editor suites. A failed check is logged and recorded instead of
    /// aborting the suite, so one run reports every failing check; each suite calls
    /// <see cref="ThrowIfAny"/> before declaring itself passed.
    /// </summary>
    public static class TestChecks
    {
        private static readonly List<string> failures = new List<string>();

        public static void Check(bool condition, string label)
        {
            if (condition) return;
            failures.Add(label);
            Debug.LogError("Test failed: " + label);
        }

        public static void ThrowIfAny(string suite)
        {
            if (failures.Count == 0) return;
            string summary = $"{failures.Count} check(s) failed in {suite}:\n  - " + string.Join("\n  - ", failures);
            failures.Clear();
            throw new InvalidOperationException(summary);
        }
    }
}
