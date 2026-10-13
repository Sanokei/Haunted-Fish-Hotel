#if UNITY_EDITOR
using System;
public static class BossDefenseAuthoring {
 public static void Apply(){throw new InvalidOperationException("Obsolete fixed-item authoring disabled. Use the serialized catalog authoring batch.");}
 public static void AppendOwnedPlayerAdapter(){throw new InvalidOperationException("Existing authored BossDefenseRequest is preserved; no shared-player repair factory is allowed.");}
}
#endif
