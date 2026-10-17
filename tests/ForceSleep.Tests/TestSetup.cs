using System.Runtime.CompilerServices;

// The UI language is process-wide state that LanguageTests switches, so tests must not overlap.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ForceSleep.Tests;

internal static class TestSetup
{
    /// <summary>Most tests assert Turkish text, independent of the machine's display language.</summary>
    [ModuleInitializer]
    internal static void UseTurkish() => L.Turkish = true;
}
