using System;

namespace HauntedFish.Multiplayer
{
    public enum ConveyorRunPolicy
    {
        AfterRoundRelease,
        Periodic,
        Manual
    }

    // Authoritative timing only. Unity objects, round identity and replication stay in TrapManager.
    internal sealed class ConveyorSchedule
    {
        bool _Seeded, _Started, _Suppressed;
        float _RoundAge, _BurstAge, _GapAge, _SpawnDue;

        public void Reset()
        {
            _Seeded = _Started = _Suppressed = false;
            _RoundAge = _BurstAge = _GapAge = _SpawnDue = 0;
        }

        public void SuppressAutomaticStarts() => _Suppressed = true;
        public void AllowAutomaticStarts() => _Suppressed = false;
        public void ClampSpawnDue(float interval) => _SpawnDue = Math.Min(_SpawnDue, SafeInterval(interval));

        public ConveyorRunStart Start(bool running, bool paused, float spawnInterval)
        {
            if (running && !paused)
                return default;

            _Started = true;
            _BurstAge = _GapAge = 0;
            _SpawnDue = SafeInterval(spawnInterval);
            bool seed = !_Seeded;
            _Seeded = true;
            return new ConveyorRunStart(true, seed);
        }

        public ConveyorStep Advance(bool running, bool paused, float seconds, ConveyorTiming timing)
        {
            if (paused || !Finite(seconds) || seconds < 0)
                return new ConveyorStep(running);

            float available = seconds;
            _RoundAge += seconds;
            ConveyorRunStart start = default;
            if (!running && !_Suppressed && timing.Policy != ConveyorRunPolicy.Manual)
            {
                float delay = SafeNonnegative(timing.StartDelay);
                if (!_Started && _RoundAge >= delay)
                {
                    available = Math.Min(seconds, _RoundAge - delay);
                    start = Start(running, paused, timing.SpawnInterval);
                    running = true;
                }
                else if (_Started && timing.Policy == ConveyorRunPolicy.Periodic)
                {
                    _GapAge += seconds;
                    float gap = SafeNonnegative(timing.PauseBetweenRuns);
                    if (_GapAge >= gap)
                    {
                        available = Math.Min(seconds, _GapAge - gap);
                        start = Start(running, paused, timing.SpawnInterval);
                        running = true;
                    }
                }
            }

            if (!running)
                return new ConveyorStep(false);

            float duration = SafeInterval(timing.RunDuration);
            float active = timing.Policy == ConveyorRunPolicy.Periodic
                ? Math.Min(available, Math.Max(0, duration - _BurstAge))
                : available;
            _BurstAge += active;

            _SpawnDue -= active;
            float firstDue = _SpawnDue;
            float interval = SafeInterval(timing.SpawnInterval);
            int attempts = 0;
            while (_SpawnDue <= 0 && attempts < 64)
            {
                _SpawnDue += interval;
                attempts++;
            }

            if (timing.Policy == ConveyorRunPolicy.Periodic && _BurstAge >= duration)
            {
                running = false;
                _GapAge = 0;
            }

            return new ConveyorStep(running, start, active, new ConveyorSpawnBatch(firstDue, interval, attempts));
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static float SafeNonnegative(float value) => Finite(value) ? Math.Max(0, value) : 0;
        internal static float SafeInterval(float value) => Finite(value) ? Math.Max(.05f, value) : .05f;
    }

    internal readonly struct ConveyorTiming
    {
        public readonly ConveyorRunPolicy Policy;
        public readonly float StartDelay, SpawnInterval, RunDuration, PauseBetweenRuns;

        public ConveyorTiming(ConveyorRunPolicy policy, float startDelay, float spawnInterval, float runDuration, float pauseBetweenRuns)
        {
            Policy = policy;
            StartDelay = startDelay;
            SpawnInterval = spawnInterval;
            RunDuration = runDuration;
            PauseBetweenRuns = pauseBetweenRuns;
        }
    }

    internal readonly struct ConveyorRunStart
    {
        public readonly bool Started, SeedInitialPackages;

        public ConveyorRunStart(bool started, bool seedInitialPackages)
        {
            Started = started;
            SeedInitialPackages = seedInitialPackages;
        }
    }

    internal readonly struct ConveyorStep
    {
        public readonly bool Running, AdvancePackages;
        public readonly ConveyorRunStart Start;
        public readonly float ActiveSeconds;
        public readonly ConveyorSpawnBatch Spawns;

        public ConveyorStep(bool running)
        {
            Running = running;
            AdvancePackages = false;
            Start = default;
            ActiveSeconds = 0;
            Spawns = default;
        }

        public ConveyorStep(bool running, ConveyorRunStart start, float activeSeconds, ConveyorSpawnBatch spawns)
        {
            Running = running;
            AdvancePackages = true;
            Start = start;
            ActiveSeconds = activeSeconds;
            Spawns = spawns;
        }
    }

    // Enumerates elapsed time since each spawn was due, without allocating per simulation tick.
    internal readonly struct ConveyorSpawnBatch
    {
        readonly float _FirstDue, _Interval;
        public int Count { get; }

        public ConveyorSpawnBatch(float firstDue, float interval, int count)
        {
            _FirstDue = firstDue;
            _Interval = interval;
            Count = count;
        }

        public Enumerator GetEnumerator() => new Enumerator(_FirstDue, _Interval, Count);

        internal struct Enumerator
        {
            float _NextDue;
            readonly float _Interval;
            int _Remaining;
            public float Current { get; private set; }

            public Enumerator(float firstDue, float interval, int count)
            {
                _NextDue = firstDue;
                _Interval = interval;
                _Remaining = count;
                Current = 0;
            }

            public bool MoveNext()
            {
                if (_Remaining <= 0)
                    return false;
                Current = Math.Max(0, -_NextDue);
                _NextDue += _Interval;
                _Remaining--;
                return true;
            }
        }
    }
}
