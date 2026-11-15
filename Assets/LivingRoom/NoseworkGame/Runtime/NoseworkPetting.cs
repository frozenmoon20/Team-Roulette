using System.Collections;
using System.Linq;
using UnityEngine;

namespace TeamRoulette.Room3.Nosework
{
    public sealed class NoseworkPetting : MonoBehaviour
    {
        public PlayerHand hand;
        public Transform head, earLeft, earRight;
        public bool IsPetting { get; private set; }
        public bool WasPetted { get; private set; }
        public bool DescendingContact { get; private set; }
        Camera view;
        NoseworkWinter actor;
        Vector3 handPosition;
        Quaternion handRotation, cameraRotation, animatedHead;
        bool handVisible, headApplied;
        float elapsed;
        public bool Hit(Ray ray)
        {
            return actor && actor.winter.GetComponentsInChildren<Renderer>().Any(r=>r.enabled && r.bounds.IntersectRay(ray));
        }
        public void Prepare(Camera camera, NoseworkWinter winter)
        { view=camera; actor=winter; WasPetted=false; }
        public IEnumerator Play()
        {
            if(!hand || !hand.handModel || !head) { Debug.LogError("쓰다듬기 손과 겨울이 머리 연결이 필요합니다.",this); yield break; }
            var model=hand.handModel.transform;
            handPosition=model.localPosition; handRotation=model.localRotation; handVisible=model.gameObject.activeSelf;
            cameraRotation=view.transform.localRotation;
            elapsed=0; IsPetting=true; model.gameObject.SetActive(true);
            actor.Play(actor.sitState);
            while(elapsed<2.6f) { yield return null; elapsed+=Time.deltaTime; }
            WasPetted=true; Restore();
        }
        void Update() { RestoreHead(); }
        void LateUpdate()
        {
            if(!IsPetting || !head || !hand || !hand.handModel || !view) return;
            float reach=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.35f));
            float stroke=Mathf.Sin((elapsed-.5f)*Mathf.PI*2);
            animatedHead=head.localRotation; headApplied=true;
            head.rotation=Quaternion.AngleAxis(reach*(5+4*stroke),actor.winter.forward)*head.rotation;
            Vector3 crown=earLeft && earRight ? (earLeft.position+earRight.position)*.5f : head.position+actor.winter.up*.12f;
            Quaternion rest=view.transform.parent ? view.transform.parent.rotation*cameraRotation:cameraRotation;
            view.transform.rotation=Quaternion.Slerp(rest,Quaternion.LookRotation(crown-view.transform.position),reach);
            var model=hand.handModel.transform;
            model.localPosition=handPosition; model.localRotation=handRotation;
            Transform palm=hand.palmPoint ? hand.palmPoint:hand.handBone;
            if(palm)
            {
                model.rotation=Quaternion.FromToRotation(palm.up,-actor.winter.up)*model.rotation;
                Vector3 high=crown+actor.winter.up*.08f;
                Vector3 low=crown-actor.winter.up*.08f+actor.winter.forward*.065f;
                Vector3 target;
                if(elapsed<.5f)
                {
                    DescendingContact=true;
                    target=Vector3.Lerp(crown+actor.winter.up*.28f,high,Mathf.SmoothStep(0,1,elapsed/.5f));
                }
                else if(elapsed<2.1f)
                {
                    float cycle=Mathf.Repeat(elapsed-.5f,.8f);
                    DescendingContact=cycle<.55f;
                    if(DescendingContact) target=Vector3.Lerp(high,low,Mathf.SmoothStep(0,1,cycle/.55f));
                    else
                    {
                        // Lift beside the head between strokes; contact always travels downwards.
                        float reset=(cycle-.55f)/.25f;
                        target=Vector3.Lerp(low,high,reset)+actor.winter.right*(Mathf.Sin(reset*Mathf.PI)*.16f);
                    }
                }
                else
                {
                    DescendingContact=false;
                    target=Vector3.Lerp(low,low+view.transform.right*.3f-actor.winter.up*.12f,Mathf.Clamp01((elapsed-2.1f)/.5f));
                }
                // Place the first visible frame above the head, avoiding an upward reach from the resting hand.
                model.position+=target-palm.position;
            }
        }
        void RestoreHead() { if(headApplied && head) head.localRotation=animatedHead; headApplied=false; }
        public void Restore()
        {
            if(!IsPetting) return;
            IsPetting=false; DescendingContact=false; RestoreHead();
            if(hand && hand.handModel) { hand.handModel.transform.localPosition=handPosition; hand.handModel.transform.localRotation=handRotation; hand.handModel.SetActive(handVisible); }
            if(view) view.transform.localRotation=cameraRotation;
        }
        void OnDisable() { Restore(); }
    }
}
