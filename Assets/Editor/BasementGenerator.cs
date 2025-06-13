#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;  // for DeleteElements

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
        
        // ADDED: Fill the floor where the old stairs and landing were.
        CreatePbShape("Floor_West_Back_Fill",
                      ShapeType.Cube,
                      new Vector3(2.5f, GROUND_Y + SLAB_THICK/2f, 22f),
                      new Vector3(5f, SLAB_THICK, 16f),
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

        // North & South walls
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

        // MODIFIED: Create a single, solid west wall to close the old opening.
        CreatePbShape("Wall_West",
                      ShapeType.Cube,
                      new Vector3(0.25f, wallCY,  15f),
                      new Vector3(wallTh, wallH, 30f),
                      wallsRoot.transform);

        // ─── 2. Stairs & Landing ────────────────────────────────────────────────────
        const int   stairCount = 12;    // Number of steps
        const float stairRun   = 12f;   // Total horizontal length the stairs will cover
        const float stairWidth = 4f;    // The width of the staircase
        const float stairTopX  = 58f;   // X-coordinate for the top of the stairs
        const float stairZPos  = 2.75f; // Z-coordinate for the centerline of the stairs

        for (int i = 1; i <= stairCount; i++)
        {
            float riserHeight = wallH / stairCount;
            float treadThick  = 0.15f;
            float stepY       = GROUND_Y + wallH - (i * riserHeight) + treadThick / 2f;

            // Decrement position along the X-axis for each step
            float treadDepth = stairRun / stairCount;
            float stepX      = stairTopX - ((i - 1) * treadDepth) - (treadDepth / 2f);

            // Note the swapped X and Z values for the size vector
            var tread = CreatePbShape($"Step_{i}",
                                      ShapeType.Cube,
                                      new Vector3(stepX, stepY, stairZPos),
                                      new Vector3(treadDepth, treadThick, stairWidth),
                                      root.transform);

            // Delete the riser face, which now points along the positive X-axis (Vector3.right)
            var riserFace = tread.faces.FirstOrDefault(f =>
            {
                int vi = f.distinctIndexes[0];
                Vector3 nrm = tread.normals[vi];
                return Vector3.Dot(nrm, Vector3.right) > 0.99f;
            });

            if (riserFace != null)
            {
                DeleteElements.DeleteFaces(tread, new[] { riserFace });
                tread.ToMesh();
                tread.Refresh(RefreshMask.All);
            }
        }

        // Create the landing at the bottom of the new stair position
        float landingDepth = 3f;
        float lastTreadX   = stairTopX - stairRun;
        float landingX     = lastTreadX - (landingDepth / 2f);

        CreatePbShape("Landing",
                      ShapeType.Cube,
                      new Vector3(landingX, GROUND_Y + SLAB_THICK/2f, stairZPos),
                      new Vector3(landingDepth, SLAB_THICK, stairWidth),
                      root.transform);

        // ─── 3. Joists & Utilities ─────────────────────────────────────────────────

        float joistY         = GROUND_Y + wallH + 0.8f/2f;
        float joistHeight    = 0.8f;
        float joistThickness = 0.125f;

        // Define the opening for the new stairs.
        float openingStartX = 45f;
        float openingEndX   = 58.5f;
        float openingStartZ = 0.6f;
        float openingEndZ   = 4.9f;

        // Create Trimmer joists to frame the opening along the X-axis.
        float openingLength = openingEndX - openingStartX;
        float trimmerX      = openingStartX + openingLength / 2f;
        CreatePbShape("Joist_Trimmer_1", ShapeType.Cube, new Vector3(trimmerX, joistY, openingStartZ), new Vector3(openingLength, joistHeight, joistThickness), joistsRoot.transform);
        CreatePbShape("Joist_Trimmer_2", ShapeType.Cube, new Vector3(trimmerX, joistY, openingEndZ),   new Vector3(openingLength, joistHeight, joistThickness), joistsRoot.transform);
        
        // Create the main joists.
        for (float z = 0.75f; z < 30f; z += 1.5f)
        {
            // If a joist is within the Z-range of the opening, don't generate it.
            // The trimmers have already framed this area.
            if (z > openingStartZ && z < openingEndZ)
            {
                // Create a shorter joist to the left of the opening.
                float length = openingStartX;
                CreatePbShape($"Joist_{z:F2}_A", ShapeType.Cube, new Vector3(length / 2f, joistY, z), new Vector3(length, joistHeight, joistThickness), joistsRoot.transform);

                // Create a shorter joist to the right of the opening.
                float startX  = openingEndX;
                float endX    = 60f;
                length = endX - startX;
                CreatePbShape($"Joist_{z:F2}_B", ShapeType.Cube, new Vector3(startX + length / 2f, joistY, z), new Vector3(length, joistHeight, joistThickness), joistsRoot.transform);
            }
            else
            {
                // Joist is outside the opening, create a full-length one.
                CreatePbShape($"Joist_{z:F2}",
                              ShapeType.Cube,
                              new Vector3(30f, joistY, z),
                              new Vector3(60f, joistHeight, joistThickness),
                              joistsRoot.transform);
            }
        }
        
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

        // lighting placeholders
        CreatePlaceholder("Bulb_Landing",      PrimitiveType.Sphere, new Vector3( 2.5f, 6.5f, 12.5f), Vector3.one * 0.3f);
        CreatePlaceholder("Pullchain_Landing", PrimitiveType.Cube,   new Vector3( 2.5f, 6.0f, 12.5f), Vector3.one * 0.1f);
        CreatePlaceholder("Bulb_Midroom",      PrimitiveType.Sphere, new Vector3(30f,  6.8f,  8f),    Vector3.one * 0.3f);
        CreatePlaceholder("Pullchain_Midroom", PrimitiveType.Cube,   new Vector3(30f,  6.3f,  8f),    Vector3.one * 0.1f);
        CreatePlaceholder("Bulb_SW",           PrimitiveType.Sphere, new Vector3( 8f,  6.8f, 25f),    Vector3.one * 0.3f);
        CreatePlaceholder("Pullchain_SW",      PrimitiveType.Cube,   new Vector3( 8f,  6.3f, 25f),    Vector3.one * 0.1f);

        // some furniture
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