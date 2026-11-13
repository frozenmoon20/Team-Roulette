using System.Collections;
using UnityEngine;
namespace TeamRoulette.Room3.DollMemory {
 public sealed class DollDoor:MonoBehaviour {
 public bool IsOpen{get;private set;} Quaternion closed; Vector3 pivot; bool captured;
 public IEnumerator Open(){
 if(IsOpen)yield break;
 if(!captured){closed=transform.rotation;pivot=transform.position;captured=true;}
 var start=transform.rotation;var end=Quaternion.AngleAxis(-100,Vector3.up)*closed;
 for(float t=0;t<.7f;t+=Time.deltaTime){transform.rotation=Quaternion.Slerp(start,end,Mathf.SmoothStep(0,1,t/.7f));yield return null;}transform.rotation=end;IsOpen=true;
 }
 public void Restore(){if(captured){transform.SetPositionAndRotation(pivot,closed);IsOpen=false;captured=false;}}
 }
}
