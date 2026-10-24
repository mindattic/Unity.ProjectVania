using Unity.ProjectVania.Core.Settings;

namespace Unity.ProjectVania.Core.Characters
{
    /// <summary>
    /// A character's action changed over a step (commands and update together), so presentation can switch its
    /// animation and cues. <see cref="Character"/> is the index into <c>GameWorld.Characters</c>.
    /// </summary>
    public readonly struct CharacterActionChanged : Commands.IGameEvent
    {
        public long Tick { get; }
        public int Character { get; }
        public ActionKind From { get; }
        public ActionKind To { get; }

        public CharacterActionChanged(long tick, int character, ActionKind from, ActionKind to)
        {
            Tick = tick;
            Character = character;
            From = from;
            To = to;
        }
    }
}
