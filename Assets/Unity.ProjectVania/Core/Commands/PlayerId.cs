using System;

namespace Unity.ProjectVania.Core.Commands
{
    /// <summary>
    /// Who issued a command. The original had one global current player; the rebuild seam is that
    /// every command names its player and nothing reads a current-player singleton.
    /// </summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public int Value { get; }
        public PlayerId(int value) { Value = value; }

        /// <summary>The first spawned player (PlayerIndex 0 in the original).</summary>
        public static PlayerId One => new PlayerId(0);

        public bool Equals(PlayerId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is PlayerId p && Equals(p);
        public override int GetHashCode() => Value;
        public override string ToString() => "Player " + Value;
        public static bool operator ==(PlayerId a, PlayerId b) => a.Value == b.Value;
        public static bool operator !=(PlayerId a, PlayerId b) => a.Value != b.Value;
    }
}
