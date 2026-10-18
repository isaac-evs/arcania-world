using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The duel itself (not in the Python game): Striker (Charmadillo, Player 1 = host) against Guardian (Pluma, Player 2 = guest),
// with Jacana as the referee. Each round both players secretly choose a move; when both are in, the host (who decides everything)
// resolves it and tells the guest what to feed to each automaton, so both screens always show the same states.
//   Attack beats Charge, Defend beats Attack, Charge beats Defend, Heal restores health unless the other one attacks.
// The Animator Controllers come from Resources/Duel (made by Arcania > Build Duel Animators). Created by DuelChallenge.
public class DuelGame : MonoBehaviour
{
    const float WindowWidth = 1280f, WindowHeight = 720f;
    const int MaxHealth = 20;
    const float StrikerX = 420f, GuardianX = 980f;   // where the fighters stand

    public enum Move { Attack, Charge, Defend, Heal }

    static readonly Color Purple = UiKit.Hex("#2e1b53"), Ground = UiKit.Hex("#4a3578"), Pale = UiKit.Hex("#b3c9d9");

    // ---- the automata of the three actors, each tied to an Animator ----

    abstract class Actor
    {
        public Animator animator;
        public abstract void Feed(string symbol);
        public abstract string StateName { get; }
        public abstract bool IsFinal { get; }
    }

    class Actor<TState, TSymbol> : Actor where TState : struct, Enum where TSymbol : struct, Enum
    {
        readonly FiniteAutomaton<TState, TSymbol> automaton;

        public Actor(Func<TState, TSymbol, TState> step, Func<TState, bool> final)
        {
            automaton = new FiniteAutomaton<TState, TSymbol>(step, final);
        }

        // The automaton moves, and the Animator is told which state to be in
        public override void Feed(string symbol)
        {
            if (!Enum.TryParse(symbol, out TSymbol parsed)) return;
            automaton.Feed(parsed);
            if (animator != null) animator.SetInteger("State", Convert.ToInt32(automaton.Current));
        }

        public override string StateName => automaton.Current.ToString();
        public override bool IsFinal => automaton.IsFinal;
    }

    NetworkSession session;
    Font regularFont;
    int role;                 // 1 = Striker (host), 2 = Guardian (guest)
    bool IsHost => role == 1;

    GameObject canvasObject;
    RectTransform content, hud;
    Actor striker, guardian, referee;

    int health1 = MaxHealth, health2 = MaxHealth;
    int round = 1;
    string message = "";
    int result = -1;          // -1 = going on, 0 = draw, 1 or 2 = that player won
    bool locked;              // this player already chose this round
    bool endMusicPlayed;
    string drawn = "";

    // the host's bookkeeping
    Move? pick1, pick2;

    public bool Finished => result >= 0;

    public static Move[] MovesFor(int role)
    {
        return role == 1 ? new[] { Move.Attack, Move.Charge, Move.Defend } : new[] { Move.Attack, Move.Defend, Move.Heal };
    }

    public void Begin(int playerRole, NetworkSession networkSession, Font font)
    {
        role = playerRole;
        session = networkSession;
        regularFont = font;
        health1 = health2 = MaxHealth;
        round = 1;
        result = -1;
        locked = false;
        endMusicPlayed = false;
        pick1 = pick2 = null;
        message = "";
        drawn = "";

        session.MessageReceived += OnMessage;
        BuildCanvas();
        Refresh();
    }

    public void Close()
    {
        if (session != null) session.MessageReceived -= OnMessage;
        StopAllCoroutines();
        if (canvasObject != null) Destroy(canvasObject);
        canvasObject = null;
    }

    void OnDestroy()
    {
        Close();
    }

    // ---- the picture ----

