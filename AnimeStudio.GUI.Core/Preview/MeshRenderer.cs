using System;
using System.IO;
using OpenTK.Graphics.OpenGL;

namespace AnimeStudio.GUI.Core.Preview
{
    using Matrix4 = OpenTK.Mathematics.Matrix4;
    using Vector3 = OpenTK.Mathematics.Vector3;
    using Vector4 = OpenTK.Mathematics.Vector4;

    // Draws a MeshData with the current GL context. The host control owns the context and must make it
    // current before calling Initialize, SetMesh, Render and Dispose.
    public sealed class MeshRenderer : IDisposable
    {
        private const int PositionLocation = 0;
        private const int NormalLocation = 1;
        private const int ColorLocation = 2;

        private ShaderProgram shaded, colored, black;
        private int vao, positionBuffer, normalBuffer, colorBuffer, indexBuffer;
        private int indexCount;
        private MeshData mesh;
        private Matrix4 projection = Matrix4.Identity;

        // 0 fill, 1 wireframe, 2 fill + wireframe
        public int WireframeMode { get; private set; }
        // 0 lit, 1 vertex colors
        public int ShadeMode { get; private set; }
        // 0 computed normals, 1 normals from the asset
        public int NormalMode { get; private set; }
        public Matrix4 View { get; set; }
        public bool HasMesh => mesh != null;

        public void Initialize(string versionHeader = "#version 140")
        {
            var vertex = LoadShader("vertex", versionHeader);
            shaded = new ShaderProgram(vertex, LoadShader("fragment", versionHeader));
            colored = new ShaderProgram(vertex, LoadShader("fragmentColor", versionHeader));
            black = new ShaderProgram(vertex, LoadShader("fragmentBlack", versionHeader));
            GL.ClearColor(0.373f, 0.620f, 0.627f, 1f);
            ResetView();
        }

        public void ResetView() => View = Matrix4.CreateRotationY(-MathF.PI / 4) * Matrix4.CreateRotationX(-MathF.PI / 6);

        public void SetMesh(MeshData data)
        {
            DeleteBuffers();
            mesh = data;
            if (data == null)
                return;

            ResetView();
            vao = GL.GenVertexArray();
            GL.BindVertexArray(vao);
            positionBuffer = Upload(data.Positions, PositionLocation, 3);
            normalBuffer = Upload(CurrentNormals, NormalLocation, 3);
            colorBuffer = Upload(data.Colors, ColorLocation, 4);
            indexBuffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBuffer);
            GL.BufferData(BufferTarget.ElementArrayBuffer, data.Indices.Length * sizeof(int), data.Indices, BufferUsageHint.StaticDraw);
            indexCount = data.Indices.Length;
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        public void CycleWireframe() => WireframeMode = (WireframeMode + 1) % 3;

        public void ToggleShade() => ShadeMode = (ShadeMode + 1) % 2;

        public void ToggleNormals()
        {
            NormalMode = (NormalMode + 1) % 2;
            if (mesh == null)
                return;
            var normals = CurrentNormals;
            GL.BindBuffer(BufferTarget.ArrayBuffer, normalBuffer);
            GL.BufferData(BufferTarget.ArrayBuffer, normals.Length * Vector3.SizeInBytes, normals, BufferUsageHint.StaticDraw);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        }

        private Vector3[] CurrentNormals => NormalMode == 1 && mesh.Normals != null ? mesh.Normals : mesh.ComputedNormals;

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return;
            GL.Viewport(0, 0, width, height);
            projection = width <= height
                ? Matrix4.CreateScale(1, (float)width / height, 1)
                : Matrix4.CreateScale((float)height / width, 1, 1);
        }

        public void Rotate(float dx, float dy) => View *= Matrix4.CreateRotationX(dy * 0.01f) * Matrix4.CreateRotationY(dx * 0.01f);

        public void Pan(float dx, float dy) => View *= Matrix4.CreateTranslation(-dx * 0.003f, dy * 0.003f, 0);

        public void Zoom(float wheelDelta) => View *= Matrix4.CreateScale(1 + wheelDelta / 1000f);

