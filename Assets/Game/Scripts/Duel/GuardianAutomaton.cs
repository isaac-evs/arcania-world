// See FiniteAutomaton.cs for how the automata of the duel are defined and used.

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
