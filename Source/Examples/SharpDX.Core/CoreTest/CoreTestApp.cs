﻿//#define TESTADDREMOVE

using HelixToolkit.SharpDX.Core.Controls;
using HelixToolkit.SharpDX.Core.Model;
using HelixToolkit.UWP;
using HelixToolkit.UWP.Cameras;
using HelixToolkit.UWP.Core;
using HelixToolkit.UWP.Model;
using HelixToolkit.UWP.Model.Scene;
using HelixToolkit.UWP.Shaders;
using ImGuiNET;
using SharpDX;
using SharpDX.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CoreTest
{
    public class CoreTestApp
    {
        private readonly ViewportCore viewport;
        private readonly Form window;
        private readonly EffectsManager effectsManager;
        private CameraCore camera;
        private Geometry3D box, sphere, points, lines;
        private GroupNode groupSphere, groupBox, groupPoints, groupLines;
        private const int NumItems = 40;
        private Random rnd = new Random((int)Stopwatch.GetTimestamp());
        private Dictionary<string, MaterialCore> materials = new Dictionary<string, MaterialCore>();
        private MaterialCore[] materialList;
        private long previousTime;
        private bool resizeRequested = false;
        private IO io = ImGui.GetIO();
        private CameraController cameraController;

        public CoreTestApp(Form window)
        {
            viewport = new ViewportCore(window.Handle);
            cameraController = new CameraController(viewport);
            this.window = window;
            window.ResizeEnd += Window_ResizeEnd;
            window.Load += Window_Load;
            window.FormClosing += Window_FormClosing;
            window.MouseMove += Window_MouseMove;
            window.MouseDown += Window_MouseDown;
            window.MouseUp += Window_MouseUp;
            window.MouseWheel += Window_MouseWheel;
            window.KeyDown += Window_KeyDown;
            window.KeyUp += Window_KeyUp;
            window.KeyPress += Window_KeyPress;
            effectsManager = new DefaultEffectsManager();
            effectsManager.AddTechnique(ImGuiNode.RenderTechnique);
            viewport.EffectsManager = effectsManager;           
            viewport.OnStartRendering += Viewport_OnStartRendering;
            viewport.OnStopRendering += Viewport_OnStopRendering;
            viewport.OnErrorOccurred += Viewport_OnErrorOccurred;
            //viewport.FXAALevel = FXAALevel.Low;
            viewport.RenderHost.EnableRenderFrustum = false;
            viewport.RenderHost.RenderConfiguration.EnableRenderOrder = true;
            viewport.BackgroundColor = new Color4(0.45f, 0.55f, 0.6f, 1f);
            InitializeScene();
        }



        private void InitializeScene()
        {
            camera = new PerspectiveCameraCore()
            {
                LookDirection = new Vector3(0, 0, 50),
                Position = new Vector3(0, 0, -50),
                FarPlaneDistance = 1000,
                NearPlaneDistance = 0.1f,
                FieldOfView = 45,
                UpDirection = new Vector3(0, 1, 0)
            };
            viewport.CameraCore = camera;
            viewport.Items.Add(new DirectionalLightNode() { Direction = new Vector3(0, -1, 1), Color = Color.White });
            viewport.Items.Add(new PointLightNode() { Position = new Vector3(0, 0, -20), Color = Color.Yellow, Range = 20, Attenuation = Vector3.One });

            var builder = new MeshBuilder(true, true, true);
            builder.AddSphere(Vector3.Zero, 1, 12, 12);
            sphere = builder.ToMesh();
            builder = new MeshBuilder(true, true, true);
            builder.AddBox(Vector3.Zero, 1, 1, 1);
            box = builder.ToMesh();
            points = new PointGeometry3D() { Positions = sphere.Positions };
            var lineBuilder = new LineBuilder();
            lineBuilder.AddBox(Vector3.Zero, 2, 2, 2);
            lines = lineBuilder.ToLineGeometry3D();
            groupSphere = new GroupNode();
            groupBox = new GroupNode();
            groupLines = new GroupNode();
            groupPoints = new GroupNode();
            InitializeMaterials();
            materialList = materials.Values.ToArray();
            var materialCount = materialList.Length;

            for (int i = 0; i < NumItems; ++i)
            {
                var transform = Matrix.Translation(new Vector3(rnd.NextFloat(-20, 20), rnd.NextFloat(-20, 20), rnd.NextFloat(-20, 20)));
                groupSphere.AddChildNode(new MeshNode() { Geometry = sphere, Material = materialList[i % materialCount], ModelMatrix = transform, CullMode = SharpDX.Direct3D11.CullMode.Back });
            }

            for (int i = 0; i < NumItems; ++i)
            {
                var transform = Matrix.Translation(new Vector3(rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50)));
                groupBox.AddChildNode(new MeshNode() { Geometry = box, Material = materialList[i % materialCount], ModelMatrix = transform, CullMode = SharpDX.Direct3D11.CullMode.Back });
            }

            //for(int i=0; i< NumItems; ++i)
            //{
            //    var transform = Matrix.Translation(new Vector3(rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50)));
            //    groupPoints.AddChildNode(new PointNode() { Geometry = points, ModelMatrix = transform, Material = new PointMaterialCore() { PointColor = Color.Red } });
            //}

            //for (int i = 0; i < NumItems; ++i)
            //{
            //    var transform = Matrix.Translation(new Vector3(rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50)));
            //    groupLines.AddChildNode(new LineNode() { Geometry = lines, ModelMatrix = transform, Material = new LineMaterialCore() { LineColor = Color.LightBlue } });
            //}

            viewport.Items.Add(groupSphere);
            groupSphere.AddChildNode(groupBox);
            groupSphere.AddChildNode(groupPoints);
            groupSphere.AddChildNode(groupLines);

            var viewbox = new ViewBoxNode();
            viewport.Items.Add(viewbox);
            var imGui = new ImGuiNode();
            viewport.Items.Add(imGui);
            imGui.UpdatingImGuiUI += ImGui_UpdatingImGuiUI;
            io.KeyMap[GuiKey.Tab] = (int)Keys.Tab;
            io.KeyMap[GuiKey.LeftArrow] = (int)Keys.Left;
            io.KeyMap[GuiKey.RightArrow] = (int)Keys.Right;
            io.KeyMap[GuiKey.UpArrow] = (int)Keys.Up;
            io.KeyMap[GuiKey.DownArrow] = (int)Keys.Down;
            io.KeyMap[GuiKey.PageUp] = (int)Keys.PageUp;
            io.KeyMap[GuiKey.PageDown] = (int)Keys.PageDown;
            io.KeyMap[GuiKey.Home] = (int)Keys.Home;
            io.KeyMap[GuiKey.End] = (int)Keys.End;
            io.KeyMap[GuiKey.Delete] = (int)Keys.Delete;
            io.KeyMap[GuiKey.Backspace] = (int)Keys.Back;
            io.KeyMap[GuiKey.Enter] = (int)Keys.Enter;
            io.KeyMap[GuiKey.Escape] = (int)Keys.Escape;
        }

        private void ImGui_UpdatingImGuiUI(object sender, EventArgs e)
        {
            bool open = true;
            ImGuiNative.igShowDemoWindow(ref open);
        }

        private void InitializeMaterials()
        {
            var diffuse = new MemoryStream();
            using (var fs = File.Open("TextureCheckerboard2.jpg", FileMode.Open))
            {
                fs.CopyTo(diffuse);
            }

            var normal = new MemoryStream();
            using (var fs = File.Open("TextureCheckerboard2_dot3.jpg", FileMode.Open))
            {
                fs.CopyTo(normal);
            }
            materials.Add("red", new DiffuseMaterialCore() { DiffuseColor = Color.Red, DiffuseMap = diffuse });
            materials.Add("green", new DiffuseMaterialCore() { DiffuseColor = Color.Green, DiffuseMap = diffuse });
            materials.Add("blue", new DiffuseMaterialCore() { DiffuseColor = Color.Blue, DiffuseMap = diffuse });
            materials.Add("DodgerBlue", new PhongMaterialCore() { DiffuseColor = Color.DodgerBlue, ReflectiveColor = Color.DarkGray, SpecularShininess = 10, SpecularColor = Color.Red, DiffuseMap = diffuse, NormalMap = normal });
            materials.Add("Orange", new PhongMaterialCore() { DiffuseColor = Color.Orange, ReflectiveColor = Color.DarkGray, SpecularShininess = 10, SpecularColor = Color.Red, DiffuseMap = diffuse, NormalMap = normal });
            materials.Add("PaleGreen", new PhongMaterialCore() { DiffuseColor = Color.PaleGreen, ReflectiveColor = Color.DarkGray, SpecularShininess = 10, SpecularColor = Color.Red, DiffuseMap = diffuse, NormalMap = normal });
            materials.Add("normal", new NormalMaterialCore());
        }

        private void Viewport_OnErrorOccurred(object sender, Exception e)
        {

        }

        private void Viewport_OnStopRendering(object sender, EventArgs e)
        {
            
        }

        private void Viewport_OnStartRendering(object sender, EventArgs e)
        {
            bool isGoingOut = true;
            bool isAddingNode = false;
            RenderLoop.Run(window, () => 
            {
                if (resizeRequested)
                {
                    viewport.Resize(window.ClientSize.Width, window.ClientSize.Height);
                    resizeRequested = false;
                    return;
                }
                var pos = camera.Position;
                var t = Stopwatch.GetTimestamp();
                var elapse = t - previousTime;
                previousTime = t;
                cameraController.OnTimeStep();

                viewport.Render();

                
#if TESTADDREMOVE
                if (groupSphere.Items.Count > 0 && !isAddingNode)
                {
                    groupSphere.RemoveChildNode(groupSphere.Items.First());
                    if (groupSphere.Items.Count == 0)
                    {
                        isAddingNode = true;
                        Console.WriteLine($"{effectsManager.GetResourceCountSummary()}");
                        groupSphere.AddChildNode(groupBox);
                        groupSphere.AddChildNode(groupPoints);
                        groupPoints.AddChildNode(groupLines);
                    }
                }
                else
                {
                    var materialCount = materialList.Length;
                    var transform = Matrix.Translation(new Vector3(rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50), rnd.NextFloat(-50, 50)));
                    groupSphere.AddChildNode(new MeshNode() { Geometry = box, Material = materialList[groupSphere.Items.Count % materialCount], ModelMatrix = transform, CullMode = SharpDX.Direct3D11.CullMode.Back });
                    transform = Matrix.Translation(new Vector3(rnd.NextFloat(-20, 20), rnd.NextFloat(-20, 20), rnd.NextFloat(-20, 20)));
                    groupSphere.AddChildNode(new MeshNode() { Geometry = sphere, Material = materialList[groupSphere.Items.Count % materialCount], ModelMatrix = transform, CullMode = SharpDX.Direct3D11.CullMode.Back });
                    if (groupSphere.Items.Count > NumItems)
                    {
                        isAddingNode = false;
                    }
                }
#endif
            });
        }

        private void Window_ResizeEnd(object sender, EventArgs e)
        {
            resizeRequested = true;
        }

        private void Window_FormClosing(object sender, FormClosingEventArgs e)
        {
            viewport.EndD3D();
        }

        private void Window_Load(object sender, EventArgs e)
        {
            viewport.StartD3D(window.ClientSize.Width, window.ClientSize.Height);
        }
        #region Handle mouse event
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!cameraController.IsMouseCaptured)
            {
                io.MousePosition = new System.Numerics.Vector2(e.X, e.Y);
            }
            else if (!io.WantCaptureMouse)
            {
                cameraController.MouseMove(new Vector2(e.X, e.Y));
            }
        }

        private void Window_MouseUp(object sender, MouseEventArgs e)
        {
            switch (e.Button)
            {
                case MouseButtons.Left:
                    io.MouseDown[0] = false;
                    break;
                case MouseButtons.Right:
                    io.MouseDown[1] = false;
                    break;
                case MouseButtons.Middle:
                    io.MouseDown[2] = false;
                    break;
            }
            if (cameraController.IsMouseCaptured)
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        break;
                    case MouseButtons.Right:
                        cameraController.EndRotate(new Vector2(e.X, e.Y));
                        break;
                    case MouseButtons.Middle:
                        cameraController.EndPan(new Vector2(e.X, e.Y));
                        break;
                }
            }
        }

        private void Window_MouseDown(object sender, MouseEventArgs e)
        {
            if (!cameraController.IsMouseCaptured)
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        io.MouseDown[0] = true;
                        break;
                    case MouseButtons.Right:
                        io.MouseDown[1] = true;
                        break;
                    case MouseButtons.Middle:
                        io.MouseDown[2] = true;
                        break;
                }
                if (!io.WantCaptureMouse)
                {
                    switch (e.Button)
                    {
                        case MouseButtons.Left:
                            break;
                        case MouseButtons.Right:
                            cameraController.StartRotate(new Vector2(e.X, e.Y));
                            break;
                        case MouseButtons.Middle:
                            cameraController.StartPan(new Vector2(e.X, e.Y));
                            break;
                    }
                }
            }
            else if(!io.WantCaptureMouse)
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        break;
                    case MouseButtons.Right:
                        cameraController.StartRotate(new Vector2(e.X, e.Y));
                        break;
                    case MouseButtons.Middle:
                        cameraController.StartPan(new Vector2(e.X, e.Y));
                        break;
                }
            }
        }

        private void Window_MouseWheel(object sender, MouseEventArgs e)
        {
            if (!cameraController.IsMouseCaptured)
            {
                io.MouseWheel = (int)(e.Delta * 0.01f);
            }
            if(!io.WantCaptureMouse)
            {
                cameraController.MouseWheel(e.Delta, new Vector2(e.X, e.Y));
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {        
            io.KeysDown[e.KeyValue] = true;
            io.ShiftPressed = e.Shift;
            io.CtrlPressed = e.Control;
            io.AltPressed = e.Alt;
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            io.KeysDown[e.KeyValue] = false;
            io.ShiftPressed = e.Shift;
            io.CtrlPressed = e.Control;
            io.AltPressed = e.Alt;
        }


        private void Window_KeyPress(object sender, KeyPressEventArgs e)
        {
            ImGui.AddInputCharacter(e.KeyChar);
        }
        #endregion
    }
}
