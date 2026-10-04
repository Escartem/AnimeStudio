using System;
using System.Linq;

namespace AnimeStudio.GUI.Core.Preview
{
    using Matrix4 = OpenTK.Mathematics.Matrix4;
    using Vector3 = OpenTK.Mathematics.Vector3;
    using Vector4 = OpenTK.Mathematics.Vector4;

    public sealed class MeshData
    {
        public Vector3[] Positions { get; private init; }
        // Normals stored in the asset, null when it has none.
        public Vector3[] Normals { get; private init; }
        // Normals averaged from the faces, used by default and as fallback.
        public Vector3[] ComputedNormals { get; private init; }
        public Vector4[] Colors { get; private init; }
        public int[] Indices { get; private init; }
        // Centers the model and scales it to fit a unit box.
        public Matrix4 ModelMatrix { get; private init; }

        public long Cost => Positions.Length * (12L * 2 + 16) + (Normals?.Length ?? 0) * 12L + Indices.Length * 4L;

        public static MeshData FromMesh(Mesh m_Mesh)
        {
            if (m_Mesh.m_VertexCount <= 0 || m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0)
                return null;

            var vertexCount = m_Mesh.m_VertexCount;
            var positions = new Vector3[vertexCount];
            var stride = m_Mesh.m_Vertices.Length == vertexCount * 4 ? 4 : 3;
            for (int v = 0; v < vertexCount; v++)
                positions[v] = new Vector3(m_Mesh.m_Vertices[v * stride], m_Mesh.m_Vertices[v * stride + 1], m_Mesh.m_Vertices[v * stride + 2]);

            var indices = new int[m_Mesh.m_Indices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = (int)m_Mesh.m_Indices[i];

            Vector3[] normals = null;
            if (m_Mesh.m_Normals?.Length > 0)
            {
                var normalStride = m_Mesh.m_Normals.Length == vertexCount * 4 ? 4 : 3;
                normals = new Vector3[vertexCount];
                for (int n = 0; n < vertexCount; n++)
                    normals[n] = new Vector3(m_Mesh.m_Normals[n * normalStride], m_Mesh.m_Normals[n * normalStride + 1], m_Mesh.m_Normals[n * normalStride + 2]);
            }

            var colors = new Vector4[vertexCount];
            if (m_Mesh.m_Colors != null && (m_Mesh.m_Colors.Length == vertexCount * 3 || m_Mesh.m_Colors.Length == vertexCount * 4))
            {
                var colorStride = m_Mesh.m_Colors.Length / vertexCount;
                for (int c = 0; c < vertexCount; c++)
                {
                    var o = c * colorStride;
                    colors[c] = new Vector4(m_Mesh.m_Colors[o], m_Mesh.m_Colors[o + 1], m_Mesh.m_Colors[o + 2], colorStride == 4 ? m_Mesh.m_Colors[o + 3] : 1f);
                }
            }
            else
            {
                Array.Fill(colors, new Vector4(0.5f, 0.5f, 0.5f, 1f));
            }

            return Create(positions, normals, colors, indices);
        }

        public static MeshData FromModel(ModelConverter model)
        {
            if (model.MeshList.Count == 0)
                return null;

            var vertices = model.MeshList.SelectMany(x => x.VertexList).ToArray();
            var positions = vertices.Select(x => new Vector3(x.Vertex.X, x.Vertex.Y, x.Vertex.Z)).ToArray();
            var normals = vertices.Select(x => new Vector3(x.Normal.X, x.Normal.Y, x.Normal.Z)).ToArray();
            var colors = vertices.Select(x => new Vector4(x.Color.R, x.Color.G, x.Color.B, x.Color.A)).ToArray();

            var indices = new System.Collections.Generic.List<int>();
            var meshOffset = 0;
            foreach (var mesh in model.MeshList)
            {
                foreach (var submesh in mesh.SubmeshList)
                    foreach (var face in submesh.FaceList)
                        foreach (var index in face.VertexIndices)
                            indices.Add(submesh.BaseVertex + index + meshOffset);
                meshOffset += mesh.VertexList.Count;
            }

            return Create(positions, normals, colors, indices.ToArray());
        }

        private static MeshData Create(Vector3[] positions, Vector3[] normals, Vector4[] colors, int[] indices)
        {
            var min = positions[0];
            var max = positions[0];
            foreach (var p in positions)
            {
                min = Vector3.ComponentMin(min, p);
                max = Vector3.ComponentMax(max, p);
            }
            var size = Math.Max(1e-5f, (max - min).Length);
            var center = (max + min) / 2;

            return new MeshData
            {
                Positions = positions,
                Normals = normals,
                ComputedNormals = ComputeNormals(positions, indices),
                Colors = colors,
                Indices = indices,
                ModelMatrix = Matrix4.CreateTranslation(-center) * Matrix4.CreateScale(2f / size),
            };
        }

        private static Vector3[] ComputeNormals(Vector3[] positions, int[] indices)
        {
            var normals = new Vector3[positions.Length];
            var counts = new int[positions.Length];
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                var a = indices[i];
                var b = indices[i + 1];
                var c = indices[i + 2];
                var normal = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                normal.Normalize();
                normals[a] += normal; counts[a]++;
                normals[b] += normal; counts[b]++;
                normals[c] += normal; counts[c]++;
            }
            for (int i = 0; i < normals.Length; i++)
                normals[i] = counts[i] == 0 ? Vector3.UnitY : normals[i] / counts[i];
            return normals;
        }
    }
}
