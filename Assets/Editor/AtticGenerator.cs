// File: Assets/Editor/AtticGenerator.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.Shapes;

public static class AtticGenerator
{
    // ─── GEOMETRIC CONSTANTS (1 Unity unit = 1 ft) ────────────────────────────────
    private const float MainBlock_L = 40f;
    private const float MainBlock_W = 25f;
    private const float WingBlock_L = 20f;
    private const float WingBlock_W = 20f;

    private const float EaveHeight = 4f;
    private static readonly float MainRidgeHeight = EaveHeight + (MainBlock_W * 0.5f * (5f / 12f));
    private static readonly float WingRidgeHeight = EaveHeight + (WingBlock_W * 0.5f * (5f / 12f));

    private const float DeckThickness = 0.0625f;

    // --- STRUCTURAL CONSTANTS BASED ON REFERENCE IMAGE ---
    private const float PlankWidth = 0.5f;       // 6-inch planks
    private const float KneeWallHeight = 4f;     // Vertical knee-wall height
    private const float FlatCeilingWidth = 10f;  // Width of flat center ceiling

    private const float HatchWidth = 2.5f;
    private const float HatchLength = 4.5f;
    private const float HatchDistFromRearWall = 6f;

    private const float TrussSpacing = 3.5f;         // wider spacing
    private const float TrussMemberThickness = 0.33f;

    // ─── MENU ENTRY ───────────────────────────────────────────────────────────────
    [MenuItem("Tools/Malicious Compliance/Generate Attic", false, 1)]
    public static void GenerateAtticFromMenu()
    {
        Undo.SetCurrentGroupName("Generate Attic");
        int group = Undo.GetCurrentGroup();

        // remove any existing
        var old = GameObject.Find("Attic");
        if (old)
            Undo.DestroyObjectImmediate(old);

        // root
        var atticRoot = new GameObject("Attic");
        Undo.RegisterCreatedObjectUndo(atticRoot, "Generate Attic");

        // parents
        var roofParent     = CreateChild(atticRoot, "RoofShell_Exterior");
        var interiorParent = CreateChild(atticRoot, "InteriorShell");
        var deckParent     = CreateChild(atticRoot, "Floor");
        var trussParent    = CreateChild(atticRoot, "Trusses");
        var hatchParent    = CreateChild(atticRoot, "Hatch");
        var windowParent   = CreateChild(atticRoot, "Windows");
        var ventParent     = CreateChild(atticRoot, "Vent");
        var lightParent    = CreateChild(atticRoot, "Lights");
        var propsParent    = CreateChild(atticRoot, "Props");

        // Create white material
        var whiteMat = CreateWhiteMaterial();

        // build sequence
        BuildDeck(deckParent.transform, whiteMat);
        BuildHatch(hatchParent.transform, whiteMat);
        BuildRoofExterior(roofParent.transform, whiteMat, whiteMat);
        BuildInteriorShell(interiorParent.transform, whiteMat);
        BuildTrusses(trussParent.transform, whiteMat);
        BuildGableDetails(windowParent.transform, ventParent.transform, whiteMat);
        BuildLights(lightParent.transform);
        ScatterProps(propsParent.transform, whiteMat);

        Undo.CollapseUndoOperations(group);

        Selection.activeGameObject = atticRoot;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    // ─── CREATE WHITE MATERIAL ───────────────────────────────────────────────────
    private static Material CreateWhiteMaterial()
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.color = Color.white;
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Glossiness", 0.2f);
        return mat;
    }

    // ─── BUILDERS ────────────────────────────────────────────────────────────────

