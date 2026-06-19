namespace VpnSc.Services;

public enum VpnActiveStack
{
    None,
    Stealth,
    Awg
}

public static class VpnSessionService
{
    public static VpnActiveStack ActiveStack { get; private set; } = VpnActiveStack.None;

    public static void SetActiveStack(VpnActiveStack stack) => ActiveStack = stack;

    public static void Reset() => ActiveStack = VpnActiveStack.None;
}
