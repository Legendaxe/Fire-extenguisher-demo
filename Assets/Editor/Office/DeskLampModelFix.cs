using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FireExtinguisher.EditorTools
{
    /// <summary>
    /// Repairs the desk lamp model shipped in the lowpoly office pack. In the source FBX the
    /// steel arm and the orange shade are stored detached from the post and from each other, so
    /// every instance looks exploded. This postprocessor re-assembles them into a normal desk
    /// lamp pose whenever the model is imported: the arm is hinged at the top of the post and the
    /// shade hangs from the arm tip, opening down over the desk.
    ///
    /// The repair is mesh data only - the model's pivot, scale, materials, scene instances and
    /// source asset are untouched - and re-importing the FBX always starts from the original
    /// data, so the pass is stable to repeat.
    /// </summary>
    public sealed class DeskLampModelFix : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Lowpoly office assets/desk_lamp.fbx";

        private const string SteelMaterial = "steel";
        private const string ShadeMaterial = "orange";
        private const string GlowMaterial = "glow";

        // Where the arm points from the top of the post, in mesh space: up and forward.
        private static readonly Vector3 ArmPose = new Vector3(0f, 0.72f, 0.69f).normalized;

        // Where the shade's opening points from the arm tip, in mesh space: down and forward.
        private static readonly Vector3 ShadePose = new Vector3(0f, -0.85f, 0.53f).normalized;

        // How far the shade's neck slides down its own axis past the arm tip, so the arm end sits
        // inside the cone instead of poking out of the neck.
        private const float ShadeNeckInset = 0.035f;

        private void OnPostprocessModel(GameObject root)
        {
            if (!assetPath.Replace('\\', '/').Equals(ModelPath, System.StringComparison.OrdinalIgnoreCase))
                return;

            MeshFilter filter = root.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer renderer = root.GetComponentInChildren<MeshRenderer>(true);
            if (filter == null || renderer == null || filter.sharedMesh == null)
                return;

            Mesh mesh = filter.sharedMesh;
            int steel = FindSubmesh(renderer, SteelMaterial);
            int shade = FindSubmesh(renderer, ShadeMaterial);
            int glow = FindSubmesh(renderer, GlowMaterial);
            if (steel < 0 || shade < 0 || glow < 0)
            {
                Debug.LogWarning("DeskLampModelFix: the desk lamp lost its steel/orange/glow submeshes.");
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            bool hasNormals = normals.Length == vertices.Length;

            if (!TryFindArm(vertices, mesh.GetTriangles(steel),
                    out List<int> armVertices, out Vector3 postTop, out Vector3 armJoint, out Vector3 armTip))
                return;

            var shadeVertices = new List<int>();
            CollectVertices(mesh.GetTriangles(shade), shadeVertices);
            var glowVertices = new List<int>();
            CollectVertices(mesh.GetTriangles(glow), glowVertices);

            var shadeAndBulb = new List<int>(shadeVertices);
            shadeAndBulb.AddRange(glowVertices);
            if (!TryFindShade(vertices, shadeAndBulb, out Vector3 narrowEnd, out Vector3 narrowToWide))
                return;

            // Arm: the end that sat near the post top is hinged there, then the arm is turned to
            // point up and out over the desk.
            Vector3 armAxis = armTip - armJoint;
            Quaternion armRotation = Quaternion.FromToRotation(armAxis, ArmPose);
            Vector3 armTipTarget = postTop + ArmPose * armAxis.magnitude;
            for (int i = 0; i < armVertices.Count; i++)
            {
                int index = armVertices[i];
                vertices[index] = postTop + armRotation * (vertices[index] - armJoint);
                if (hasNormals)
                    normals[index] = armRotation * normals[index];
            }

            // Shade: its neck sits just past the arm tip, tilted so its opening faces down over the
            // desk. The arm ends a few centimetres inside the cone, where the shell hides it.
            Vector3 shadeAnchor = armTipTarget - ShadePose * ShadeNeckInset;
            Quaternion shadeRotation = Quaternion.FromToRotation(narrowToWide, ShadePose);
            for (int i = 0; i < shadeVertices.Count; i++)
            {
                int index = shadeVertices[i];
                vertices[index] = shadeAnchor + shadeRotation * (vertices[index] - narrowEnd);
                if (hasNormals)
                    normals[index] = shadeRotation * normals[index];
            }

            // The bulb rides inside the shade, then is centred on the shade's axis.
            for (int i = 0; i < glowVertices.Count; i++)
            {
                int index = glowVertices[i];
                vertices[index] = shadeAnchor + shadeRotation * (vertices[index] - narrowEnd);
                if (hasNormals)
                    normals[index] = shadeRotation * normals[index];
            }

            CenterBulb(vertices, glowVertices, shadeAnchor);

            mesh.vertices = vertices;
            if (hasNormals)
                mesh.normals = normals;
            else
                mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Moves the glowing bulb onto the shade's own axis. The source model stores it off-centre
        /// and it used to poke through the side of the shade; on the axis it stays hidden inside
        /// the cone, where a bulb belongs.
        /// </summary>
        private static void CenterBulb(Vector3[] vertices, List<int> glowVertices, Vector3 shadeAnchor)
        {
            if (glowVertices.Count == 0)
                return;

            var centroid = Vector3.zero;
            for (int i = 0; i < glowVertices.Count; i++)
                centroid += vertices[glowVertices[i]];
            centroid /= glowVertices.Count;

            float along = Vector3.Dot(centroid - shadeAnchor, ShadePose);
            Vector3 shift = shadeAnchor + ShadePose * along - centroid;
            for (int i = 0; i < glowVertices.Count; i++)
                vertices[glowVertices[i]] += shift;
        }

        // ------------------------------------------------------------------ arm

        /// <summary>
        /// Splits the steel submesh into the post and the arm, then returns the arm vertices plus
        /// the post top and the arm's two ends. The post is the steel that sits above the base;
        /// the arm is the steel that reaches the top of the model or stands out in front of it.
        /// </summary>
        private static bool TryFindArm(
            Vector3[] vertices, int[] triangles,
            out List<int> armVertices, out Vector3 postTop,
            out Vector3 armJoint, out Vector3 armTip)
        {
            armVertices = new List<int>();
            postTop = Vector3.zero;
            armJoint = Vector3.zero;
            armTip = Vector3.zero;

            List<Island> islands = BuildIslands(vertices, triangles);
            if (islands.Count < 2)
                return false;

            var steelBounds = new Bounds(vertices[islands[0].Vertices[0]], Vector3.zero);
            for (int i = 0; i < islands.Count; i++)
                steelBounds.Encapsulate(islands[i].Bounds);

            // Relative splits, so this still works if the pack rescales the model: the arm is the
            // steel in the top quarter of the post-and-arm height, or in front of the post.
            float armHeight = Mathf.Lerp(steelBounds.min.y, steelBounds.max.y, 0.75f);
            float armDepth = steelBounds.center.z + steelBounds.extents.z * 0.5f;

            var postPoints = new List<Vector3>();
            var armPoints = new List<Vector3>();
            for (int i = 0; i < islands.Count; i++)
            {
                Island island = islands[i];
                bool isArm = island.Bounds.max.y > armHeight || island.Bounds.center.z > armDepth;
                List<Vector3> target = isArm ? armPoints : postPoints;
                for (int v = 0; v < island.Vertices.Count; v++)
                {
                    int index = island.Vertices[v];
                    target.Add(vertices[index]);
                    if (isArm)
                        armVertices.Add(index);
                }
            }

            if (armPoints.Count == 0 || postPoints.Count == 0)
                return false;

            // The post top is the cap at the highest point of the post.
            float postTopY = float.MinValue;
            for (int i = 0; i < postPoints.Count; i++)
                postTopY = Mathf.Max(postTopY, postPoints[i].y);

            float postTopBand = Mathf.Max(0.002f, (postTopY - steelBounds.min.y) * 0.05f);
            int postTopCount = 0;
            for (int i = 0; i < postPoints.Count; i++)
            {
                if (postPoints[i].y < postTopY - postTopBand)
                    continue;

                postTop += postPoints[i];
                postTopCount++;
            }

            postTop /= postTopCount;

            if (!TryFindAxis(armPoints, out Vector3 armAxis) ||
                !SplitEnds(armPoints, armAxis, out Vector3 firstEnd, out Vector3 secondEnd))
                return false;

            // The end nearer the post top is the hinge; the far end carries the shade.
            if (Vector3.Distance(firstEnd, postTop) <= Vector3.Distance(secondEnd, postTop))
            {
                armJoint = firstEnd;
                armTip = secondEnd;
            }
            else
            {
                armJoint = secondEnd;
                armTip = firstEnd;
            }

            return Vector3.Distance(armJoint, armTip) > 0.001f;
        }

        // ------------------------------------------------------------------ shade

        /// <summary>
        /// Finds the narrow end of the cone-shaped shade and the direction from that end to the
        /// wide opening, which is where the shade attaches and how it must be aimed.
        /// </summary>
        private static bool TryFindShade(
            Vector3[] vertices, List<int> shadeVertices,
            out Vector3 narrowEnd, out Vector3 narrowToWide)
        {
            narrowEnd = Vector3.zero;
            narrowToWide = Vector3.zero;

            var points = new List<Vector3>(shadeVertices.Count);
            for (int i = 0; i < shadeVertices.Count; i++)
                points.Add(vertices[shadeVertices[i]]);

            if (!TryFindAxis(points, out Vector3 axis) ||
                !SplitEnds(points, axis, out Vector3 firstEnd, out Vector3 secondEnd))
                return false;

            if (EndRadius(points, axis, firstEnd) <= EndRadius(points, axis, secondEnd))
            {
                narrowEnd = firstEnd;
                narrowToWide = (secondEnd - firstEnd).normalized;
            }
            else
            {
                narrowEnd = secondEnd;
                narrowToWide = (firstEnd - secondEnd).normalized;
            }

            return narrowToWide.sqrMagnitude > 0.5f;
        }

        private static float EndRadius(List<Vector3> points, Vector3 axis, Vector3 center)
        {
            float band = 0.02f;
            float radius = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (Mathf.Abs(Vector3.Dot(points[i] - center, axis)) > band)
                    continue;

                radius = Mathf.Max(radius, Vector3.Distance(points[i], center));
            }

            return radius;
        }

        // ------------------------------------------------------------------ geometry helpers

        /// <summary>Principal axis of a point cloud, by power iteration on its covariance.</summary>
        private static bool TryFindAxis(List<Vector3> points, out Vector3 axis)
        {
            axis = Vector3.up;
            if (points.Count < 3)
                return false;

            var centroid = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                centroid += points[i];
            centroid /= points.Count;

            var covariance = new double[3, 3];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 offset = points[i] - centroid;
                covariance[0, 0] += offset.x * offset.x;
                covariance[0, 1] += offset.x * offset.y;
                covariance[0, 2] += offset.x * offset.z;
                covariance[1, 0] += offset.y * offset.x;
                covariance[1, 1] += offset.y * offset.y;
                covariance[1, 2] += offset.y * offset.z;
                covariance[2, 0] += offset.z * offset.x;
                covariance[2, 1] += offset.z * offset.y;
                covariance[2, 2] += offset.z * offset.z;
            }

            axis = new Vector3(0.1f, 1f, 1f);
            for (int iteration = 0; iteration < 64; iteration++)
            {
                var next = new Vector3(
                    (float)(covariance[0, 0] * axis.x + covariance[0, 1] * axis.y + covariance[0, 2] * axis.z),
                    (float)(covariance[1, 0] * axis.x + covariance[1, 1] * axis.y + covariance[1, 2] * axis.z),
                    (float)(covariance[2, 0] * axis.x + covariance[2, 1] * axis.y + covariance[2, 2] * axis.z));
                if (next.sqrMagnitude < 1e-12f)
                    return false;

                axis = next.normalized;
            }

            return true;
        }

        /// <summary>Centres of the two extreme cross sections along the given axis.</summary>
        private static bool SplitEnds(List<Vector3> points, Vector3 axis, out Vector3 minEnd, out Vector3 maxEnd)
        {
            minEnd = Vector3.zero;
            maxEnd = Vector3.zero;

            var centroid = Vector3.zero;
            for (int i = 0; i < points.Count; i++)
                centroid += points[i];
            centroid /= points.Count;

            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < points.Count; i++)
            {
                float projection = Vector3.Dot(points[i] - centroid, axis);
                min = Mathf.Min(min, projection);
                max = Mathf.Max(max, projection);
            }

            if (max - min < 0.001f)
                return false;

            float band = (max - min) * 0.08f;
            int minCount = 0;
            int maxCount = 0;
            for (int i = 0; i < points.Count; i++)
            {
                float projection = Vector3.Dot(points[i] - centroid, axis);
                if (Mathf.Abs(projection - min) <= band)
                {
                    minEnd += points[i];
                    minCount++;
                }

                if (Mathf.Abs(projection - max) <= band)
                {
                    maxEnd += points[i];
                    maxCount++;
                }
            }

            if (minCount == 0 || maxCount == 0)
                return false;

            minEnd /= minCount;
            maxEnd /= maxCount;
            return true;
        }

        private sealed class Island
        {
            public readonly List<int> Vertices = new List<int>();
            public Bounds Bounds;
        }

        private static List<Island> BuildIslands(Vector3[] vertices, int[] triangles)
        {
            int[] parent = new int[vertices.Length];
            for (int i = 0; i < parent.Length; i++)
                parent[i] = i;

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Union(parent, triangles[t], triangles[t + 1]);
                Union(parent, triangles[t + 1], triangles[t + 2]);
            }

            var byRoot = new Dictionary<int, Island>();
            for (int t = 0; t < triangles.Length; t++)
            {
                int index = triangles[t];
                int root = Find(parent, index);
                if (!byRoot.TryGetValue(root, out Island island))
                    byRoot[root] = island = new Island();

                if (island.Vertices.Contains(index))
                    continue;

                if (island.Vertices.Count == 0)
                    island.Bounds = new Bounds(vertices[index], Vector3.zero);
                else
                    island.Bounds.Encapsulate(vertices[index]);

                island.Vertices.Add(index);
            }

            return new List<Island>(byRoot.Values);
        }

        private static void CollectVertices(int[] triangles, List<int> target)
        {
            for (int i = 0; i < triangles.Length; i++)
            {
                if (!target.Contains(triangles[i]))
                    target.Add(triangles[i]);
            }
        }

        private static int FindSubmesh(Renderer renderer, string materialName)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null &&
                    materials[i].name.IndexOf(materialName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }

        private static int Find(int[] parent, int index)
        {
            while (parent[index] != index)
            {
                parent[index] = parent[parent[index]];
                index = parent[index];
            }

            return index;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int rootA = Find(parent, a);
            int rootB = Find(parent, b);
            if (rootA != rootB)
                parent[rootB] = rootA;
        }
    }
}
