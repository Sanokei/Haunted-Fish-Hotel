using System;
using System.Collections.Generic;
namespace HauntedFish.Multiplayer
{
    // Owns visual-only cutscene copies, never players or network identities.
    public sealed class HallwayFishStaging<T> where T : class
    {
        readonly Dictionary<uint, T> _Visuals = new Dictionary<uint, T>();
        Action<T> _Release;
        public IReadOnlyDictionary<uint, T> Visuals => _Visuals;
        public void Begin(IEnumerable<uint> roster, Func<uint, T> create, Action<T> release)
        {
            if (roster == null) throw new ArgumentNullException(nameof(roster));
            if (create == null) throw new ArgumentNullException(nameof(create));
            if (release == null) throw new ArgumentNullException(nameof(release));
            End();
            _Release = release;
            var seen = new HashSet<uint>();
            try
            {
                foreach (uint id in roster)
                {
                    if (!seen.Add(id)) continue;
                    var visual = create(id);
                    if (visual != null) _Visuals.Add(id, visual);
                }
            }
            catch { End(); throw; }
        }
        public void End()
        {
            var retired = new List<T>(_Visuals.Values);
            var release = _Release;
            // Clear ownership before callbacks, so reentrant cancellation cannot double-release.
            _Visuals.Clear();
            _Release = null;
            List<Exception> errors = null;
            foreach (var visual in retired)
            {
                try { release(visual); }
                catch (Exception error)
                {
                    if (errors == null) errors = new List<Exception>();
                    errors.Add(error);
                }
            }
            if (errors != null) throw new AggregateException(errors);
        }
    }
}