    private static void BuildDeck(Transform parent, Material mat)
    {
        // Main block floor
        int count = Mathf.FloorToInt(MainBlock_W / PlankWidth);
        for (int i = 0; i < count; i++)
        {
            float x = -MainBlock_W * 0.5f + PlankWidth * 0.5f + i * PlankWidth;
            var plank = CreateProBuilderCube(
                parent, 
                $"Plank_{i:00}",
                new Vector3(PlankWidth, DeckThickness, MainBlock_L),
                new Vector3(x, DeckThickness * 0.5f, 0),
                mat
            );
            plank.AddComponent<MeshCollider>();
        }

        // Wing block floor
        int wingCount = Mathf.FloorToInt(WingBlock_W / PlankWidth);
        for (int i = 0; i < wingCount; i++)
        {
            float x = MainBlock_W * 0.5f + PlankWidth * 0.5f + i * PlankWidth;
            var plank = CreateProBuilderCube(
                parent,
                $"WingPlank_{i:00}",
                new Vector3(PlankWidth, DeckThickness, WingBlock_L),
                new Vector3(x, DeckThickness * 0.5f, 0),
                mat
            );
            plank.AddComponent<MeshCollider>();
        }
    }

    private static void BuildHatch(Transform parent, Material mat)
    {
        float t      = 0.25f;
        float rearZ  = -MainBlock_L * 0.5f;
        float center = rearZ + HatchDistFromRearWall;
        float y      = DeckThickness;

        CreateProBuilderCube(parent, "Hatch_Back",  new Vector3(HatchWidth, t, t),  new Vector3(0, y + t*0.5f, center - HatchLength*0.5f), mat);
        CreateProBuilderCube(parent, "Hatch_Front", new Vector3(HatchWidth, t, t),  new Vector3(0, y + t*0.5f, center + HatchLength*0.5f), mat);
        CreateProBuilderCube(parent, "Hatch_Left",  new Vector3(t, t, HatchLength), new Vector3(-HatchWidth*0.5f, y + t*0.5f, center), mat);
        CreateProBuilderCube(parent, "Hatch_Right", new Vector3(t, t, HatchLength), new Vector3( HatchWidth*0.5f, y + t*0.5f, center), mat);
    }

    private static void BuildInteriorShell(Transform parent, Material mat)
    {
        float halfW = FlatCeilingWidth * 0.5f;
        float halfL = MainBlock_L * 0.5f;

        // compute angled ceiling peak by linear interpolation
        float rise = MainRidgeHeight - EaveHeight;
        float run  = MainBlock_W * 0.5f;
        float t    = halfW / run;
        float peakY = Mathf.Lerp(EaveHeight, MainRidgeHeight, t);

        // Build full perimeter walls for main block
        // Left wall (full height)
        CreatePolygon(parent, "Wall_Left", mat, new[] {
            new Vector3(-MainBlock_W * 0.5f, 0,  halfL),
            new Vector3(-MainBlock_W * 0.5f, 0, -halfL),
            new Vector3(-MainBlock_W * 0.5f, EaveHeight, -halfL),
            new Vector3(-MainBlock_W * 0.5f, EaveHeight,  halfL)
        });

        // Right wall sections (around wing opening)
        // Right wall front section
        CreatePolygon(parent, "Wall_Right_Front", mat, new[] {
            new Vector3(MainBlock_W * 0.5f, 0, halfL),
            new Vector3(MainBlock_W * 0.5f, 0, WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f, EaveHeight, WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f, EaveHeight, halfL)
        });

        // Right wall back section
        CreatePolygon(parent, "Wall_Right_Back", mat, new[] {
            new Vector3(MainBlock_W * 0.5f, 0, -WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f, 0, -halfL),
            new Vector3(MainBlock_W * 0.5f, EaveHeight, -halfL),
            new Vector3(MainBlock_W * 0.5f, EaveHeight, -WingBlock_L * 0.5f)
        });

        // Wing walls
        // Wing outer wall
        CreatePolygon(parent, "Wall_Wing_Outer", mat, new[] {
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, 0, WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, 0, -WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, EaveHeight, -WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, EaveHeight, WingBlock_L * 0.5f)
        });

