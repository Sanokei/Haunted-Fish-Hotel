using System;
using System.Collections.Generic;
namespace HauntedFish.Multiplayer
{
    // Host-owned state. Cinematic completion and deliberate readiness are separate facts.
    public sealed class GameReadyGate
    {
        sealed class Member { public bool Intro, Ready; }
        readonly Dictionary<uint, Member> _Members = new Dictionary<uint, Member>();
        string _Key; int _Version;
        public int Count => _Members.Count;
        public int ReadyCount { get { int count = 0; foreach (var m in _Members.Values) if (m.Ready) count++; return count; } }
        public bool Complete => Count > 0 && ReadyCount == Count;
        public void Begin(IEnumerable<uint> roster, string key, int version)
        {
            _Members.Clear(); _Key = key; _Version = version;
            foreach (var id in roster) _Members[id] = new Member();
        }
        bool Scope(string key, int version) => !string.IsNullOrEmpty(key) && key == _Key && version == _Version;
        public void Join(uint id, string key, int version) { if (Scope(key, version)) _Members[id] = new Member(); }
        public bool FinishIntro(uint id, string key, int version)
        {
            if (!Scope(key, version) || !_Members.TryGetValue(id, out var member)) return false;
            member.Intro = true; return true;
        }
        public bool Ready(uint id, uint ownedId, string key, int version)
        {
            if (id != ownedId || !Scope(key, version) || !_Members.TryGetValue(id, out var member) || !member.Intro) return false;
            member.Ready = true; return true;
        }
        public bool IsReady(uint id) => _Members.TryGetValue(id, out var m) && m.Ready;
        public void Remove(uint id) => _Members.Remove(id);
    }
}
