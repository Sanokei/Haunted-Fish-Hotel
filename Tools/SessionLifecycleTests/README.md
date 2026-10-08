# Session lifecycle validation

Run `dotnet run --project Tools/SessionLifecycleTests/SessionLifecycleTests.csproj`.

Current result: **PASS 131 checks**. Failed assertions exit with code 1 and print their scenario without invoking a native crash handler.

This suite compiles the production `HotelSessionLifecycle`, its typed ports, session states and lobby contracts directly. Its scheduler traverses nested coroutines with controlled time and delay objects. Directory callbacks can complete after cancellation or disposal to exercise stale-result rejection.

Coverage includes connection state order, admission and character readiness, normalized joins, replacing/closing owned and unowned rooms, transport failure/backoff, timeouts/startup failure, queued callbacks, heartbeat failure and cancellation, quickplay presentation/readiness gates, disposal, and prepared transferred-host startup with carried bans and capacity. Reentrant state listeners exercise leave/dispose during transport preparation, directory lookup and peer startup notifications, proving cancelled operations cannot initiate the next external side effect.

These are deterministic session-policy tests. Unity scene loading, authored assets, Mirage RPC weaving, actual directory/relay integration, and two admitted clients require their existing isolated native workflows; this suite establishes no live-network result.
