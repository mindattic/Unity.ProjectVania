using Unity.ProjectVania.Core.Settings;
using Unity.ProjectVania.Core.Simulation;
using UnityEngine;

namespace Unity.ProjectVania
{
    /// <summary>
    /// Owns the running game: the validated settings and the Core world, stepped from real time at the fixed
    /// rate. Created by <see cref="Bootstrap"/>, never placed in a scene by hand.
    /// </summary>
    public sealed class GameHost : MonoBehaviour
    {
        public GameSettings Settings { get; private set; }
        public GameWorld World { get; private set; }

        public void Initialize(GameSettings settings)
        {
            Settings = settings;
            World = new GameWorld();
        }

        void Update()
        {
            World?.Advance(Time.unscaledDeltaTime);
        }
    }
}
