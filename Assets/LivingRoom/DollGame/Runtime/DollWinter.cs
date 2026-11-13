using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace TeamRoulette.Room3.DollMemory
{
    // Owns only the existing dog's animation and temporary movement during this memory.
    public sealed class DollWinter : MonoBehaviour
    {
        public Transform dog, mouth;
        public Animator animator;
        public RuntimeAnimatorController fallbackController;
        public Vector3 mouthOffset = new Vector3(0, -.015f, .09f);
        public Vector3 restOffset = new Vector3(0, .13f, .36f);
        NavMeshAgent agent;
        bool oldAgent, rootMotion, captured, oldAnimatorEnabled;
        AnimatorCullingMode oldCulling;
        Vector3 oldPosition; Quaternion oldRotation;
        RuntimeAnimatorController oldController; string playing; float oldSpeed;
        public bool IsSitting => animator && animator.GetCurrentAnimatorStateInfo(0).IsName("Arm_Spitz|Sitting_loop_1");
        public void Capture()
        {
            captured=true; oldPosition=dog.position; oldRotation=dog.rotation;
            oldController=animator.runtimeAnimatorController; rootMotion=animator.applyRootMotion;
            if(!oldController) animator.runtimeAnimatorController=fallbackController;
            oldAnimatorEnabled=animator.enabled; oldCulling=animator.cullingMode;
            animator.enabled=true; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; animator.applyRootMotion=false;
            oldSpeed=animator.speed; animator.speed=1; playing=null;
            if(!animator.HasState(0,Animator.StringToHash("Arm_Spitz|Sitting_loop_1")) && fallbackController) animator.runtimeAnimatorController=fallbackController;
            agent=dog.GetComponent<NavMeshAgent>(); oldAgent=agent && agent.enabled;
            if(agent) agent.enabled=false;
        }
        public void Play(string clip)
        {
            string state="Arm_Spitz|"+clip;
            if(playing==state && (animator.GetCurrentAnimatorStateInfo(0).IsName(state) || animator.IsInTransition(0))) return;
            if(animator.HasState(0,Animator.StringToHash(state))) { animator.CrossFadeInFixedTime(state,.15f); playing=state; }
        }
        public void Face(Vector3 point,float speed=8)
        {
            Vector3 d=point-dog.position; d.y=0;
            if(d.sqrMagnitude>.001f) dog.rotation=Quaternion.Slerp(dog.rotation,Quaternion.LookRotation(d),Time.deltaTime*speed);
        }
        public bool Step(Vector3 point,float speed,bool avoidObstacles)
        {
            Vector3 delta=point-dog.position; delta.y=0;
            if(delta.magnitude<.08f) return true;
            Face(point);
            Vector3 next=dog.position+delta.normalized*Mathf.Min(delta.magnitude,speed*Time.deltaTime);
            if(avoidObstacles)
            {
                var path=new NavMeshPath();
                if(NavMesh.SamplePosition(dog.position,out var from,.4f,NavMesh.AllAreas) && NavMesh.SamplePosition(point,out var to,.6f,NavMesh.AllAreas)
                    && NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete && path.corners.Length>1)
                    next=Vector3.MoveTowards(dog.position,path.corners[1],speed*Time.deltaTime);
                else if(Physics.SphereCast(dog.position+Vector3.up*.28f,.16f,delta.normalized,out var hit,Mathf.Min(.4f,delta.magnitude),~0,QueryTriggerInteraction.Ignore)
                    && !hit.transform.IsChildOf(dog)) return true;
            }
            dog.position=next; return false;
        }
        public IEnumerator Move(Vector3 point,float speed=1.4f,bool avoid=true)
        {
            Play(speed>1.5f?"Run_F_IP":"Walk_F_IP");
            float remaining=12;
            while(remaining>0 && !Step(point,speed,avoid)) { remaining-=Time.deltaTime; yield return null; }
            Play("Idle_1");
        }
        // Terrain following is used only by the authored outdoor return route.
        public bool GroundStep(Vector3 target,float speed,Collider[] surfaces,out bool blocked)
        {
            blocked=false;
            Vector3 delta=target-dog.position;delta.y=0;
            if(delta.magnitude<.025f)return true;
            Face(target);
            Vector3 next=dog.position+delta.normalized*Mathf.Min(delta.magnitude,speed*Time.deltaTime);
            var ray=new Ray(next+Vector3.up*.29f,Vector3.down);
            float ground=float.NegativeInfinity;
            foreach(var collider in surfaces)
                if(collider && collider.enabled && collider.gameObject.activeInHierarchy && !collider.isTrigger
                    && collider.Raycast(ray,out var hit,1.0f) && hit.normal.y>.6f)
                    ground=Mathf.Max(ground,hit.point.y);
            if(float.IsNegativeInfinity(ground) || ground+.025f-dog.position.y>.245f) {blocked=true;return false;}
            next.y=ground+.025f;dog.position=next;return false;
        }
        public IEnumerator Sit() { Play("Sitting_start"); yield return new WaitForSeconds(.7f); Play("Sitting_loop_1"); }
        public void HoldSitting()
        {
            if(!IsSitting && !animator.IsInTransition(0)) animator.Play("Arm_Spitz|Sitting_loop_1",0,0);
            animator.speed=1;
        }
        public void Carry(Transform doll)
        {
            doll.SetParent(mouth,true); doll.localPosition=mouthOffset; doll.localRotation=Quaternion.Euler(0,90,0);
        }
        public void Restore()
        {
            if(!captured) return; captured=false;
            dog.SetPositionAndRotation(oldPosition,oldRotation); animator.runtimeAnimatorController=oldController; animator.applyRootMotion=rootMotion;
            animator.enabled=oldAnimatorEnabled; animator.cullingMode=oldCulling;
            animator.speed=oldSpeed;
            if(agent) agent.enabled=oldAgent;
        }
    }
}