        // Wing front wall
        CreatePolygon(parent, "Wall_Wing_Front", mat, new[] {
            new Vector3(MainBlock_W * 0.5f, 0, WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, 0, WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, EaveHeight, WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f, EaveHeight, WingBlock_L * 0.5f)
        });

        // Wing back wall
        CreatePolygon(parent, "Wall_Wing_Back", mat, new[] {
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, 0, -WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f, 0, -WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f, EaveHeight, -WingBlock_L * 0.5f),
            new Vector3(MainBlock_W * 0.5f + WingBlock_W, EaveHeight, -WingBlock_L * 0.5f)
        });

        // Interior knee-walls and ceiling
        // knee-walls
        CreatePolygon(parent, "KneeWall_Left",  mat, new[] {
            new Vector3(-halfW, 0,  halfL),
            new Vector3(-halfW, 0, -halfL),
            new Vector3(-halfW, KneeWallHeight, -halfL),
            new Vector3(-halfW, KneeWallHeight,  halfL)
        });
        CreatePolygon(parent, "KneeWall_Right", mat, new[] {
            new Vector3( halfW, 0, -halfL),
            new Vector3( halfW, 0,  halfL),
            new Vector3( halfW, KneeWallHeight,  halfL),
            new Vector3( halfW, KneeWallHeight, -halfL)
        });

        // flat ceiling
        CreatePolygon(parent, "Ceiling_Flat", mat, new[] {
            new Vector3( halfW, KneeWallHeight,  halfL),
            new Vector3( halfW, KneeWallHeight, -halfL),
            new Vector3(-halfW, KneeWallHeight, -halfL),
            new Vector3(-halfW, KneeWallHeight,  halfL)
        });

        // angled ceilings
        CreatePolygon(parent, "Ceiling_Angled_Left", mat, new[] {
            new Vector3(-halfW, KneeWallHeight, -halfL),
            new Vector3(   0f, peakY,           -halfL),
            new Vector3(   0f, peakY,            halfL),
            new Vector3(-halfW, KneeWallHeight,  halfL)
        });
        CreatePolygon(parent, "Ceiling_Angled_Right", mat, new[] {
            new Vector3( halfW, KneeWallHeight, -halfL),
            new Vector3( halfW, KneeWallHeight,  halfL),
            new Vector3(   0f, peakY,            halfL),
            new Vector3(   0f, peakY,           -halfL)
        });

        // end walls: front with hole, back solid
        BuildEndWall(parent, mat,  halfL, true,  peakY);
        BuildEndWall(parent, mat, -halfL, false, peakY);
    }

    private static void BuildEndWall(Transform parent, Material mat, float z, bool withWindow, float peakY)
    {
        float halfW = FlatCeilingWidth * 0.5f;

        var bl = new Vector3(-halfW, 0, z);
        var br = new Vector3( halfW, 0, z);
        var tr = new Vector3( halfW, KneeWallHeight, z);
        var tl = new Vector3(-halfW, KneeWallHeight, z);
        var pk = new Vector3(   0f, peakY,          z);

        if (!withWindow)
        {
            CreatePolygon(parent, z > 0 ? "FrontWall" : "BackWall", mat, new[] { bl, br, tr, pk, tl });
            return;
        }

        // window opening dims
        float ww = 2.5f, wh = 3f, wy = 5.5f;
        var wbl = new Vector3(-ww*0.5f, wy - wh*0.5f, z);
        var wbr = new Vector3( ww*0.5f, wy - wh*0.5f, z);
        var wtr = new Vector3( ww*0.5f, wy + wh*0.5f, z);
        var wtl = new Vector3(-ww*0.5f, wy + wh*0.5f, z);

        CreatePolygon(parent, "Front_BelowWindow", mat, new[] { bl, br, wbr, wbl });
        CreatePolygon(parent, "Front_LeftWindow",  mat, new[] { bl, wbl, wtl, tl });
        CreatePolygon(parent, "Front_RightWindow", mat, new[] { br, tr, wtr, wbr });
        CreatePolygon(parent, "Front_AboveWindow", mat, new[] { wtl, wtr, tr, pk, tl });
    }

    private static void BuildRoofExterior(Transform parent, Material shingles, Material wood)
    {
        float mW = MainBlock_W * 0.5f, mL = MainBlock_L * 0.5f;
        float wW = WingBlock_W * 0.5f, wL = WingBlock_L * 0.5f;

        var valleyPeak      = new Vector3(0, MainRidgeHeight,    0);
        var valleyEaveFront = new Vector3(mW, EaveHeight,   wL);
        var valleyEaveBack  = new Vector3(mW, EaveHeight,  -wL);
        var wingPeak        = new Vector3(mW + wW, WingRidgeHeight, 0);

        var roofRoot = CreateChild(parent.gameObject, "RoofGeometry").transform;

        CreatePolygon(roofRoot, "Main_Left",        shingles, new[] { new Vector3(-mW, EaveHeight,  mL), new Vector3(-mW, EaveHeight, -mL), new Vector3(0, MainRidgeHeight, -mL), new Vector3(0, MainRidgeHeight,  mL) });
        CreatePolygon(roofRoot, "Main_RightFront",  shingles, new[] { new Vector3(mW, EaveHeight,  mL), valleyEaveFront, valleyPeak, new Vector3(0, MainRidgeHeight,  mL) });
        CreatePolygon(roofRoot, "Main_RightBack",   shingles, new[] { new Vector3(mW, EaveHeight, -mL), valleyEaveBack,  valleyPeak, new Vector3(0, MainRidgeHeight, -mL) });
        CreatePolygon(roofRoot, "Wing_ValleySide",  shingles, new[] { valleyEaveBack, valleyEaveFront, wingPeak });
        CreatePolygon(roofRoot, "Wing_OuterSide",   shingles, new[] { valleyEaveFront + Vector3.right * WingBlock_W, valleyEaveBack + Vector3.right * WingBlock_W, wingPeak });
        CreatePolygon(roofRoot, "Gable_MainFront",  wood,     new[] { new Vector3(-mW, EaveHeight,  mL), new Vector3(mW, EaveHeight,  mL), new Vector3(0, MainRidgeHeight,  mL) });
        CreatePolygon(roofRoot, "Gable_MainBack",   wood,     new[] { new Vector3(mW, EaveHeight, -mL), new Vector3(-mW, EaveHeight, -mL), new Vector3(0, MainRidgeHeight, -mL) });
        CreatePolygon(roofRoot, "Gable_WingSide",   wood,     new[] { valleyEaveBack + Vector3.right * WingBlock_W, valleyEaveFront + Vector3.right * WingBlock_W, wingPeak });
    }

    private static void BuildTrusses(Transform parent, Material mat)
    {
        int count   = Mathf.FloorToInt(MainBlock_L / TrussSpacing);
        float halfW = FlatCeilingWidth * 0.5f;

        float rise = MainRidgeHeight - EaveHeight;
        float run  = MainBlock_W * 0.5f;
        float t    = halfW / run;
        float peakY = Mathf.Lerp(EaveHeight, MainRidgeHeight, t);

        // Build trusses for the entire length (no skipping)
        for (int i = 1; i < count; i++)
        {
            float z = -MainBlock_L * 0.5f + i * TrussSpacing;

            var trussGO = CreateChild(parent.gameObject, $"Truss_{i:00}");
            trussGO.transform.localPosition = new Vector3(0, 0, z);

            // Vertical studs
            CreateProBuilderCube(trussGO.transform, "Stud_Left",  new Vector3(TrussMemberThickness, KneeWallHeight, TrussMemberThickness), new Vector3(-halfW, KneeWallHeight * 0.5f, 0), mat);
            CreateProBuilderCube(trussGO.transform, "Stud_Right", new Vector3(TrussMemberThickness, KneeWallHeight, TrussMemberThickness), new Vector3( halfW, KneeWallHeight * 0.5f, 0), mat);
            
            // Collar tie (horizontal beam)
            CreateProBuilderCube(trussGO.transform, "CollarTie",  new Vector3(FlatCeilingWidth, TrussMemberThickness, TrussMemberThickness), new Vector3(0, KneeWallHeight, 0), mat);

            // Rafters
            var leftStart  = new Vector3(-halfW, KneeWallHeight, 0);
            var rightStart = new Vector3( halfW, KneeWallHeight, 0);
            var apex       = new Vector3(0, peakY, 0);
            float length   = Vector3.Distance(leftStart, apex);
            float angle    = Vector3.Angle(Vector3.right, apex - leftStart);

            var rL = CreateProBuilderCube(trussGO.transform, "Rafter_L", new Vector3(length, TrussMemberThickness, TrussMemberThickness), Vector3.Lerp(leftStart, apex, 0.5f), mat);
            rL.transform.localRotation = Quaternion.Euler(0, 0, angle);
            var rR = CreateProBuilderCube(trussGO.transform, "Rafter_R", new Vector3(length, TrussMemberThickness, TrussMemberThickness), Vector3.Lerp(rightStart, apex, 0.5f), mat);
            rR.transform.localRotation = Quaternion.Euler(0, 0, -angle);

            // Add center vertical support
            CreateProBuilderCube(trussGO.transform, "CenterSupport", new Vector3(TrussMemberThickness, peakY - KneeWallHeight, TrussMemberThickness), new Vector3(0, KneeWallHeight + (peakY - KneeWallHeight) * 0.5f, 0), mat);
        }

        // Add wing trusses
        BuildWingTrusses(parent, mat);
    }

    private static void BuildWingTrusses(Transform parent, Material mat)
    {
        int count = Mathf.FloorToInt(WingBlock_L / TrussSpacing);
        float wingCenterX = MainBlock_W * 0.5f + WingBlock_W * 0.5f;

        for (int i = 1; i < count; i++)
        {
            float z = -WingBlock_L * 0.5f + i * TrussSpacing;

            var trussGO = CreateChild(parent.gameObject, $"WingTruss_{i:00}");
            trussGO.transform.localPosition = new Vector3(wingCenterX, 0, z);

            float halfW = WingBlock_W * 0.5f;
            float peakY = WingRidgeHeight;

            // Wing rafters (simplified triangular structure)
            var leftStart = new Vector3(-halfW, EaveHeight, 0);
            var rightStart = new Vector3(halfW, EaveHeight, 0);
            var apex = new Vector3(0, peakY, 0);
            float length = Vector3.Distance(leftStart, apex);
            float angle = Vector3.Angle(Vector3.right, apex - leftStart);

            var rL = CreateProBuilderCube(trussGO.transform, "WingRafter_L", new Vector3(length, TrussMemberThickness, TrussMemberThickness), Vector3.Lerp(leftStart, apex, 0.5f), mat);
            rL.transform.localRotation = Quaternion.Euler(0, 0, angle);
            var rR = CreateProBuilderCube(trussGO.transform, "WingRafter_R", new Vector3(length, TrussMemberThickness, TrussMemberThickness), Vector3.Lerp(rightStart, apex, 0.5f), mat);
            rR.transform.localRotation = Quaternion.Euler(0, 0, -angle);
        }
    }

    private static void BuildGableDetails(Transform windowParent, Transform ventParent, Material mat)
    {
        float win_w = 2.5f, win_h = 3f, win_y = 5.5f;
        CreateProBuilderCube(windowParent, "GableWindow_Front", new Vector3(win_w, win_h, 0.5f), new Vector3(0, win_y,  MainBlock_L * .5f), mat);
        CreateProBuilderCube(windowParent, "GableWindow_Back",  new Vector3(win_w, win_h, 0.5f), new Vector3(0, win_y, -MainBlock_L * .5f), mat);

        float s = 1.5f;
        CreateProBuilderCube(ventParent, "GableVent", new Vector3(s, s, .2f), new Vector3(0, MainRidgeHeight - s, MainBlock_L * .5f), mat);
    }

    private static void BuildLights(Transform parent)
    {
        for (int i = 0; i < 2; i++)
        {
            var bulb = new GameObject($"AtticBulb_{i+1}");
            Undo.RegisterCreatedObjectUndo(bulb, "Generate Attic");
            bulb.transform.SetParent(parent, false);
            bulb.transform.localPosition = new Vector3(0, KneeWallHeight - 1f, (i == 0 ? 1 : -1) * MainBlock_L * 0.25f);

            var lt = bulb.AddComponent<Light>();
            lt.type      = LightType.Point;
            lt.range     = 25;
            lt.color     = new Color(1f, 0.9f, 0.75f);
            lt.intensity = 1.8f;
            lt.shadows   = LightShadows.Soft;
        }
    }

    private static void ScatterProps(Transform parent, Material mat)
    {
        Rect clear = new Rect(-2.5f, -MainBlock_L * 0.5f + HatchDistFromRearWall - 3.5f, 5f, 7f);
        for (int i = 0; i < 15; i++)
        {
            Vector3 sz = new Vector3(Random.Range(1f, 2.5f), Random.Range(1f, 2f), Random.Range(1f, 2.5f));
            Vector3 pos; int tries = 0;
            do
            {
                pos = new Vector3(
                    Random.Range(-FlatCeilingWidth * 0.45f, FlatCeilingWidth * 0.45f),
                    DeckThickness + sz.y * 0.5f,
                    Random.Range(-MainBlock_L * 0.5f, MainBlock_L * 0.5f)
                );
            } while (clear.Contains(new Vector2(pos.x, pos.z)) && ++tries < 100);

            if (tries < 100)
            {
                var box = CreateProBuilderCube(parent, $"Box_{i:00}", sz, pos, mat);
                box.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 90), 0);
            }
        }
    }

    // ─── LOW-LEVEL HELPERS ───────────────────────────────────────────────────────

    private static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        Undo.RegisterCreatedObjectUndo(go, "Generate Attic");
        return go;
    }

    private static GameObject CreateProBuilderCube(Transform parent, string name, Vector3 size, Vector3 pos, Material mat)
    {
        var pb = ShapeGenerator.CreateShape(ShapeType.Cube);
        pb.gameObject.name   = name;
        pb.transform.SetParent(parent, false);
        pb.transform.localScale    = size;
        pb.transform.localPosition = pos;
        pb.GetComponent<Renderer>().sharedMaterial = mat;
        pb.ToMesh();
        pb.Refresh(RefreshMask.UV);
        Undo.RegisterCreatedObjectUndo(pb.gameObject, "Generate Attic");
        return pb.gameObject;
    }

    private static void CreatePolygon(Transform parent, string name, Material mat, IReadOnlyList<Vector3> pts)
    {
        if (pts == null || pts.Count < 3) return;
        var faces = new List<Face>();
        for (int i = 1; i < pts.Count - 1; i++)
            faces.Add(new Face(new[] { 0, i, i + 1 }));

        var mesh = ProBuilderMesh.Create(pts, faces);
        mesh.gameObject.name = name;
        mesh.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Generate Attic");
        mesh.GetComponent<Renderer>().sharedMaterial = mat;
        mesh.ToMesh();
        mesh.Refresh(RefreshMask.All);
    }

    private static Material LoadMaterial(string pathNoExt)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{pathNoExt}.mat");
        if (mat != null) return mat;
        Debug.LogWarning($"Material at '{pathNoExt}.mat' not found. Using Default-Diffuse.");
        return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
    }
}
#endif