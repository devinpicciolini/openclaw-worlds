using UnityEngine;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Forces a static NPC into the idle animation state.
    /// Without this, locomotion controllers default to T-pose because
    /// no script is driving the animator parameters.
    ///
    /// Uses a low-frequency check (every 2s) to avoid per-frame cost.
    /// </summary>
    public class NPCIdleAnimator : MonoBehaviour
    {
        Animator animator;
        float nextCheck;
        const float CheckInterval = 2f;

        void Start()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            animator.applyRootMotion = false;
            ForceIdle();
            nextCheck = Time.time + Random.Range(0.5f, CheckInterval);
        }

        void ForceIdle()
        {
            if (animator == null) return;
            animator.SetFloat(AnimHashes.MoveSpeed, 0f);
            animator.SetInteger(AnimHashes.CurrentGait, 0);
            animator.SetBool(AnimHashes.IsGrounded, true);
            animator.SetBool(AnimHashes.IsStopped, true);
            animator.SetBool(AnimHashes.IsWalking, false);
            animator.SetBool(AnimHashes.MovementInputHeld, false);
            animator.SetFloat(AnimHashes.StrafeDirectionZ, 0f);
            animator.SetFloat(AnimHashes.ForwardStrafe, 0f);
            animator.SetFloat(AnimHashes.FallingDuration, 0f);
            animator.SetBool(AnimHashes.IsJumping, false);
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckInterval;
            ForceIdle();
        }
    }
}
