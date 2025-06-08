#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;  // still included for other mesh ops

public static class BasementGenerator
{
    private const string RootName = "Basement_Root";
    private const float SLAB_THICK = 0.1f;
    private const float GROUND_Y   = 0f;

    [MenuItem("Tools/House/Generate Basement")]
    public static void GenerateBasement()
    {
        // ─── Cleanup ────────────────────────────────────────────────────────────────
        var existing = GameObject.Find(RootName);
        if (existing != null)
            Object.DestroyImmediate(existing);

        var root       = new GameObject(RootName);
        var joistsRoot = new GameObject("Joists_Root"); joistsRoot.transform.SetParent(root.transform);
        var wallsRoot  = new GameObject("Walls_Root");  wallsRoot.transform.SetParent(root.transform);
        var floorRoot  = new GameObject("Floor_Root");  floorRoot.transform.SetParent(root.transform);

        // ─── ProBuilder Helper ──────────────────────────────────────────────────────
        ProBuilderMesh CreatePbShape(string name, ShapeType shape, Vector3 pos, Vector3 size, Transform parent, int axisDivs = 8)
        {
            ProBuilderMesh pb;
            if (shape == ShapeType.Cube)
                pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
            else if (shape == ShapeType.Cylinder)
            {
                float radius = size.x * 0.5f;
                float height = size.y;
                pb = ShapeGenerator.GenerateCylinder(PivotLocation.Center, axisDivs, radius, height, 0);
            }
            else
            {
                pb = ShapeGenerator.CreateShape(shape, PivotLocation.Center);
                pb.transform.localScale = size;
            }

            var go = pb.gameObject;
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;

            pb.ToMesh();
            pb.Refresh(RefreshMask.All);
            return pb;
        }

        GameObject CreatePlaceholder(string name, PrimitiveType t, Vector3 pos, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale     = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // ─── 1. Footprint & Walls ───────────────────────────────────────────────────
        CreatePbShape("Floor_Main",
                      ShapeType.Cube,
                      new Vector3(32.5f, GROUND_Y + SLAB_THICK/2f, 15f),
                      new Vector3(55f, SLAB_THICK,     30f),
                      floorRoot.transform);

        CreatePbShape("Floor_West_Front",
                      ShapeType.Cube,
                      new Vector3(2.5f, GROUND_Y + SLAB_THICK/2f, 7f),
                      new Vector3(5f,  SLAB_THICK,     14f),
                      floorRoot.transform);

        const float wallH  = 7f;
        const float wallTh = 0.5f;
        float       wallCY = GROUND_Y + wallH/2f;
        float       sillH  = 4f;
        float       winH   = 1.5f;
        float       winW   = 3f;
        float       winCZ  = 15f;
        float       eastX  = 59.75f;

        // East wall under window
        CreatePbShape("EastWall_BelowWindow",
                      ShapeType.Cube,
                      new Vector3(eastX, GROUND_Y + sillH/2f, 15f),
                      new Vector3(wallTh, sillH, 30f),
                      wallsRoot.transform);

        // East wall header
        float hdrH = wallH - (sillH + winH);
        CreatePbShape("EastWall_AboveWindow",
                      ShapeType.Cube,
                      new Vector3(eastX, GROUND_Y + sillH + winH + hdrH/2f, 15f),
                      new Vector3(wallTh, hdrH, 30f),
                      wallsRoot.transform);

        // East wall left/right of window
        float leftW = winCZ - winW/2f;
        CreatePbShape("EastWall_LeftOfWindow",
                      ShapeType.Cube,
                      new Vector3(eastX, GROUND_Y + sillH + winH/2f, leftW/2f),
                      new Vector3(wallTh, winH, leftW),
                      wallsRoot.transform);

        float rightStart = winCZ + winW/2f;
        float rightW     = 30f - rightStart;
        CreatePbShape("EastWall_RightOfWindow",
                      ShapeType.Cube,
                      new Vector3(eastX, GROUND_Y + sillH + winH/2f, rightStart + rightW/2f),
                      new Vector3(wallTh, winH, rightW),
                      wallsRoot.transform);

        // North, South, West walls
        CreatePbShape("Wall_North",
                      ShapeType.Cube,
                      new Vector3(32.5f, wallCY, 29.75f),
                      new Vector3(55f, wallH,  wallTh),
                      wallsRoot.transform);

        CreatePbShape("Wall_South",
                      ShapeType.Cube,
                      new Vector3(30f, wallCY, 0.25f),
                      new Vector3(60f, wallH,  wallTh),
                      wallsRoot.transform);

        CreatePbShape("Wall_West",
                      ShapeType.Cube,
                      new Vector3(0.25f, wallCY,  7f),
                      new Vector3(wallTh, wallH, 14f),
                      wallsRoot.transform);

        CreatePbShape("Wall_West_Header",
                      ShapeType.Cube,
                      new Vector3(0.25f, GROUND_Y + 6.5f, 22f),
                      new Vector3(wallTh, 1f, 16f),
                      wallsRoot.transform);

        // ─── 2. Stairs & Landing ────────────────────────────────────────────────────
        for (int i = 1; i <= 12; i++)
        {
            // 1) Tread position
            float stepY = GROUND_Y + wallH - (i * (wallH / 12f)) + 0.15f / 2f;
            float stepZ = 30f - ((i - 1) * (16f / 12f)) - ((16f / 12f) / 2f);

            var tread = CreatePbShape(
                $"Step_{i}",
                ShapeType.Cube,
                new Vector3(2.5f, stepY, stepZ),
                new Vector3(5f, 0.15f, 16f / 12f),
                root.transform
            );

            // 2) Riser to fill gap under tread
            float stepHeight = wallH / 12f;
            float gapHeight  = stepHeight - 0.15f;
            float riserY     = stepY - (stepHeight / 2f);
            float riserZ     = stepZ - (16f / 12f) / 2f;

            CreatePbShape(
                $"Riser_{i}",
                ShapeType.Cube,
                new Vector3(2.5f, riserY, riserZ),
                new Vector3(5f, gapHeight, 16f / 12f),
                root.transform
            );

            tread.ToMesh();
            tread.Refresh(RefreshMask.All);
        }

        CreatePbShape("Landing",
                      ShapeType.Cube,
                      new Vector3(2.5f, GROUND_Y + SLAB_THICK/2f, 12.5f),
                      new Vector3(5f, SLAB_THICK, 3f),
                      root.transform);

        // ─── 3. Joists & Utilities ─────────────────────────────────────────────────
        float joistY = GROUND_Y + wallH + 0.8f/2f;
        for (float z = 0.75f; z < 30f; z += 1.5f)
            CreatePbShape($"Joist_{z:F2}",
                          ShapeType.Cube,
                          new Vector3(30f, joistY, z),
                          new Vector3(60f, 0.8f, 0.125f),
                          joistsRoot.transform);

        CreatePbShape("SupportPost",
                      ShapeType.Cylinder,
                      new Vector3(30f, GROUND_Y + wallH/2f, 15f),
                      new Vector3(0.5f, wallH, 0.5f),
                      root.transform,
                      12);

        CreatePbShape("BreakerPanel",
                      ShapeType.Cube,
                      new Vector3(7.5f, GROUND_Y + 4f, 29.45f),
                      new Vector3(1f, 1.5f, 0.1f),
                      root.transform);

        CreatePbShape("WaterHeater",
                      ShapeType.Cylinder,
                      new Vector3(55f, GROUND_Y + 3f, 25f),
                      new Vector3(2f, 6f, 2f),
                      root.transform,
                      16);

        CreatePbShape("SumpPit",
                      ShapeType.Cylinder,
                      new Vector3(55f, GROUND_Y - 0.25f, 25f),
                      new Vector3(1.5f, 0.5f, 1.5f),
                      root.transform,
                      16);

        CreatePbShape("FloorDrain",
                      ShapeType.Cylinder,
                      new Vector3(55f, GROUND_Y + SLAB_THICK - 0.01f, 25f),
                      new Vector3(0.5f, 0.02f, 0.5f),
                      root.transform,
                      12);

        CreatePlaceholder("SumpPumpSwitch",
                          PrimitiveType.Cube,
                          new Vector3(55f, GROUND_Y + 1f, 25f),
                          Vector3.one * 0.2f);

        // lighting placeholders...
        CreatePlaceholder("Bulb_Landing",      PrimitiveType.Sphere, new Vector3( 2.5f, 6.5f, 12.5f), Vector3.one * 0.3f);
        CreatePlaceholder("Pullchain_Landing", PrimitiveType.Cube,   new Vector3( 2.5f, 6.0f, 12.5f), Vector3.one * 0.1f);
        CreatePlaceholder("Bulb_Midroom",      PrimitiveType.Sphere, new Vector3(30f,  6.8f,  8f),    Vector3.one * 0.3f);
        CreatePlaceholder("Pullchain_Midroom", PrimitiveType.Cube,   new Vector3(30f,  6.3f,  8f),    Vector3.one * 0.1f);
        CreatePlaceholder("Bulb_SW",           PrimitiveType.Sphere, new Vector3( 8f,  6.8f, 25f),    Vector3.one * 0.3f);
        CreatePlaceholder("Pullchain_SW",      PrimitiveType.Cube,   new Vector3( 8f,  6.3f, 25f),    Vector3.one * 0.1f);

        // some furniture...
        CreatePbShape("FilingCab_Barricade", ShapeType.Cube, new Vector3(18f, GROUND_Y + 1.25f, 12f), new Vector3(6f, 2.5f, 1f), root.transform);
        CreatePbShape("DeskStack",            ShapeType.Cube, new Vector3(40f, GROUND_Y + 1.5f,  7f), new Vector3(5f, 3f,   2.5f), root.transform);
        CreatePbShape("ChairPile",            ShapeType.Cube, new Vector3(47f, GROUND_Y + 2f,    12f), new Vector3(4f, 4f,   4f  ), root.transform);
        CreatePbShape("Copier",               ShapeType.Cube, new Vector3(35f, GROUND_Y + 1.5f, 23f), new Vector3(3f, 2f,   3f  ), root.transform);

        // ─── Finalize ───────────────────────────────────────────────────────────────
        Selection.activeGameObject = root;
        Debug.Log("🏠 Basement generated successfully!");
    }
}
#endif
