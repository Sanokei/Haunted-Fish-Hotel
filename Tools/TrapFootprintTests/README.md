Run `dotnet run --project Tools/TrapFootprintTests --artifacts-path <Temp output>`.
Tests exercise the production SAT footprint math for intersection, adjacency,
rotation, containment, symmetry and rotation invariance. Integration assertions
check the preview/authority shared path, revalidation-before-inventory-mutation,
current/reset reservations, rendering order and cleanup wiring. These assertions
are source checks and do not replace Unity play mode, networking or pixel QA.
The trap world reserves full X/Z body footprints regardless of height, preventing
ceiling traps from sharing their landing column with another trap. A moved cart
keeps its original column reserved for resetting as well as its current column.