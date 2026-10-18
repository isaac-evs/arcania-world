using System;

// The three finite automata of the duel (not in the Python game). Each one is M = (Q, Sigma, delta, q0, F):
//   Q      the enum of states        Sigma  the enum of symbols        q0  the first value of the state enum (0)
//   delta  the Step function below, one `switch` on the state          F   the states that end the automaton (IsFinal)
// A (state, symbol) pair that no case handles is not in delta: the automaton stays where it is (the symbol is ignored).
// Every state is an animation of an Animator Controller: the DuelAssetBuilder editor tool reads Step to draw the same
// states and transitions in the Animator window, and at run time the integer parameter "State" selects the state.

// A finite automaton driven by a Step function; Current is the state it is in
public class FiniteAutomaton<TState, TSymbol> where TState : struct, Enum where TSymbol : struct, Enum
{
    readonly Func<TState, TSymbol, TState> delta;
    readonly Func<TState, bool> isFinal;

    public TState Current { get; private set; }
    public bool IsFinal => isFinal(Current);

    public FiniteAutomaton(Func<TState, TSymbol, TState> step, Func<TState, bool> final)
    {
        delta = step;
        isFinal = final;
        Current = default; // q0: the first value of the enum
    }

    public void Feed(TSymbol symbol)
    {
        Current = delta(Current, symbol);
    }
}

// ---------- Striker (Cindrill, Player 1): attacks, charges, defends; it cannot heal ----------

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

// ---------- Guardian (Pluma, Player 2): attacks, defends and heals; it cannot charge ----------

// Q = {Idle, Attacking, Defending, Healing, Hurt, Victory, Defeated}, q0 = Idle, F = {Victory, Defeated}
public enum GuardianState { Idle, Attacking, Defending, Healing, Hurt, Victory, Defeated }

// Sigma = {Atk, Def, Heal (the move was played), Hit, Done, Hp0, Win}
public enum GuardianSymbol { Atk, Def, Heal, Hit, Done, Hp0, Win }

public static class GuardianAutomaton
{
    public static bool IsFinal(GuardianState state) => state == GuardianState.Victory || state == GuardianState.Defeated;

    public static GuardianState Step(GuardianState state, GuardianSymbol symbol)
    {
        switch (state)
        {
            case GuardianState.Idle:
                switch (symbol)
                {
                    case GuardianSymbol.Atk: return GuardianState.Attacking;
                    case GuardianSymbol.Def: return GuardianState.Defending;
                    case GuardianSymbol.Heal: return GuardianState.Healing;
                    case GuardianSymbol.Hit: return GuardianState.Hurt;
                    case GuardianSymbol.Hp0: return GuardianState.Defeated;
                    case GuardianSymbol.Win: return GuardianState.Victory;
                }
                break;

            case GuardianState.Attacking:
            case GuardianState.Defending:
                switch (symbol)
                {
                    case GuardianSymbol.Done: return GuardianState.Idle;
                    case GuardianSymbol.Hit: return GuardianState.Hurt;
                    case GuardianSymbol.Hp0: return GuardianState.Defeated;
                    case GuardianSymbol.Win: return GuardianState.Victory;
                }
                break;

            case GuardianState.Healing:
                switch (symbol)
                {
                    case GuardianSymbol.Done: return GuardianState.Idle;
                    case GuardianSymbol.Hit: return GuardianState.Hurt;   // hit while healing: the heal is lost
                    case GuardianSymbol.Hp0: return GuardianState.Defeated;
                    case GuardianSymbol.Win: return GuardianState.Victory;
                }
                break;

            case GuardianState.Hurt:
                switch (symbol)
                {
                    case GuardianSymbol.Done: return GuardianState.Idle;
                    case GuardianSymbol.Hit: return GuardianState.Hurt;
                    case GuardianSymbol.Hp0: return GuardianState.Defeated;
                    case GuardianSymbol.Win: return GuardianState.Victory;
                }
                break;
        }
        return state;
    }
}

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
