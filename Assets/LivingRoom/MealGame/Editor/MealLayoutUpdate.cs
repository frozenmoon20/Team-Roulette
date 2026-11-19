using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public static class MealLayoutUpdate
{
    const string Folder="Assets/LivingRoom/MealGame";
    [InitializeOnLoadMethod]
    static void Resume()
    {
        EditorApplication.delayCall+=()=>{
            string request=Folder+"/Layout.pending";
            if(!File.Exists(request))return;
            File.Delete(request);
            try{Apply();}catch(Exception e){Debug.LogException(e);}
        };
    }
    [MenuItem("Tools/MealGame/Update counter layout and pie timer")]
    public static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play Mode first.");
        var g=Object.FindFirstObjectByType<MealGameController>();
        if(!g)throw new Exception("No MealGame in the current scene.");
        var counter=g.GetComponentsInChildren<MealTarget>(true).First(t=>t.enabled&&t.action==MealAction.Counter);
        var all=g.gameObject.scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).ToArray();
        var knife=all.First(t=>t.name=="KnifeBreakfast");
        var spoon=all.FirstOrDefault(t=>t.name=="SpoonBreakfast");
        var plateVisual=g.plate.transform.Find("PlateBigBreakfast");
        if(!plateVisual)throw new Exception("Purchased plate visual was not found.");
        Bounds plate=MealGeometry.BoundsOf(plateVisual.gameObject);
        Bounds furniture=MealGeometry.BoundsOf(counter.gameObject);
        Bounds blade=MealGeometry.BoundsOf(knife.gameObject);
        Vector3 right=furniture.extents.z>furniture.extents.x?Vector3.forward:Vector3.right;
        float diameter=Vector3.Dot(plate.size,right);
        if(diameter<.08f||diameter>1f)throw new Exception("Unexpected plate dimensions; no layout applied.");
        float height=furniture.max.y+.002f;
        Vector3 platePoint=blade.center+right*(diameter*.95f);
        // Preserve the plate's depth on the worktop; arrange across the same surface.
        if(right==Vector3.forward)platePoint.x=plate.center.x;else platePoint.z=plate.center.z;
        platePoint.y=height;
        Vector3 cupPoint=platePoint+right*(diameter*1.05f);
        Vector3 jamPoint=cupPoint+right*(diameter*.90f);
        if(!InsideTop(furniture,platePoint)||!InsideTop(furniture,cupPoint)||!InsideTop(furniture,jamPoint))
            throw new Exception("The photo layout would leave the counter; no layout applied.");

        Directory.CreateDirectory(Folder+"/Reports");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EditorSceneManager.SaveScene(g.gameObject.scene);
        string backup=Folder+"/Reports/BeforeLayout_20261119.unity.bak";
        if(!File.Exists(backup))File.Copy(g.gameObject.scene.path,backup);
        Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Organize MealGame and update counter layout");
        try
        {
            Undo.RecordObject(g.plate.transform,"Move plate");
            g.plate.transform.position+=platePoint-new Vector3(plate.center.x,plate.min.y,plate.center.z);
            Undo.RecordObject(g.emptyCup.transform,"Move cup");MealGeometry.BottomAt(g.emptyCup,cupPoint);
            Undo.RecordObject(g.juiceCup.transform,"Align filled cup");MealGeometry.BottomAt(g.juiceCup.gameObject,cupPoint);
            Undo.RecordObject(g.jamPoint,"Move jam placement to red spot");g.jamPoint.position=jamPoint;
            plate=MealGeometry.BoundsOf(plateVisual.gameObject);
            Undo.RecordObject(g.toastOnPlatePoint,"Keep toast placement on relocated plate");
            g.toastOnPlatePoint.position=new Vector3(plate.center.x,plate.max.y+.002f,plate.center.z);
            ConfigureTimer(g);
            Organize(g,counter.transform,knife,spoon);
            Undo.CollapseUndoOperations(group);
        }
        catch{Undo.RevertAllDownToGroup(group);throw;}
        EditorUtility.SetDirty(g);EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        EditorSceneManager.SaveScene(g.gameObject.scene);
        OrganizeFiles();
        File.WriteAllText(Folder+"/Reports/Layout_20261119.txt",
            "Plate bottom: "+platePoint.ToString("F4")+"\nCup bottom: "+cupPoint.ToString("F4")+
            "\nJam placement: "+jamPoint.ToString("F4")+"\nTimer diameter: 0.25 world units\n"+
            "Transparent radial countdown; no background or remaining numeric display.\n");
        Selection.activeGameObject=g.gameObject;
        var view=SceneView.lastActiveSceneView;
        if(view)view.LookAt((platePoint+jamPoint)*.5f,Quaternion.LookRotation(new Vector3(-1,-.3f,0),Vector3.up),.8f,false,true);
        Debug.Log("MealGame layout, transparent pie timer and hierarchy updated.");
        if(File.Exists(Folder+"/LayoutVerify.pending"))
        {
            File.Delete(Folder+"/LayoutVerify.pending");
            EditorApplication.delayCall+=MealLayoutVerification.Run;
        }
    }
    static bool InsideTop(Bounds bounds,Vector3 p)=>p.x>=bounds.min.x&&p.x<=bounds.max.x&&p.z>=bounds.min.z&&p.z<=bounds.max.z;
    static Transform Group(Transform parent,string name)
    {
        var found=parent.Find(name);if(found)return found;
        var go=new GameObject(name);Undo.RegisterCreatedObjectUndo(go,"Create MealGame group");go.transform.SetParent(parent,false);return go.transform;
    }
    static void Parent(Transform item,Transform group,string name=null)
    {
        if(!item)return;
        if(item.parent!=group)Undo.SetTransformParent(item,group,"Group MealGame objects");
        if(name!=null&&item.name!=name){Undo.RecordObject(item.gameObject,"Name MealGame object");item.name=name;}
    }
    static void Organize(MealGameController g,Transform counter,Transform knife,Transform spoon)
    {
        var root=g.transform;
        var furniture=Group(root,"Furniture");
        foreach(var target in g.GetComponentsInChildren<MealTarget>(true).Where(t=>t.enabled&&
            (t.action==MealAction.Counter||t.action==MealAction.Fridge||t.action==MealAction.Table||t.action==MealAction.Toaster)).ToArray())
            if(target.gameObject!=g.bakingToast&&!target.transform.IsChildOf(g.bakingToast.transform))Parent(target.transform,furniture);
        Parent(counter,furniture);
        var oldItems=root.Find("PurchasedItems");
        if(oldItems){Undo.RecordObject(oldItems.gameObject,"Rename items group");oldItems.name="Items";}
        var items=Group(root,"Items");var ingredients=items;var toast=items;var dishes=items;var cutlery=items;
        Parent(g.rawToast.transform,ingredients,"RawToast");Parent(g.jam.transform,ingredients,"Jam");Parent(g.juice.transform,ingredients,"Juice");
        Parent(g.cookedToast.transform,toast,"CookedToast");Parent(g.bakingToast.transform,toast,"BakingToast");
        Parent(g.plate.transform,dishes,"Plate");Parent(g.emptyCup.transform,dishes,"EmptyCup");Parent(g.juiceCup.transform,dishes,"JuiceCup");
        Parent(g.jamToast.transform,g.plate.transform,"JamToast");Parent(g.toastOnPlatePoint,g.plate.transform);
        Parent(knife,cutlery);Parent(spoon,cutlery);
        var points=Group(root,"PlacementPoints");Parent(g.jamPoint,points);Parent(g.cookedPopPoint,points);
        var table=points;Parent(g.plateTablePoint,table);Parent(g.cupTablePoint,table);
        var player=points;Parent(g.eatingView,player);Parent(g.playerAfterMeal,player);
        var ui=Group(root,"UI");Parent(g.timerPanel.transform,ui);Parent(g.blackScreen.transform,ui);
        var legacy=root.Find("LegacyObjects");if(legacy)Parent(legacy,Group(root,"Archive"));
        foreach(var child in root.Cast<Transform>().ToArray())
            if(child!=ui&&child.GetComponent<Canvas>())Parent(child,ui);
        furniture.SetSiblingIndex(0);items.SetSiblingIndex(1);points.SetSiblingIndex(2);ui.SetSiblingIndex(3);
    }
    static void ConfigureTimer(MealGameController g)
    {
        var rect=g.timerPanel.GetComponent<RectTransform>();Undo.RecordObject(rect,"Shrink timer");
        rect.sizeDelta=new Vector2(100,100);rect.localScale=Vector3.one*.0025f;
        foreach(var graphic in g.timerPanel.GetComponentsInChildren<Graphic>(true))
        {
            if(graphic is MealPieTimer)continue;
            Undo.RecordObject(graphic,"Hide old timer background and numbers");graphic.enabled=false;graphic.raycastTarget=false;
        }
        var pie=g.timerPanel.GetComponentInChildren<MealPieTimer>(true);
        if(!pie)
        {
            var go=new GameObject("RemainingTime",typeof(RectTransform),typeof(CanvasRenderer));Undo.RegisterCreatedObjectUndo(go,"Create transparent radial timer");
            go.transform.SetParent(rect,false);pie=Undo.AddComponent<MealPieTimer>(go);
            var child=go.GetComponent<RectTransform>();child.anchorMin=Vector2.zero;child.anchorMax=Vector2.one;child.offsetMin=child.offsetMax=Vector2.zero;
        }
        if(!pie.GetComponent<CanvasRenderer>())Undo.AddComponent<CanvasRenderer>(pie.gameObject);
        Undo.RecordObject(pie,"Configure pie timer");pie.game=g;pie.color=Color.white;pie.raycastTarget=false;pie.Preview(1);EditorUtility.SetDirty(pie);
        pie.SetAllDirty();
    }
    static void OrganizeFiles()
    {
        if(!AssetDatabase.IsValidFolder(Folder+"/Runtime"))AssetDatabase.CreateFolder(Folder,"Runtime");
        if(!AssetDatabase.IsValidFolder(Folder+"/Reports"))AssetDatabase.CreateFolder(Folder,"Reports");
        foreach(string file in new[]{"MealGameController.cs","MealGeometry.cs","MealInput.cs","MealTarget.cs","MealPieTimer.cs"})
            Move(Folder+"/"+file,Folder+"/Runtime/"+file);
        foreach(string file in new[]{"PositionCheck.txt","PurchasedTransfer.txt","Verification.txt"})Move(Folder+"/"+file,Folder+"/Reports/"+file);
        AssetDatabase.SaveAssets();
    }
    static void Move(string from,string to)
    {
        if(!File.Exists(from)||File.Exists(to))return;
        string error=AssetDatabase.MoveAsset(from,to);if(!string.IsNullOrEmpty(error))throw new Exception(error);
    }
}
