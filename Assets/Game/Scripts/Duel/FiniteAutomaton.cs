using System;

// The three finite automata of the duel (not in the Python game). Each one is M = (Q, Sigma, delta, q0, F):
//   Q      the enum of states        Sigma  the enum of symbols        q0  the first value of the state enum (0)
//   delta  the Step function of each automaton (a `switch` on the state), in its own script          F   the states that end the automaton (IsFinal)
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
