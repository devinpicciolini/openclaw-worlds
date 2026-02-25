using UnityEngine;

namespace OpenClawWorlds
{
    /// <summary>
    /// Shared Animator parameter hashes. Compatible with Synty locomotion controllers.
    /// </summary>
    public static class AnimHashes
    {
        public static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
        public static readonly int CurrentGait = Animator.StringToHash("CurrentGait");
        public static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
        public static readonly int IsJumping = Animator.StringToHash("IsJumping");
        public static readonly int IsStopped = Animator.StringToHash("IsStopped");
        public static readonly int IsWalking = Animator.StringToHash("IsWalking");
        public static readonly int FallingDuration = Animator.StringToHash("FallingDuration");
        public static readonly int StrafeDirectionZ = Animator.StringToHash("StrafeDirectionZ");
        public static readonly int ForwardStrafe = Animator.StringToHash("ForwardStrafe");
        public static readonly int MovementInputHeld = Animator.StringToHash("MovementInputHeld");
        public static readonly int MovementInputPressed = Animator.StringToHash("MovementInputPressed");
    }
}
