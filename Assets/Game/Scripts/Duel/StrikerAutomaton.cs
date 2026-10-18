// See FiniteAutomaton.cs for how the automata of the duel are defined and used.

// ---------- Striker (Charmadillo, Player 1): attacks, charges, defends; it cannot heal ----------

// Q = {Idle, Attacking, Charging, Defending, Hurt, Victory, Defeated}, q0 = Idle, F = {Victory, Defeated}
public enum StrikerState { Idle, Attacking, Charging, Defending, Hurt, Victory, Defeated }

// Sigma = {Atk, Chg, Def (the move was played), Hit (took damage), Done (the round is over), Hp0 (health <= 0), Win (the other one fell)}
public enum StrikerSymbol { Atk, Chg, Def, Hit, Done, Hp0, Win }

public static class StrikerAutomaton
{
    public static bool IsFinal(StrikerState state) => state == StrikerState.Victory || state == StrikerState.Defeated;

    public static StrikerState Step(StrikerState state, StrikerSymbol symbol)
    {
        switch (state)
        {
            case StrikerState.Idle:
                switch (symbol)
                {
                    case StrikerSymbol.Atk: return StrikerState.Attacking;
                    case StrikerSymbol.Chg: return StrikerState.Charging;
                    case StrikerSymbol.Def: return StrikerState.Defending;
                    case StrikerSymbol.Hit: return StrikerState.Hurt;
                    case StrikerSymbol.Hp0: return StrikerState.Defeated;
                    case StrikerSymbol.Win: return StrikerState.Victory;
                }
                break;

            case StrikerState.Attacking:
            case StrikerState.Charging:
            case StrikerState.Defending:
                switch (symbol)
                {
                    case StrikerSymbol.Done: return StrikerState.Idle;
                    case StrikerSymbol.Hit: return StrikerState.Hurt;   // an interrupted attack, a broken guard
                    case StrikerSymbol.Hp0: return StrikerState.Defeated;
                    case StrikerSymbol.Win: return StrikerState.Victory;
                }
                break;

            case StrikerState.Hurt:
                switch (symbol)
                {
                    case StrikerSymbol.Done: return StrikerState.Idle;
                    case StrikerSymbol.Hit: return StrikerState.Hurt;
                    case StrikerSymbol.Hp0: return StrikerState.Defeated;
                    case StrikerSymbol.Win: return StrikerState.Victory;
                }
                break;

            // Victory and Defeated are final: no symbol leaves them
        }
        return state;
    }
}
