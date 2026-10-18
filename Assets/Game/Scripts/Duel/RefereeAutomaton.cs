// See FiniteAutomaton.cs for how the automata of the duel are defined and used.

// ---------- Referee (Jacana, in the middle): follows the round from the first choice to the verdict ----------

// Q = {Waiting, Watching, Judging, Ruling, Finished}, q0 = Waiting, F = {Finished}
public enum RefereeState { Waiting, Watching, Judging, Ruling, Finished }

// Sigma = {Pick (the first player chose), Both (the second one chose), Rule (the round is resolved),
//          Next (a new round starts), End (a player's health reached 0: the match is over)}
public enum RefereeSymbol { Pick, Both, Rule, Next, End }

public static class RefereeAutomaton
{
    public static bool IsFinal(RefereeState state) => state == RefereeState.Finished;

    public static RefereeState Step(RefereeState state, RefereeSymbol symbol)
    {
        switch (state)
        {
            case RefereeState.Waiting:
                if (symbol == RefereeSymbol.Pick) return RefereeState.Watching;
                break;
            case RefereeState.Watching:
                if (symbol == RefereeSymbol.Both) return RefereeState.Judging;
                break;
            case RefereeState.Judging:
                if (symbol == RefereeSymbol.Rule) return RefereeState.Ruling;
                break;
            case RefereeState.Ruling:
                if (symbol == RefereeSymbol.Next) return RefereeState.Waiting;
                if (symbol == RefereeSymbol.End) return RefereeState.Finished;
                break;
        }
        return state;
    }
}