        public void Render()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            if (mesh == null)
                return;

            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.BindVertexArray(vao);
            var model = mesh.ModelMatrix;
            if (WireframeMode != 1)
            {
                (ShadeMode == 0 ? shaded : colored).Use(model, View, projection);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
            }
            if (WireframeMode != 0)
            {
                GL.Enable(EnableCap.PolygonOffsetLine);
                GL.PolygonOffset(-1, -1);
                black.Use(model, View, projection);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(PrimitiveType.Triangles, indexCount, DrawElementsType.UnsignedInt, 0);
                GL.Disable(EnableCap.PolygonOffsetLine);
            }
            GL.BindVertexArray(0);
        }

        private static int Upload<T>(T[] data, int location, int components) where T : struct
        {
            var buffer = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, buffer);
            GL.BufferData(BufferTarget.ArrayBuffer, data.Length * components * sizeof(float), data, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(location, components, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(location);
            return buffer;
        }

        private void DeleteBuffers()
        {
            if (vao == 0)
                return;
            GL.DeleteBuffer(positionBuffer);
            GL.DeleteBuffer(normalBuffer);
            GL.DeleteBuffer(colorBuffer);
            GL.DeleteBuffer(indexBuffer);
            GL.DeleteVertexArray(vao);
            vao = positionBuffer = normalBuffer = colorBuffer = indexBuffer = 0;
            indexCount = 0;
        }

        public void Dispose()
        {
            DeleteBuffers();
            shaded?.Dispose();
            colored?.Dispose();
            black?.Dispose();
            shaded = colored = black = null;
            mesh = null;
        }

        private static string LoadShader(string name, string versionHeader)
        {
            using var stream = typeof(MeshRenderer).Assembly.GetManifestResourceStream($"AnimeStudio.GUI.Core.Preview.Shaders.{name}.glsl");
            using var reader = new StreamReader(stream);
            return versionHeader + "\n" + reader.ReadToEnd();
        }

        private sealed class ShaderProgram : IDisposable
        {
            private readonly int id;
            private readonly int model, view, proj;

            public ShaderProgram(string vertexSource, string fragmentSource)
            {
                id = GL.CreateProgram();
                var vs = Compile(ShaderType.VertexShader, vertexSource);
                var fs = Compile(ShaderType.FragmentShader, fragmentSource);
                GL.AttachShader(id, vs);
                GL.AttachShader(id, fs);
                GL.BindAttribLocation(id, PositionLocation, "vertexPosition");
                GL.BindAttribLocation(id, NormalLocation, "normalDirection");
                GL.BindAttribLocation(id, ColorLocation, "vertexColor");
                GL.LinkProgram(id);
                GL.GetProgram(id, GetProgramParameterName.LinkStatus, out var linked);
                if (linked == 0)
                    Logger.Error($"Shader link failed: {GL.GetProgramInfoLog(id)}");
                GL.DetachShader(id, vs);
                GL.DetachShader(id, fs);
                GL.DeleteShader(vs);
                GL.DeleteShader(fs);
                model = GL.GetUniformLocation(id, "modelMatrix");
                view = GL.GetUniformLocation(id, "viewMatrix");
                proj = GL.GetUniformLocation(id, "projMatrix");
            }

            private static int Compile(ShaderType type, string source)
            {
                var shader = GL.CreateShader(type);
                GL.ShaderSource(shader, source);
                GL.CompileShader(shader);
                GL.GetShader(shader, ShaderParameter.CompileStatus, out var ok);
                if (ok == 0)
                    Logger.Error($"{type} compile failed: {GL.GetShaderInfoLog(shader)}");
                return shader;
            }

            public void Use(Matrix4 modelMatrix, Matrix4 viewMatrix, Matrix4 projMatrix)
            {
                GL.UseProgram(id);
                GL.UniformMatrix4(model, false, ref modelMatrix);
                GL.UniformMatrix4(view, false, ref viewMatrix);
                GL.UniformMatrix4(proj, false, ref projMatrix);
            }

            public void Dispose() => GL.DeleteProgram(id);
        }
    }
}
