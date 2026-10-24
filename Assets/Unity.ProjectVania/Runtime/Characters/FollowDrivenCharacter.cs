using Unity.ProjectVania.Core.Commands;
using Unity.ProjectVania.Core.Simulation;
using UnityEngine;

namespace Unity.ProjectVania
{
    /// <summary>
    /// A stand-in until the camera's own story (U.VAN-US-A9) builds the original's modes and ease: keeps the game camera
    /// centred on the character player one drives, so [ and ] visibly move it from one to the other. A9 replaces this
    /// with the Loose, Lock, cursor and free modes of RECONSTRUCTION 7.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class FollowDrivenCharacter : MonoBehaviour
    {
        GameWorld _world;
        PlayerId _player;
        float _pixelsPerUnit;

        public void Initialize(GameWorld world, PlayerId player, float pixelsPerUnit)
        {
            _world = world;
            _player = player;
            _pixelsPerUnit = pixelsPerUnit;
            Follow();
        }

        void LateUpdate()
        {
            Follow();
        }

        public void Follow()
        {
            var character = _world?.Driven(_player);
            if (character == null)
            {
                return;
            }

            var at = CharacterView.ToUnity(character.PositionAt((float)_world.Alpha), _pixelsPerUnit);
            transform.position = new Vector3(at.x, at.y, transform.position.z);
        }
    }
}
