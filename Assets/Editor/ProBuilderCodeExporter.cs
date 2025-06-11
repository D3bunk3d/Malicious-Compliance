// Assets/Editor/ProBuilderCodeExporter.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

/// <summary>
/// Traverses the selected root, captures every Transform and ProBuilderMesh,
/// and emits one self-contained C# file that can rebuild the entire hierarchy.
/// Tested with Unity 6000.1.6 & ProBuilder 6.0.5.
/// </summary>
public static class ProBuilderCodeExporter
{
    private const string MenuPath = "Tools/ProBuilder/Export Hierarchy To Code";

    [MenuItem(MenuPath, true)]
    private static bool Validate() => Selection.activeGameObject != null;

    [MenuItem(MenuPath)]
    private static void ExportHierarchyToCode()
    {
        Transform root = Selection.activeGameObject?.transform;
        if (root == null)
        {
            EditorUtility.DisplayDialog("ProBuilder Exporter",
                "Select a root GameObject first.", "Ok");
            return;
        }

        // ── gather ─────────────────────────────────────────────────────────────
        var nodes = new List<NodeData>();
        Traverse(root, root, nodes);

        // ── write ──────────────────────────────────────────────────────────────
        string className = $"Generated{Sanitize(root.name)}Builder";
        string path = EditorUtility.SaveFilePanel("Save ProBuilder Script",
                                                  "Assets", className, "cs");
        if (string.IsNullOrEmpty(path)) return;

        File.WriteAllText(path, GenerateScript(nodes, className));
        AssetDatabase.Refresh();

        // highlight new asset
        string assetPath = path.Substring(path.IndexOf("Assets/", StringComparison.Ordinal));
        if (AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath) is { } ms)
        {
            Selection.activeObject = ms;
            EditorGUIUtility.PingObject(ms);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private sealed class NodeData
    {
        public Transform      Tf;
        public string         Var;
        public string         ParentVar;
        public bool           HasMesh;
        public List<Vector3>  Vtx;
        public IList<Face>    Faces;
        public List<Vector2>  UVs;
        public List<Material> Mats;
    }

    private static void Traverse(Transform root, Transform tf, ICollection<NodeData> list)
    {
        var pb = tf.GetComponent<ProBuilderMesh>();

        list.Add(new NodeData
        {
            Tf      = tf,
            HasMesh = pb != null,
            Vtx     = pb ? pb.positions.ToList() : null,
            Faces   = pb ? pb.faces               : null,
            UVs     = pb ? pb.textures.ToList()   : null,
            Mats    = pb ? pb.GetComponent<Renderer>().sharedMaterials.ToList()
                         : new List<Material>()
        });

        foreach (Transform c in tf) Traverse(root, c, list);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static string F(float v) => v.ToString("F6", CultureInfo.InvariantCulture);

    private static string GenerateScript(IReadOnlyList<NodeData> nodes, string className)
    {
        // unique variable names & parent links
        for (int i = 0; i < nodes.Count; i++)
            nodes[i].Var = $"go_{Sanitize(RelPath(nodes[0].Tf, nodes[i].Tf))}_{i}";

        foreach (var n in nodes)
            n.ParentVar = n == nodes[0] ? null
                         : nodes.First(p => p.Tf == n.Tf.parent).Var;

        var sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.ProBuilder;");
        sb.AppendLine();
        sb.AppendLine($"public class {className} : MonoBehaviour");
        sb.AppendLine("{");
        sb.AppendLine("    private bool _built;");
        sb.AppendLine("    void Reset ()  { if (_built) return; Build(); _built = true; }");
        sb.AppendLine("    void Awake ()  { if (Application.isPlaying) Build(); }");
        sb.AppendLine();
        sb.AppendLine("    private void Build()");
        sb.AppendLine("    {");

        // ---------- GameObjects ----------
        sb.AppendLine("        // ---------- GameObjects ----------");
        foreach (var n in nodes)
        {
            Vector3 p = n.Tf.localPosition;
            Quaternion r = n.Tf.localRotation;
            Vector3 s = n.Tf.localScale;
            string parent = n.ParentVar == null ? "transform" : $"{n.ParentVar}.transform";

            sb.AppendLine($"        var {n.Var} = new GameObject(\"{n.Tf.name}\");");
            sb.AppendLine($"        {n.Var}.transform.parent        = {parent};");
            sb.AppendLine($"        {n.Var}.transform.localPosition = new Vector3({F(p.x)}f,{F(p.y)}f,{F(p.z)}f);");
            sb.AppendLine($"        {n.Var}.transform.localRotation = new Quaternion({F(r.x)}f,{F(r.y)}f,{F(r.z)}f,{F(r.w)}f);");
            sb.AppendLine($"        {n.Var}.transform.localScale    = new Vector3({F(s.x)}f,{F(s.y)}f,{F(s.z)}f);");
            sb.AppendLine();
        }

        // ---------- ProBuilder Meshes ----------
        sb.AppendLine("        // ---------- ProBuilder Meshes ----------");
        foreach (var n in nodes.Where(m => m.HasMesh))
        {
            string pb = $"pb_{n.Var}";
            sb.AppendLine($"        var {pb} = {n.Var}.AddComponent<ProBuilderMesh>();");

            // vertices
            sb.AppendLine($"        {pb}.positions = new List<Vector3>");
            sb.AppendLine("        {");
            foreach (var v in n.Vtx)
                sb.AppendLine($"            new Vector3({F(v.x)}f,{F(v.y)}f,{F(v.z)}f),");
            sb.AppendLine("        };");

            // faces
            sb.AppendLine($"        {pb}.faces = new List<Face>");
            sb.AppendLine("        {");
            foreach (var f in n.Faces)
                sb.AppendLine($"            new Face(new[] {{ {string.Join(", ", f.indexes)} }}) {{ smoothingGroup = {f.smoothingGroup} }},");
            sb.AppendLine("        };");

            // uvs
            sb.AppendLine($"        {pb}.textures = new List<Vector2>");
            sb.AppendLine("        {");
            foreach (var uv in n.UVs)
                sb.AppendLine($"            new Vector2({F(uv.x)}f,{F(uv.y)}f),");
            sb.AppendLine("        };");

            // original materials (kept for completeness, but will be overridden)
            if (n.Mats.Count > 0)
            {
                string matVar = $"mats_{n.Var}";
                sb.AppendLine($"        var {matVar} = new List<Material>();");
                foreach (var m in n.Mats)
                    sb.AppendLine($"        {matVar}.Add(Resources.Load<Material>(\"{m?.name}\"));");
                sb.AppendLine($"        {pb}.GetComponent<Renderer>().sharedMaterials = {matVar}.ToArray();");
            }

            // build shared indices before refreshing
            sb.AppendLine($"        {pb}.sharedVertices = SharedVertex.GetSharedVerticesWithPositions({pb}.positions);");

            sb.AppendLine($"        {pb}.ToMesh();");
            sb.AppendLine($"        {pb}.Refresh();");

            // ── assign ProBuilderDefault material ──────────────────────────────
            sb.AppendLine($"        {pb}.GetComponent<Renderer>().sharedMaterial = Resources.Load<Material>(\"Materials/ProBuilderDefault\");");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private static string RelPath(Transform root, Transform t)
    {
        var stack = new Stack<string>();
        for (var c = t; c != null && c != root.parent; c = c.parent) stack.Push(c.name);
        return string.Join("_", stack);
    }

    private static string Sanitize(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (char c in raw) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }
}
#endif
