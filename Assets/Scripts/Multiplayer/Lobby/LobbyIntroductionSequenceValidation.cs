using System;
using System.Collections.Generic;
using Monologue.Dialogue;

namespace HauntedFish.Multiplayer
{
    // Fail before presentation begins; a missing world shot must never look like a completed intro.
    public static class LobbyIntroductionSequenceValidation
    {
        public static IReadOnlyList<StorySequenceStep> UIOnly(
            IReadOnlyList<StorySequenceStep> steps, Func<StorySequenceStep, bool> supportsWorld)
        {
            var ui = new List<StorySequenceStep>();
            foreach (var step in steps)
            {
                bool world = step.Kind == StorySequenceStepKind.LobbyShot || step.Kind == StorySequenceStepKind.LobbyLine;
                if (!world) { ui.Add(step); continue; }
                if (supportsWorld == null)
                    throw new InvalidOperationException("Assign LobbyIntroductionPresentation._World before playing the lobby intro.");
                if (!supportsWorld(step))
                    throw new InvalidOperationException("Unbound lobby intro world operation: " + step.Kind +
                        "(" + step.Target + "). Check the authored camera/shot markers or caption on _World.");
            }
            return ui;
        }
    }
}
