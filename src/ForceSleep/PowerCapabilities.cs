namespace ForceSleep;

/// <summary>What the firmware and Windows allow on this machine (SYSTEM_POWER_CAPABILITIES).</summary>
internal sealed record PowerCapabilities(bool S1, bool S2, bool S3, bool S4, bool HiberFilePresent, bool ModernStandby)
{
    /// <summary>Classic (S1-S3) sleep, which we can verify and force.</summary>
    public bool CanSleep => S1 || S2 || S3;

    public bool CanHibernate => S4 && HiberFilePresent;

    public static PowerCapabilities Query()
    {
        // The struct is ~76 bytes; a larger buffer keeps us safe if it grows.
        var raw = new byte[256];
        if (!Native.GetPwrCapabilities(raw))
            return new PowerCapabilities(false, false, false, false, false, false);

        // Byte offsets of the BOOLEAN fields in SYSTEM_POWER_CAPABILITIES.
        return new PowerCapabilities(
            S1: raw[3] != 0,
            S2: raw[4] != 0,
            S3: raw[5] != 0,
            S4: raw[6] != 0,
            HiberFilePresent: raw[8] != 0,
            ModernStandby: raw[20] != 0);
    }
}
