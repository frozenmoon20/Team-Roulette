using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MealGameSetup
{
    public const string Folder = "Assets/LivingRoom/MealGame";
    const string ScenePath = "Assets/Scenes/Room3_LivingRoom.unity";

    [MenuItem("Tools/MealGame/1 - Set up current scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play mode first.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) throw new Exception("Open Room3_LivingRoom first; no scene was changed.");
        if (Object.FindFirstObjectByType<MealGameController>(FindObjectsInactive.Include))
            throw new Exception("MealGame is already configured. Existing settings were preserved.");
        var all = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Transform>(true)).ToArray();
        Func<string, GameObject> find = name => all.Single(t => t.name == name).gameObject;
        string[] names = { "냉장고", "Table", "조리대", "조리대 (1)", "토스터기", "토스트", "잼", "주스", "익은 토스트", "접시", "잼 토스트", "빈 컵", "주스컵" };
        var objects = names.ToDictionary(n => n, find); // Validate every name before editing.
        var root = New("MealGame", null);
        var game = Undo.AddComponent<MealGameController>(root);
        foreach (var go in objects.Values) Undo.SetTransformParent(go.transform, root.transform, "Group MealGame");

        game.rawToast = Item(objects["토스트"]);
        game.jam = Item(objects["잼"]);
        game.juice = Item(objects["주스"]);
        game.cookedToast = Item(objects["익은 토스트"]);
        game.plate = Item(objects["접시"]);
        game.juiceCup = Item(objects["주스컵"]);
        game.jamToast = objects["잼 토스트"];
        game.emptyCup = objects["빈 컵"];
        foreach (string n in new[] { "토스트", "잼", "주스", "익은 토스트", "접시", "잼 토스트", "빈 컵", "주스컵" })
            FixCollider(objects[n]);

        game.interactor = Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        game.player = Object.FindFirstObjectByType<FirstPersonPlayer>(FindObjectsInactive.Include);
        var input = Undo.AddComponent<MealInput>(root);
        input.game = game; input.shared = game.interactor;
        Target(objects["냉장고"], game, MealAction.Fridge);
        Target(objects["Table"], game, MealAction.Table);
        Target(objects["조리대"], game, MealAction.Counter);
        Target(objects["조리대 (1)"], game, MealAction.Counter);
        Target(objects["토스터기"], game, MealAction.Toaster);
        Target(game.rawToast.gameObject, game, MealAction.RawToast);
        Target(game.jam.gameObject, game, MealAction.Jam);
        Target(game.juice.gameObject, game, MealAction.Juice);
        Target(game.cookedToast.gameObject, game, MealAction.CookedToast);
        Target(game.plate.gameObject, game, MealAction.Plate);
        Target(game.jamToast, game, MealAction.JamToast);
        Target(game.emptyCup, game, MealAction.EmptyCup);
        Target(game.juiceCup.gameObject, game, MealAction.JuiceCup);

        var points = New("PlacementPoints", root.transform).transform;
        Bounds counter = objects["조리대"].GetComponent<Renderer>().bounds;
        Bounds toaster = objects["토스터기"].GetComponent<Renderer>().bounds;
        Bounds table = objects["Table"].GetComponent<Renderer>().bounds;
        Bounds dish = game.plate.GetComponent<Renderer>().bounds;
        game.jamPoint = Point("JamOnCounter", points, new Vector3(counter.center.x, counter.max.y + .005f, 13.5f));
        game.toastOnPlatePoint = Point("ToastOnPlate", game.plate.transform, new Vector3(dish.center.x, dish.max.y + .002f, dish.center.z));
        game.plateTablePoint = Point("PlateOnTable_Left", points, new Vector3(table.center.x - .5f, table.max.y + .005f, table.center.z - .35f));
        game.cupTablePoint = Point("CupOnTable_Right", points, new Vector3(table.center.x + .5f, table.max.y + .005f, table.center.z - .35f));
        game.cookedPopPoint = Point("CookedToastPop", points, new Vector3(toaster.center.x, toaster.max.y + .23f, toaster.center.z));
        game.cookedPopPoint.rotation = Quaternion.Euler(0, 0, 90);
        game.eatingView = Point("EatingView", points, new Vector3(table.center.x, table.max.y + 1.25f, table.min.z - .9f));
        game.eatingView.LookAt(new Vector3(table.center.x, table.max.y + .1f, table.center.z - .35f));

        // The pre-existing jam toast is a child of the plate from the beginning.
        Undo.SetTransformParent(game.jamToast.transform, game.plate.transform, "Attach jam toast to plate");
        game.jamToast.transform.rotation = Quaternion.identity;
        Bounds bread = game.jamToast.GetComponent<Renderer>().bounds;
        game.jamToast.transform.position += game.toastOnPlatePoint.position - new Vector3(bread.center.x, bread.min.y, bread.center.z);
        game.jamToast.SetActive(false);
        game.cookedToast.rotationOffset = Vector3.zero; // Existing mesh is already horizontal.

        game.bakingToast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        game.bakingToast.name = "ToastInToaster";
        Undo.RegisterCreatedObjectUndo(game.bakingToast, "Create baking toast");
        game.bakingToast.transform.SetParent(root.transform, false);
        game.bakingToast.transform.localScale = new Vector3(.10f, .3f, .3f);
        game.bakingToast.transform.position = new Vector3(toaster.center.x, toaster.max.y - .09f, toaster.center.z);
        Target(game.bakingToast, game, MealAction.Toaster);
        game.bakingToast.SetActive(false);

        var materialFolder = Folder + "/Materials";
        Directory.CreateDirectory(materialFolder);
        ColorItem(game.rawToast.gameObject, "Bread", new Color(.88f, .73f, .46f));
        ColorItem(game.bakingToast, "Bread", new Color(.88f, .73f, .46f));
        ColorItem(game.cookedToast.gameObject, "ToastedBread", new Color(.65f, .36f, .12f));
        ColorItem(game.jamToast, "JamToast", new Color(.56f, .07f, .13f));
        ColorItem(game.jam.gameObject, "Jam", new Color(.75f, .13f, .2f));
        ColorItem(game.juice.gameObject, "Juice", new Color(1f, .52f, .04f));
        ColorItem(game.juiceCup.gameObject, "Juice", new Color(1f, .52f, .04f));

        BuildTimer(game, root.transform, toaster);
        BuildBlackScreen(game, root.transform);
        game.toasterAudio = Undo.AddComponent<AudioSource>(objects["토스터기"]);
        game.toasterAudio.playOnAwake = false;
        game.toasterAudio.spatialBlend = 1;
        game.toasterAudio.minDistance = 2;
        game.toasterAudio.maxDistance = 25;
        game.actionAudio = Undo.AddComponent<AudioSource>(root);
        game.actionAudio.playOnAwake = false;
        game.actionAudio.spatialBlend = 0;
        game.dingSound = MealSoundAssets.Make("ToasterDing", 1.1f, 0);
        game.pourSound = MealSoundAssets.Make("PourJuice", 1.5f, 1);
        game.eatSound = MealSoundAssets.Make("EatToast", 3f, 2);
        foreach (var item in new[] { game.rawToast, game.jam, game.juice, game.cookedToast, game.juiceCup }) item.gameObject.SetActive(false);
        game.emptyCup.SetActive(true);
        game.plate.gameObject.SetActive(true);
        game.timerPanel.SetActive(false);
        EditorUtility.SetDirty(game);
        EditorUtility.SetDirty(input);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("MealGame: setup complete; existing furniture positions preserved.");
    }

    static GameObject New(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create MealGame object");
        go.transform.SetParent(parent, false);
        return go;
    }
    static Transform Point(string name, Transform parent, Vector3 position)
    {
        var t = New(name, parent).transform;
        t.position = position;
        t.rotation = Quaternion.identity;
        // A point below a scaled plate must retain a unit world scale.
        Vector3 s = parent.lossyScale;
        t.localScale = new Vector3(1 / s.x, 1 / s.y, 1 / s.z);
        return t;
    }
    static GrabbableItem Item(GameObject go)
    {
        var item = Undo.AddComponent<GrabbableItem>(go);
        item.displayName = go.name;
        item.enabled = false;
        return item;
    }
    static void Target(GameObject go, MealGameController game, MealAction action)
    {
        var t = Undo.AddComponent<MealTarget>(go);
        t.game = game; t.action = action; t.displayName = go.name;
    }
    static void FixCollider(GameObject go)
    {
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        foreach (var c in go.GetComponents<Collider>()) Undo.DestroyObjectImmediate(c);
        var box = Undo.AddComponent<BoxCollider>(go);
        box.center = mesh.bounds.center; box.size = mesh.bounds.size;
    }
    static void ColorItem(GameObject go, string name, Color color)
    {
        string path = Folder + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
        }
        Undo.RecordObject(go.GetComponent<Renderer>(), "Set MealGame material");
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }
    static void BuildTimer(MealGameController game, Transform parent, Bounds toaster)
    {
        var go = new GameObject("ToasterTimer", typeof(RectTransform), typeof(Canvas));
        Undo.RegisterCreatedObjectUndo(go, "Create timer");
        go.transform.SetParent(parent, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 100);
        rt.localScale = Vector3.one * .004f;
        rt.position = new Vector3(toaster.center.x, toaster.max.y + .85f, toaster.center.z);
        rt.rotation = Quaternion.Euler(0, 90, 0); // Face the kitchen, away from the east wall.
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(rt, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.GetComponent<Image>().color = new Color(.035f, .045f, .055f, .95f);
        panel.GetComponent<Image>().raycastTarget = false;
        var digits = new GameObject("Seconds", typeof(RectTransform), typeof(TextMeshProUGUI));
        digits.transform.SetParent(rt, false);
        Stretch(digits.GetComponent<RectTransform>());
        var text = digits.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = "10"; text.fontSize = 64; text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, .79f, .3f); text.raycastTarget = false;
        game.timerPanel = go; game.timerText = text;
    }
    static void BuildBlackScreen(MealGameController game, Transform parent)
    {
        var go = new GameObject("MealBlackScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        Undo.RegisterCreatedObjectUndo(go, "Create meal fade");
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32001;
        var image = new GameObject("Black", typeof(RectTransform), typeof(Image));
        image.transform.SetParent(go.transform, false); Stretch(image.GetComponent<RectTransform>());
        image.GetComponent<Image>().color = Color.black; image.GetComponent<Image>().raycastTarget = false;
        game.blackScreen = go.GetComponent<CanvasGroup>(); game.blackScreen.alpha = 0;
        game.blackScreen.blocksRaycasts = false;
    }
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
