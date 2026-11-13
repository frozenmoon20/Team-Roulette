using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace TeamRoulette.Room3.Nosework
{
    // Animation names and the extraction movement can be replaced independently of game rules.
    public sealed class NoseworkWinter : MonoBehaviour
    {
        public Transform winter;
        public Animator animator;
        public RuntimeAnimatorController controller;
        public string idleState = "Arm_Spitz|Idle_1", walkState = "Arm_Spitz|Walk_F_IP";
        public string sniffState = "Arm_Spitz|Crouch_Idle_loop_1", pickState = "Arm_Spitz|Pick_up";
        public string happyState = "Arm_Spitz|Bark";
        public string sitStartState = "Arm_Spitz|Sitting_start", sitState = "Arm_Spitz|Sitting_loop_1", standState = "Arm_Spitz|Sitting_end";
        public bool IsSitting { get; private set; }
        public Vector3 WaitingPosition { get; private set; }
        public int StandCount { get; private set; }
        public int DirectApproachCount { get; private set; }
        public Vector3 PlayerSide => -side;
        public Vector3 PettingPosition { get; private set; }
        public float PettingClearance { get; private set; }
        public float moveSpeed = .8f, standOff = .42f, sniffDuration = .7f, biteDelay = .65f;
        Vector3 savedPosition, side;
        Quaternion savedRotation;
        RuntimeAnimatorController savedController;
        bool savedRootMotion, savedAnimatorEnabled, savedAgentEnabled, captured;
        NavMeshAgent agent;
        Bounds board;
        public void Capture(Bounds bounds, Vector3 cameraSide)
        {
            board = bounds; side = -cameraSide.normalized;
            savedPosition = winter.position; savedRotation = winter.rotation;
            agent = winter.GetComponent<NavMeshAgent>(); savedAgentEnabled = agent && agent.enabled;
            if (agent) agent.enabled = false;
            if (animator)
            {
                savedAnimatorEnabled = animator.enabled; savedController = animator.runtimeAnimatorController;
                savedRootMotion = animator.applyRootMotion; animator.enabled = true;
                if (controller) animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
            }
            captured = true;
            StandCount=0; DirectApproachCount=0;
            WaitingPosition = new Vector3(board.center.x, board.min.y, board.center.z) + side * (board.extents.magnitude + .5f);
            winter.position=WaitingPosition;
            winter.rotation=Quaternion.LookRotation(-side);
            IsSitting=true; Play(sitState);
        }
        public void Play(string state)
        {
            if (animator && animator.runtimeAnimatorController && animator.HasState(0, Animator.StringToHash(state)))
                animator.CrossFadeInFixedTime(state, .15f);
        }
        IEnumerator Face(Vector3 target, float duration = .3f)
        {
            var dir = target - winter.position; dir.y = 0;
            if (dir.sqrMagnitude < .0001f) yield break;
            var from = winter.rotation; var to = Quaternion.LookRotation(dir);
            for (float t = 0; t < duration; t += Time.deltaTime)
            { winter.rotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / duration)); yield return null; }
            winter.rotation = to;
        }
        IEnumerator Move(Vector3 target)
        {
            yield return Face(target); Play(walkState);
            while ((winter.position - target).sqrMagnitude > .0001f)
            { winter.position = Vector3.MoveTowards(winter.position, target, moveSpeed * Time.deltaTime); yield return null; }
            Play(idleState);
        }
        public IEnumerator Extract(NoseworkCarrot carrot)
        {
            var point = carrot.transform.position;
            yield return Stand();
            DirectApproachCount++;
            // Move straight from the previous carrot. Only round boundaries return to the seat.
            var approach = point + side * standOff; approach.y = winter.position.y;
            yield return Move(approach); yield return Face(point);
            Play(sniffState); yield return new WaitForSeconds(sniffDuration);
            Play(pickState); yield return new WaitForSeconds(biteDelay);
            yield return carrot.Pull(.32f, .65f);
            Play(idleState); yield return new WaitForSeconds(.25f);
        }
        public IEnumerator Mistake(Vector3 playerPosition)
        { Play(IsSitting?sitState:idleState); yield return Face(playerPosition); yield return new WaitForSeconds(.7f); }
        public IEnumerator Celebrate() { Play(happyState); yield return new WaitForSeconds(1.4f); }
        IEnumerator Stand()
        {
            if(!IsSitting) yield break;
            Play(standState); yield return new WaitForSeconds(.65f); IsSitting=false; StandCount++;
        }
        IEnumerator Sit()
        {
            Play(sitStartState); yield return new WaitForSeconds(.7f); Play(sitState); IsSitting=true;
        }
        public IEnumerator ReturnToSeat()
        {
            if(IsSitting && Vector3.Distance(winter.position,WaitingPosition)<.01f) { Play(sitState); yield break; }
            yield return Stand(); yield return Move(WaitingPosition); yield return Face(board.center); yield return Sit();
        }
        public IEnumerator ApproachForPetting(Transform player)
        {
            yield return Stand();
            var direction=PlayerSide;
            yield return Face(board.center+direction*4);
            float matReach=ProjectedExtent(board.extents,direction);
            float rearReach=.4f;
            foreach(var renderer in winter.GetComponentsInChildren<Renderer>())
                if(renderer.enabled) rearReach=Mathf.Max(rearReach,Vector3.Dot(winter.position-renderer.bounds.center,direction)+ProjectedExtent(renderer.bounds.extents,direction));
            // Include paws, hips and tail, rather than positioning only winter's root past the edge.
            Vector3 point=board.center+direction*(matReach+rearReach+.3f); point.y=WaitingPosition.y;
            PettingPosition=point;
            yield return Move(point); yield return Face(board.center+direction*4); yield return Sit();
            PettingClearance=float.PositiveInfinity;
            foreach(var renderer in winter.GetComponentsInChildren<Renderer>())
                if(renderer.enabled) PettingClearance=Mathf.Min(PettingClearance,Vector3.Dot(renderer.bounds.center-board.center,direction)-ProjectedExtent(renderer.bounds.extents,direction)-matReach);
        }
        static float ProjectedExtent(Vector3 extents,Vector3 axis) => Mathf.Abs(axis.x)*extents.x+Mathf.Abs(axis.y)*extents.y+Mathf.Abs(axis.z)*extents.z;
        public void Restore()
        {
            if (!captured) return; captured = false;
            IsSitting=false;
            if(winter) winter.SetPositionAndRotation(savedPosition, savedRotation);
            if (animator) { animator.runtimeAnimatorController = savedController; animator.applyRootMotion = savedRootMotion; animator.enabled = savedAnimatorEnabled; }
            if (agent) agent.enabled = savedAgentEnabled;
        }
    }
}
