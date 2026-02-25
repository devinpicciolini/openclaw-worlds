using UnityEngine;
using OpenClawWorlds;

namespace OpenClawWorlds.World
{
    /// <summary>
    /// Makes an NPC or animal wander randomly within a defined area.
    /// Uses direct transform movement (no CharacterController needed).
    /// </summary>
    public class WanderingNPC : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 1.4f;
        [SerializeField] float wanderRadius = 15f;
        [SerializeField] float waitTimeMin = 2f;
        [SerializeField] float waitTimeMax = 6f;
        [SerializeField] float groundY = 0.0f;

        Vector3 origin;
        Vector3 target;
        float waitTimer;
        bool waiting;

        Animator animator;

        public void Init(float speed, float radius, float minWait = 2f, float maxWait = 6f)
        {
            moveSpeed = speed;
            wanderRadius = radius;
            waitTimeMin = minWait;
            waitTimeMax = maxWait;
        }

        void Start()
        {
            origin = transform.position;
            groundY = origin.y;
            animator = GetComponentInChildren<Animator>();
            if (animator) animator.applyRootMotion = false;
            PickNewTarget();
            waiting = true;
            waitTimer = Random.Range(0f, waitTimeMax);
            SetAnim(0f, true);
        }

        void Update()
        {
            if (waiting)
            {
                SetAnim(0f, true);
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    waiting = false;
                    PickNewTarget();
                }
                return;
            }

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;

            if (dist < 0.5f)
            {
                waiting = true;
                waitTimer = Random.Range(waitTimeMin, waitTimeMax);
                SetAnim(0f, true);
                return;
            }

            dir.Normalize();
            transform.position += dir * moveSpeed * Time.deltaTime;
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);

            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 5f * Time.deltaTime);

            SetAnim(moveSpeed, false);
        }

        void SetAnim(float speed, bool stopped)
        {
            if (animator == null) return;
            animator.SetFloat(AnimHashes.MoveSpeed, speed);
            animator.SetInteger(AnimHashes.CurrentGait, stopped ? 0 : 1);
            animator.SetBool(AnimHashes.IsGrounded, true);
            animator.SetBool(AnimHashes.IsStopped, stopped);
            animator.SetBool(AnimHashes.IsWalking, !stopped);
            animator.SetBool(AnimHashes.MovementInputHeld, !stopped);
            animator.SetFloat(AnimHashes.StrafeDirectionZ, 1f);
            animator.SetFloat(AnimHashes.ForwardStrafe, 1f);
        }

        void PickNewTarget()
        {
            Vector2 rnd = Random.insideUnitCircle * wanderRadius;
            target = origin + new Vector3(rnd.x, 0f, rnd.y);
            target.y = groundY;
        }
    }
}
