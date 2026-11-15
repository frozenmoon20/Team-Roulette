using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MealPurchasedSwap
{
    const string Folder = "Assets/LivingRoom/MealGame";
    [InitializeOnLoadMethod]
    static void ResumeRequestedTransfer()
    {
        EditorApplication.delayCall += () => {
            string request=Folder+"/Transfer.pending";
            if(!File.Exists(request))return;
            File.Delete(request);
            try{Transfer();}catch(Exception e){Debug.LogException(e);}
        };
    }
    [MenuItem("Tools/MealGame/5 - Transfer to purchased assets")]
    static void Transfer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var g = Object.FindFirstObjectByType<MealGameController>(FindObjectsInactive.Include);
        if (!g) throw new Exception("No MealGame.");
        var scene = g.gameObject.scene;
        var all = scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).ToArray();
        Func<string,Transform> find = name => {
            var matches=all.Where(t=>t.name==name && !t.GetComponentsInParent<Transform>(true).Skip(1).Any(p=>p.name==name || p.name=="JamToast" || (name=="Glas" && p.name=="Glas_Juice"))).ToArray();
            if(matches.Length!=1) throw new Exception(name+": expected one placed object, found "+matches.Length);
            return matches[0];
        };
        string[] names={"Toast_Sandwich_Version01","Jam01","MapleSyrup","Toast_Sandwich_Version03","JamToast","PlateBigBreakfast","Glas","Glas_Juice","Toster_Prefab","Fridge","Kitcabinet_low_1½x_A (2)","Kitchen_table_large"};
        var assets=names.ToDictionary(n=>n,find); // Preflight before any scene change.
        if(g.transform.Find("PurchasedItems")){Finish(g,assets);return;}
        EditorSceneManager.SaveScene(scene);
        string backup=Folder+"/BeforePurchasedSwap.unity.bak";
        if(!File.Exists(backup)) File.Copy(scene.path,backup);
        var items=New("PurchasedItems",g.transform);
        var legacy=New("LegacyObjects",g.transform);
        var oldTargets=g.GetComponentsInChildren<MealTarget>(true).ToArray();
        foreach(var t in oldTargets)
        {
            Undo.RecordObject(t,"Disable legacy MealGame interaction");t.enabled=false;
            if(t.transform==g.transform)continue;
            if(!t.transform.IsChildOf(legacy))Undo.SetTransformParent(t.transform,legacy,"Archive MealGame placeholder");
        }
        legacy.gameObject.SetActive(false);
        var bake=Object.Instantiate(assets["Toast_Sandwich_Version01"].gameObject,items);
        bake.name="ToastInToaster_Purchased";
        Undo.RegisterCreatedObjectUndo(bake,"Create baking representation");
        bake.transform.SetPositionAndRotation(assets[names[0]].position,assets[names[0]].rotation);
        bake.transform.localScale=assets[names[0]].lossyScale;
        g.bakingToast=bake; Target(bake,g,MealAction.Toaster);
        g.rawToast=Item(assets[names[0]],items,g,MealAction.RawToast);
        g.jam=Item(assets[names[1]],items,g,MealAction.Jam);
        g.juice=Item(assets[names[2]],items,g,MealAction.Juice);
        g.cookedToast=Item(assets[names[3]],items,g,MealAction.CookedToast);
        g.plate=Item(assets[names[5]],items,g,MealAction.Plate);
        Undo.SetTransformParent(g.toastOnPlatePoint,g.plate.transform,"Move toast placement to purchased plate");
        var empty=Wrap(assets[names[6]],items);g.emptyCup=empty.gameObject;Target(empty.gameObject,g,MealAction.EmptyCup);
        g.juiceCup=Item(assets[names[7]],items,g,MealAction.JuiceCup);
        var jt=Wrap(assets[names[4]],items);g.jamToast=jt.gameObject;Target(jt.gameObject,g,MealAction.JamToast);
        jt.SetParent(g.plate.transform,true); // Preserve the user's plated toast pose.
        foreach(var pair in new[]{(names[8],MealAction.Toaster),(names[9],MealAction.Fridge),(names[10],MealAction.Counter),(names[11],MealAction.Table)})
            Target(assets[pair.Item1].gameObject,g,pair.Item2);
        Finish(g,assets);
    }
    static void Finish(MealGameController g,System.Collections.Generic.Dictionary<string,Transform> assets)
    {
        var scene=g.gameObject.scene;
        var toaster=assets["Toster_Prefab"];
        var audio=toaster.GetComponent<AudioSource>(); if(!audio)audio=Undo.AddComponent<AudioSource>(toaster.gameObject);
        audio.playOnAwake=false;audio.spatialBlend=1;audio.minDistance=2;audio.maxDistance=25;g.toasterAudio=audio;
        Pose(g.cookedPopPoint,g.cookedToast.transform.position,Quaternion.identity);
        var jb=MealGeometry.BoundsOf(g.jam.gameObject);
        Pose(g.jamPoint,new Vector3(jb.center.x,jb.min.y,jb.center.z),Quaternion.identity);
        var pb=MealGeometry.BoundsOf(g.plate.gameObject);
        var flat=FlatRotation(g.cookedToast.gameObject);
        g.cookedToast.GetComponent<MealGeometry>().heldEuler=flat.eulerAngles;
        g.rawToast.GetComponent<MealGeometry>().heldEuler=FlatRotation(g.rawToast.gameObject).eulerAngles;
        Pose(g.toastOnPlatePoint,new Vector3(pb.center.x,pb.max.y+.002f,pb.center.z),flat);
        var table=MealGeometry.BoundsOf(assets["Kitchen_table_large"].gameObject);
        // Table positions follow this table, never the old room's coordinates.
        float spacing=Mathf.Min(.45f,table.extents.x*.5f);
        float z=table.center.z-table.extents.z*.3f;
        Pose(g.plateTablePoint,new Vector3(table.center.x-spacing,table.max.y+.002f,z),Quaternion.identity);
        Pose(g.cupTablePoint,new Vector3(table.center.x+spacing,table.max.y+.002f,z),Quaternion.identity);
        Pose(g.eatingView,new Vector3(table.center.x,table.max.y+1.0f,table.min.z-.8f),Quaternion.identity);
        g.eatingView.LookAt(new Vector3(table.center.x,table.max.y,z));
        var floor=Physics.RaycastAll(new Vector3(table.center.x,table.max.y+3,table.min.z-.6f),Vector3.down,20)
            .Where(h=>!h.collider.transform.IsChildOf(g.player.transform)).OrderBy(h=>h.distance).First();
        Pose(g.playerAfterMeal,new Vector3(table.center.x,floor.point.y+.02f,table.min.z-.6f),Quaternion.identity);
        var tb=MealGeometry.BoundsOf(toaster.gameObject);
        g.timerPanel.transform.position=new Vector3(tb.center.x,tb.max.y+.4f,tb.center.z);
        g.timerPanel.transform.rotation=Quaternion.LookRotation(toaster.forward,Vector3.up);
        foreach(var item in new[]{g.rawToast,g.jam,g.juice,g.cookedToast,g.juiceCup})item.gameObject.SetActive(false);
        g.jamToast.SetActive(false);g.bakingToast.SetActive(false);
        g.plate.gameObject.SetActive(true);g.emptyCup.SetActive(true);
        EditorUtility.SetDirty(g);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText(Folder+"/PurchasedTransfer.txt",string.Join("\n",assets.Select(p=>p.Key+" -> "+p.Value.position.ToString("F4")))+"\nTRANSFER COMPLETE\n");
        Selection.activeGameObject=g.gameObject;
        Debug.Log("MealGame purchased asset transfer complete.");
    }
    static Transform New(string n,Transform p){var o=new GameObject(n);Undo.RegisterCreatedObjectUndo(o,n);o.transform.SetParent(p,false);return o.transform;}
    static Transform Wrap(Transform visual,Transform p){var root=New(visual.name+"_Meal",p);root.position=visual.position;Undo.SetTransformParent(visual,root,"Keep purchased visual pose");return root;}
    static GrabbableItem Item(Transform visual,Transform p,MealGameController g,MealAction a){var root=Wrap(visual,p);var item=Undo.AddComponent<GrabbableItem>(root.gameObject);item.displayName=visual.name;item.enabled=false;Undo.AddComponent<MealGeometry>(root.gameObject);Target(root.gameObject,g,a);return item;}
    static void Target(GameObject o,MealGameController g,MealAction a)
    {
        var t=o.GetComponent<MealTarget>()??Undo.AddComponent<MealTarget>(o);t.game=g;t.action=a;t.displayName=o.name;
        var renderers=o.GetComponentsInChildren<Renderer>(true);
        foreach(var c in o.GetComponentsInChildren<Collider>(true)){Undo.RecordObject(c,"Replace interaction bounds");c.enabled=false;}
        foreach(var r in renderers){if(!(r is MeshRenderer))continue;var mesh=r.GetComponent<MeshFilter>();if(!mesh||!mesh.sharedMesh)continue;var c=Undo.AddComponent<BoxCollider>(r.gameObject);c.center=mesh.sharedMesh.bounds.center;c.size=mesh.sharedMesh.bounds.size;}
    }
    static Quaternion FlatRotation(GameObject o)
    {
        var r=o.GetComponentsInChildren<MeshFilter>(true).OrderByDescending(m=>m.sharedMesh?m.sharedMesh.bounds.size.sqrMagnitude:0).First();
        var size=Vector3.Scale(r.sharedMesh.bounds.size,r.transform.lossyScale);
        Vector3 n=size.x<size.y&&size.x<size.z?Vector3.right:size.z<size.y?Vector3.forward:Vector3.up;
        return Quaternion.FromToRotation(r.transform.TransformDirection(n),Vector3.up);
    }
    static void Pose(Transform t,Vector3 p,Quaternion q){Undo.RecordObject(t,"Update MealGame marker");t.SetPositionAndRotation(p,q);}
}
