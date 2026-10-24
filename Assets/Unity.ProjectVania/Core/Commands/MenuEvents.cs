namespace Unity.ProjectVania.Core.Commands
{
    /// <summary>
    /// A sound cue to play (<c>Audio.PlayAmbience</c> / <c>PlaySfx</c>): presentation looks <see cref="Cue"/> up in
    /// audio.json's cue table and plays it. The world names a cue only while the game is not muted (<see cref="Simulation.GameWorld.Muted"/>).
    /// </summary>
    public readonly struct CueRequested : IGameEvent
    {
        public long Tick { get; }
        public string Cue { get; }
        public CueRequested(long tick, string cue) { Tick = tick; Cue = cue; }
    }

    /// <summary>The menu's Quit was selected (<c>Game.Exit()</c>): presentation ends the program.</summary>
    public readonly struct QuitRequested : IGameEvent
    {
        public long Tick { get; }
        public QuitRequested(long tick) { Tick = tick; }
    }
}
