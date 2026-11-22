using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class MealSimplifyGroups
{
    const string Folder="Assets/LivingRoom/MealGame";
    [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.delayCall+=()=>{
            string path=Folder+"/Simplify.pending";
            if(!File.Exists(path))return;
            File.Delete(path);
            try{Apply();}catch(Exception e){Debug.LogException(e);}
        };
    }
    [MenuItem("Tools/MealGame/Simplify groups and reset containers")]
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode first.");
        var g=Object.FindFirstObjectByType<MealGameController>();
        if(!g)throw new Exception("No MealGame in the current scene.");
        var root=g.transform;
        var items=root.Find("Items");var points=root.Find("PlacementPoints");
        if(!items||!points)throw new Exception("Expected MealGame groups were not found.");
        var containers=new[]{root,root.Find("Furniture"),items,points,root.Find("UI"),
            g.rawToast.transform,g.jam.transform,g.juice.transform,g.cookedToast.transform,g.plate.transform,
            g.juiceCup.transform,g.emptyCup.transform,g.jamToast.transform}
            .Where(t=>t && !PrefabUtility.IsPartOfPrefabInstance(t) && !t.GetComponent<Renderer>() &&
                !t.GetComponent<MeshFilter>() && !(t is RectTransform)).Distinct().ToArray();
        var checks=g.GetComponentsInChildren<Renderer>(true).Select(r=>r.transform)
            .Concat(new[]{g.jamPoint,g.cookedPopPoint,g.toastOnPlatePoint,g.plateTablePoint,g.cupTablePoint,g.eatingView,g.playerAfterMeal,g.timerPanel.transform,g.blackScreen.transform})
            .Where(t=>t).Distinct().Select(t=>new PoseRecord(t)).ToArray();
        var active=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.activeSelf);
        EditorSceneManager.SaveScene(g.gameObject.scene);
        string backup=Folder+"/Reports/BeforeSimplify_20261119.unity.bak";
        if(!File.Exists(backup))File.Copy(g.gameObject.scene.path,backup);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Simplify MealGame groups and reset empty containers");
        try
        {
            foreach(string name in new[]{"Ingredients","Toast","Tableware","Cutlery"})Flatten(items,name);
            foreach(string name in new[]{"Counter","Toaster","DiningTable","PlayerView"})Flatten(points,name);
            foreach(var t in containers)
            {
                ResetAndRestore(t);
                try{foreach(var record in checks)record.Verify();}
                catch(Exception e){throw new Exception("After resetting "+t.name+": "+e.Message,e);}
            }
            foreach(var record in checks)record.Verify();
            foreach(var pair in active)if(pair.Key&&pair.Key.gameObject.activeSelf!=pair.Value)throw new Exception("Visibility changed: "+pair.Key.name);
            if(g.jamToast.transform.parent!=g.plate.transform||g.toastOnPlatePoint.parent!=g.plate.transform)
                throw new Exception("Plate assembly was changed.");
            foreach(var t in containers)
                if(Quaternion.Angle(t.localRotation,Quaternion.identity)>.001f||(t.localScale-Vector3.one).sqrMagnitude>.000001f)
                    throw new Exception("Container was not normalized: "+t.name);
            Undo.CollapseUndoOperations(group);
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
        EditorSceneManager.MarkSceneDirty(g.gameObject.scene);EditorSceneManager.SaveScene(g.gameObject.scene);
        File.WriteAllText(Folder+"/Reports/Simplify_20261119.txt",
            "Groups: Furniture / Items / PlacementPoints / UI\n"+
            "Reset and repositioned: "+containers.Length+" empty containers\n"+
            "Preserved world pose and scale: "+checks.Length+" renderer/placement/UI transforms\n"+
            "All surviving object visibility flags preserved. Plate assembly preserved.\n"+
            string.Join("\n",containers.Select(t=>t.name+": position="+t.position.ToString("F4")+", local rotation=(0,0,0), local scale=(1,1,1)"))+"\nALL CHECKS PASSED\n");
        Selection.activeGameObject=g.gameObject;
        Debug.Log("MealGame groups simplified; empty containers reset; all model and marker world poses preserved.");
    }
    static void Flatten(Transform parent,string name)
    {
        var group=parent.Find(name);if(!group)return;
        if(group.GetComponents<Component>().Length!=1)throw new Exception("Group has functional components: "+name);
        foreach(var child in group.Cast<Transform>().ToArray())Reparent(child,parent,"Flatten MealGame group");
        // Only the now-empty organizational shell is removed; Undo retains it.
        Undo.DestroyObjectImmediate(group.gameObject);
    }
    static void ResetAndRestore(Transform t)
    {
        Vector3 position=t.position;
        var children=t.Cast<Transform>().ToArray();
        foreach(var child in children)Reparent(child,t.parent,"Keep child world pose during reset");
        Undo.RecordObject(t,"Reset empty transform");
        t.localPosition=Vector3.zero;t.localRotation=Quaternion.identity;t.localScale=Vector3.one;
        t.position=position;
        for(int i=0;i<children.Length;i++)
        {
            Reparent(children[i],t,"Restore children to reset container");
            children[i].SetSiblingIndex(i);
        }
    }
    static void Reparent(Transform child,Transform parent,string label)
    {
        Matrix4x4 world=child.localToWorldMatrix;
        Undo.SetTransformParent(child,parent,label);
        var native=child.localToWorldMatrix;bool preserved=true;
        for(int i=0;i<16;i++)if(Mathf.Abs(native[i]-world[i])>.000001f){preserved=false;break;}
        if(preserved)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(child))PrefabUtility.RecordPrefabInstancePropertyModifications(child);
            return;
        }
        Matrix4x4 local=parent?parent.worldToLocalMatrix*world:world;
        Vector3 x=local.GetColumn(0),y=local.GetColumn(1),z=local.GetColumn(2);
        float sx=x.magnitude,sy=y.magnitude,sz=z.magnitude;
        if(Mathf.Min(sx,Mathf.Min(sy,sz))<.0000001f)throw new Exception("Cannot reset a zero-scale container: "+child.name);
        if(Vector3.Dot(Vector3.Cross(x,y),z)<0)sx=-sx;
        Undo.RecordObject(child,label);
        child.localPosition=local.GetColumn(3);
        child.localRotation=Quaternion.LookRotation(z/sz,y/sy);
        child.localScale=new Vector3(sx,sy,sz);
        if(PrefabUtility.IsPartOfPrefabInstance(child))PrefabUtility.RecordPrefabInstancePropertyModifications(child);
    }
    sealed class PoseRecord
    {
        readonly Transform t;readonly Matrix4x4 matrix;
        public PoseRecord(Transform value){t=value;matrix=t.localToWorldMatrix;}
        public void Verify()
        {
            // Imported mirrored models can express the same world matrix with
            // different rotation/scale signs. Compare the actual geometry mapping.
            if(!t)throw new Exception("Missing transform during normalization.");
            var current=t.localToWorldMatrix;
            for(int i=0;i<16;i++)
                if(Mathf.Abs(current[i]-matrix[i])>.0001f)
                    throw new Exception("World geometry changed: "+t.name+"; matrix element "+i+" delta="+(current[i]-matrix[i]).ToString("F7"));
        }
    }
}
