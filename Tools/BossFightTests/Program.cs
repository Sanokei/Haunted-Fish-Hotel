using System;
using HauntedFish.BossFight;

class Program
{
    static int checks;
    static void Check(bool value, string name)
    {
        if (!value)
            throw new Exception(name);
        checks++;
    }

    static void Main()
    {
        var m = new BossMatch();
        int results = 0;
        BossMatchResult last = default;
        m.Completed += r =>
        {
            results++;
            last = r;
        };
        Check(!m.TryBegin(0, 2), "zero fish");
        Check(!m.TryBegin(1, 1), "same player");
        for (int i = 0; i < 100; i++)
        {
            Check(m.TryBegin(1, 2), "begin");
            uint epoch = m.Epoch;
            Check(!m.TryBegin(3, 4), "busy");
            Check(!m.Score(epoch - 1, true), "stale goal");
            Check(m.Score(epoch, true), "fish first goal");
            Check(!m.Active && m.ResultIssued && last.WinnerFish && !last.Cancelled, "fish win");
            Check(!m.Score(epoch, false) && !m.Cancel(1), "duplicate result");
            Check(results == i * 3 + 1, "fish result once");
            Check(m.TryBegin(1, 2), "ghost begin");
            Check(m.Score(m.Epoch, false) && m.Active, "ghost first stays");
            Check(m.Score(m.Epoch, false) && !m.Active && !last.WinnerFish, "ghost second wins");
            Check(results == i * 3 + 2, "ghost result once");
            Check(m.TryBegin(1, 2), "cancel begin");
            Check(!m.Cancel(3), "spectator leaves");
            Check(m.Cancel(i % 2 == 0 ? 1u : 2u) && last.Cancelled, "participant departure");
            Check(!m.Cancel(1) && results == i * 3 + 3, "cancel once");
        }

        Console.WriteLine(checks + " BossFight rule checks passed.");
    }
}
