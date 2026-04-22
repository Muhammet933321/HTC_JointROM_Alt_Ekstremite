/// <summary>
/// Task types matching PDF §9 task list.
/// </summary>
public enum TaskType
{
    /// <summary>Dik duruş – postural baseline (§9.1)</summary>
    Standing,

    /// <summary>Sağa eğilme (§9.2)</summary>
    LeanRight,

    /// <summary>Sola eğilme (§9.2)</summary>
    LeanLeft,

    /// <summary>Öne eğilme (§9.3)</summary>
    LeanForward,

    /// <summary>Sağ ayak üzerinde denge (§9.4)</summary>
    SingleLegBalance_R,

    /// <summary>Sol ayak üzerinde denge (§9.4)</summary>
    SingleLegBalance_L,

    /// <summary>Mini squat / çömelme (§9.5)</summary>
    MiniSquat,

    /// <summary>Yürüme simülasyonu / adım alma (§9.6)</summary>
    WalkSimulation
}