    void BuildCanvas()
    {
        canvasObject = new GameObject("Duel");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 670;   // over the challenge prompt (660), under the network menu (700)
        canvas.pixelPerfect = true;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(WindowWidth, WindowHeight);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        content = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(canvasObject.transform, false);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = new Vector2(WindowWidth, WindowHeight);

        UiKit.Box(content, -640, -360, WindowWidth * 2, WindowHeight * 2, Purple);   // covers any window shape
        var background = Resources.Load<Texture2D>("Duel/arena_background");       // a copy of graphics/backgrounds/arena.jpeg
        if (background != null)
        {
            // the picture covers the whole screen whatever its shape: it is cropped, never stretched
            var picture = new GameObject("Background", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(content, false);
            picture.texture = background;
            picture.raycastTarget = false;
            var pictureRect = picture.rectTransform;
            pictureRect.anchorMin = pictureRect.anchorMax = new Vector2(0.5f, 0.5f);
            pictureRect.sizeDelta = new Vector2(WindowWidth, WindowHeight);
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.width / (float)background.height;
        }
        else UiKit.Box(content, 0, 500, WindowWidth, 220, Ground);

        striker = new Actor<StrikerState, StrikerSymbol>(StrikerAutomaton.Step, StrikerAutomaton.IsFinal);
        guardian = new Actor<GuardianState, GuardianSymbol>(GuardianAutomaton.Step, GuardianAutomaton.IsFinal);
        referee = new Actor<RefereeState, RefereeSymbol>(RefereeAutomaton.Step, RefereeAutomaton.IsFinal);

        // the sheets face left, like the player's monsters in a fight: the striker on the left is flipped
        striker.animator = NewActor("Striker", "Striker", StrikerX, 330, 384, flip: true);
        guardian.animator = NewActor("Guardian", "Guardian", GuardianX, 330, 384, flip: false);
        referee.animator = NewActor("Referee", "Referee", 640, 250, 288, flip: false);

        hud = UiKit.NewRect(content, "Hud", 0, 0, WindowWidth, WindowHeight);
    }

    // A holder (flipped if needed) with the Animator and the picture inside it, so the animation never fights the flip
    Animator NewActor(string name, string controllerName, float cx, float cy, float size, bool flip)
    {
        var holder = UiKit.NewRect(content, name, cx - size / 2f, cy - size / 2f, size, size);
        holder.localScale = new Vector3(flip ? -1 : 1, 1, 1);

        var visual = new GameObject("Visual", typeof(RectTransform));
        var rect = (RectTransform)visual.transform;
        rect.SetParent(holder, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;

        var image = visual.AddComponent<Image>();
        image.raycastTarget = false;

        var controller = Resources.Load<RuntimeAnimatorController>("Duel/" + controllerName);
        if (controller == null)
        {
            Debug.LogError($"Resources/Duel/{controllerName}.controller is missing: run the menu Arcania > Build Duel Animators in the Unity editor.");
            return null;
        }
        var animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        return animator;
    }

    void Update()
    {
        if (canvasObject == null) return; // no duel going on

        if (result >= 0 && !endMusicPlayed)
        {
            endMusicPlayed = true;
            AudioManager.PlayVictory("arena_battle_end");
        }
        var keyboard = Keyboard.current;
        if (keyboard != null && result < 0 && !locked)
        {
            var moves = MovesFor(role);
            if (keyboard.digit1Key.wasPressedThisFrame) Choose(moves[0]);
            else if (keyboard.digit2Key.wasPressedThisFrame) Choose(moves[1]);
            else if (keyboard.digit3Key.wasPressedThisFrame) Choose(moves[2]);
        }
        Refresh();
    }

    // Redrawn only when something shown changed
    void Refresh()
    {
        if (hud == null) return;
        string signature = $"{health1}|{health2}|{round}|{message}|{result}|{locked}|{striker.StateName}|{guardian.StateName}|{referee.StateName}";
        if (signature == drawn) return;
        drawn = signature;

        for (int i = hud.childCount - 1; i >= 0; i--) Destroy(hud.GetChild(i).gameObject);

        var big = new UiFont(regularFont, 30, 38);
        var small = new UiFont(regularFont, 18, 23);

        Bubble(big, "Arena Duel  -  Round " + round, 20);

        FighterPanel(small, "Player 1 - Striker (Charmadillo)", health1, 60, 80, striker.StateName, StrikerX, 540, role == 1);
        FighterPanel(small, "Player 2 - Guardian (Pluma)", health2, 740, 80, guardian.StateName, GuardianX, 540, role == 2);

        float refereeWidth = UiKit.Measure(small, "Referee Jacana: " + referee.StateName) + 24;
        UiKit.Box(hud, 640 - refereeWidth / 2f, 402, refereeWidth, small.surfaceHeight + 12, new Color(Purple.r, Purple.g, Purple.b, 0.8f), 4);
        UiKit.Label(hud, "Referee Jacana: " + referee.StateName, small, Color.white, Anchor.MidTop, 640, 408);
        Bubble(big, RefereeLine(), 438);

        string bottom;
        if (result >= 0) bottom = "Space or Escape: leave the arena";
        else if (locked) bottom = "Move locked. Waiting for the other player...";
        else
        {
            var moves = MovesFor(role);
            bottom = $"1 {moves[0]}     2 {moves[1]}     3 {moves[2]}     (Escape: leave)";
        }
        Bubble(big, bottom, 640);
    }

    void Bubble(UiFont font, string text, float y)
    {
        float width = UiKit.Measure(font, text) + 28;
        UiKit.Box(hud, (WindowWidth - width) / 2f, y, width, font.surfaceHeight + 28, Color.white, 4);
        UiKit.Label(hud, text, font, Color.black, Anchor.MidTop, WindowWidth / 2f, y + 14);
    }

    void FighterPanel(UiFont small, string title, int health, float x, float y, string state, float labelX, float labelY, bool mine)
    {
        const float barWidth = 480;
        UiKit.Box(hud, x - 12, y - 8, barWidth + 24, 90, new Color(Purple.r, Purple.g, Purple.b, 0.8f), 4);
        float stateWidth = UiKit.Measure(small, "State: " + state) + 24;
        UiKit.Box(hud, labelX - stateWidth / 2f, labelY - 6, stateWidth, small.surfaceHeight + 12, new Color(Purple.r, Purple.g, Purple.b, 0.8f), 4);
        UiKit.Label(hud, title + (mine ? "  (you)" : ""), small, Color.white, Anchor.TopLeft, x, y);
        UiKit.Bar(hud, x, y + 30, barWidth, 22, health, MaxHealth, UiKit.Hex("#64a990"), Pale, 2);
        UiKit.Label(hud, $"HP {Mathf.Max(0, health)}/{MaxHealth}", small, Color.white, Anchor.TopLeft, x, y + 58);
        UiKit.Label(hud, "State: " + state, small, Pale, Anchor.MidTop, labelX, labelY);
    }

    string RefereeLine()
    {
        if (result == 0) return "Draw! Nobody stands.";
        if (result > 0) return $"Player {result} wins the duel!";

        switch (referee.StateName)
        {
            case "Waiting": return "Choose your move!";
            case "Watching": return "One player is ready...";
            case "Judging": return "Moves locked. Let's see!";
            default: return message;
        }
    }

    // ---- choosing ----

    void Choose(Move move)
    {
        if (locked || result >= 0) return;
        if (referee.StateName != "Waiting" && referee.StateName != "Watching") return;
        locked = true;

        if (IsHost) HostPick(1, move);
        else session.Send("PICK|" + move);
    }

    // Host only: a player's choice arrived
    void HostPick(int player, Move move)
    {
        if (result >= 0) return;
        if (Array.IndexOf(MovesFor(player), move) < 0) return;           // not one of this fighter's moves
        if ((player == 1 && pick1.HasValue) || (player == 2 && pick2.HasValue)) return;

        if (player == 1) pick1 = move; else pick2 = move;

        if (pick1.HasValue && pick2.HasValue)
        {
            Broadcast(0, "Both");
            StartCoroutine(Resolve(pick1.Value, pick2.Value));
        }
        else Broadcast(0, "Pick");
    }

    // ---- the round (host) ----

    IEnumerator Resolve(Move moveStriker, Move moveGuardian)
    {
        yield return new WaitForSeconds(0.9f);

        // the moves are shown
        Broadcast(1, Symbol(moveStriker));
        Broadcast(2, Symbol(moveGuardian));
        Outcome(moveStriker, moveGuardian, out int damage1, out int damage2, out int heal1, out int heal2, out string text);
        Say(text);
        yield return new WaitForSeconds(1.0f);

        // the damage is dealt: the condition "health <= 0" becomes the symbol Hp0
        health1 = Mathf.Clamp(health1 - damage1 + heal1, 0, MaxHealth);
        health2 = Mathf.Clamp(health2 - damage2 + heal2, 0, MaxHealth);
        session.Send($"HP|{health1}|{health2}");
        if (damage1 > 0) Broadcast(1, "Hit");
        if (damage2 > 0) Broadcast(2, "Hit");
        yield return new WaitForSeconds(0.9f);

        bool dead1 = health1 <= 0, dead2 = health2 <= 0;
        if (dead1 || dead2)
        {
            if (dead1) Broadcast(1, "Hp0"); else Broadcast(1, "Win");
            if (dead2) Broadcast(2, "Hp0"); else Broadcast(2, "Win");
            Broadcast(0, "Rule");
            yield return new WaitForSeconds(1.0f);

            int winner = dead1 && dead2 ? 0 : (dead1 ? 2 : 1);
            Broadcast(0, "End");
            session.Send("END|" + winner);
            result = winner;
            yield break;
        }

        Broadcast(0, "Rule");
        yield return new WaitForSeconds(1.4f);

        Broadcast(1, "Done");
        Broadcast(2, "Done");
        pick1 = pick2 = null;
        session.Send("ROUND|" + (round + 1));
        round++;
        Broadcast(0, "Next");
    }

    static string Symbol(Move move)
    {
        switch (move)
        {
            case Move.Attack: return "Atk";
            case Move.Charge: return "Chg";
            case Move.Defend: return "Def";
            default: return "Heal";
        }
    }

    // What the two moves do. The striker plays Attack, Charge or Defend; the guardian Attack, Defend or Heal.
    static void Outcome(Move s, Move g, out int damageStriker, out int damageGuardian, out int healStriker, out int healGuardian, out string text)
    {
        damageStriker = damageGuardian = healStriker = healGuardian = 0;
        switch (s)
        {
            case Move.Attack:
                if (g == Move.Attack) { damageStriker = 3; damageGuardian = 3; text = "Both attack and both are hit!"; }
                else if (g == Move.Defend) { damageStriker = 2; text = "The guardian blocks and counters!"; }
                else { damageGuardian = 5; text = "The attack stops the heal!"; }
                break;
            case Move.Charge:
                if (g == Move.Attack) { damageStriker = 4; text = "The attack interrupts the charge!"; }
                else if (g == Move.Defend) { damageGuardian = 4; text = "The charge breaks the guard!"; }
                else { healGuardian = 3; text = "The charge is wasted. The guardian heals."; }
                break;
            default: // Defend
                if (g == Move.Attack) { damageGuardian = 2; text = "The striker blocks and counters!"; }
                else if (g == Move.Defend) text = "Both guard. Nothing happens.";
                else { healGuardian = 3; text = "The guardian heals in peace."; }
                break;
        }
    }

    // ---- telling both screens ----

    // target: 0 the referee, 1 the striker, 2 the guardian
    void Broadcast(int target, string symbol)
    {
        Apply(target, symbol);
        session.Send($"SYM|{target}|{symbol}");
    }

    void Say(string text)
    {
        message = text;
        session.Send("MSG|" + text);
    }

    void Apply(int target, string symbol)
    {
        var actor = target == 0 ? referee : (target == 1 ? striker : guardian);
        actor.Feed(symbol);
        if (target == 0 && symbol == "Next") locked = false;
    }

    void OnMessage(string line)
    {
        var parts = line.Split('|');
        switch (parts[0])
        {
            case "PICK":
                if (IsHost && parts.Length > 1 && Enum.TryParse(parts[1], out Move move)) HostPick(2, move);
                break;
            case "SYM":
                if (!IsHost && parts.Length > 2 && int.TryParse(parts[1], out int target) && target >= 0 && target <= 2) Apply(target, parts[2]);
                break;
            case "HP":
                if (parts.Length > 2) { int.TryParse(parts[1], out health1); int.TryParse(parts[2], out health2); }
                break;
            case "MSG":
                if (parts.Length > 1) message = parts[1];
                break;
            case "ROUND":
                if (parts.Length > 1) int.TryParse(parts[1], out round);
                break;
            case "END":
                if (parts.Length > 1) int.TryParse(parts[1], out result);
                break;
        }
    }
}
