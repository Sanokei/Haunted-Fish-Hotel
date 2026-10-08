using System;
using Ink.Runtime;

namespace Monologue.Dialogue
{
    internal enum DialogueAdvanceResult
    {
        LineReady,
        WaitingForInput,
        WaitingForChoice,
        Ended
    }

    /// <summary>
    /// Owns one executable Ink story and its global-variable subscription.
    /// Presentation, player input, external-function bindings, and network routing
    /// remain with the manager. Remote snapshots never create this session.
    /// </summary>
    internal sealed class DialogueStorySession : IDisposable
    {
        readonly Variables globals;
        bool hasPendingLine;
        bool disposed;

        public Story Story { get; }

        public DialogueStorySession(Story story, Variables globals)
        {
            Story = story ?? throw new ArgumentNullException(nameof(story));
            this.globals = globals;
            globals?.StartListening(story);
        }

        public DialogueAdvanceResult Advance(Func<bool> inputActive)
        {
            if (disposed)
                return DialogueAdvanceResult.Ended;
            if (!hasPendingLine && !Story.canContinue)
                return Story.currentChoices.Count > 0
                    ? DialogueAdvanceResult.WaitingForChoice
                    : DialogueAdvanceResult.Ended;

            if (!hasPendingLine)
            {
                Story.Continue();
                // External Ink functions may close or replace the current dialogue.
                if (disposed) return DialogueAdvanceResult.Ended;
                hasPendingLine = true;
            }

            // Continue may invoke InputText. Keep the resulting line until submission,
            // then present it without executing Ink and its effects a second time.
            if (inputActive != null && inputActive())
                return DialogueAdvanceResult.WaitingForInput;

            hasPendingLine = false;
            return Story.currentText == "" && !Story.canContinue && Story.currentChoices.Count == 0
                ? DialogueAdvanceResult.Ended
                : DialogueAdvanceResult.LineReady;
        }

        public bool TryChoose(int index)
        {
            if (disposed || index < 0 || index >= Story.currentChoices.Count)
                return false;

            Story.ChooseChoiceIndex(index);
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            hasPendingLine = false;
            globals?.StopListening(Story);
        }
    }
}
