using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class MealGameAdjustments
{
    [MenuItem("Tools/MealGame/4 - Set player position after meal")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var g = Object.FindFirstObjectByType<MealGameController>();
        if (!g) throw new Exception("Open the configured MealGame scene.");
        var table = g.GetComponentsInChildren<MealTarget>(true).Single(t => t.action == MealAction.Table);
        Bounds b = table.GetComponent<Renderer>().bounds;
        var body = g.player.GetComponent<CharacterController>();
        float worldRadius = body.radius * Mathf.Max(g.player.transform.lossyScale.x, g.player.transform.lossyScale.z);
        Vector3 feet = new Vector3((g.plateTablePoint.position.x + g.cupTablePoint.position.x) * .5f,
            0, b.min.z - Mathf.Max(.5f, worldRadius + .15f));
        Physics.SyncTransforms();
        var hits = Physics.RaycastAll(new Vector3(feet.x, b.max.y + 4, feet.z), Vector3.down, 20,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            .Where(h => !h.collider.transform.IsChildOf(g.player.transform))
            .OrderBy(h => h.distance).ToArray();
        if (hits.Length == 0) throw new Exception("No floor found in front of the table.");
        float floorY = hits[0].point.y;
        feet.y = floorY + (body.height * .5f - body.center.y) * g.player.transform.lossyScale.y + .02f;
        Transform marker = g.playerAfterMeal;
        if (!marker)
        {
            var go = new GameObject("PlayerAfterMeal");
            Undo.RegisterCreatedObjectUndo(go, "Create meal exit position");
            go.transform.SetParent(g.transform.Find("PlacementPoints"), false);
            marker = go.transform;
        }
        Undo.RecordObject(marker, "Position player in front of table");
        marker.position = feet;
        Vector3 forward = (g.plateTablePoint.position + g.cupTablePoint.position) * .5f - feet;
        forward.y = 0;
        marker.rotation = Quaternion.LookRotation(forward, Vector3.up);
        Undo.RecordObject(g, "Assign meal exit position");
        g.playerAfterMeal = marker;
        EditorUtility.SetDirty(g);
        EditorSceneManager.MarkSceneDirty(g.gameObject.scene);
        EditorSceneManager.SaveScene(g.gameObject.scene);
        File.WriteAllText(MealGameSetup.Folder + "/PositionCheck.txt",
            "Floor Y: " + floorY.ToString("F3") + "\nPlayer feet marker: " + feet.ToString("F3") +
            "\nTable bounds: " + b + "\nCharacter radius: " + worldRadius.ToString("F3") + "\n");
        Selection.activeGameObject = marker.gameObject;
        Debug.Log("MealGame: player-after-meal marker = " + feet.ToString("F3"));
    }
}
