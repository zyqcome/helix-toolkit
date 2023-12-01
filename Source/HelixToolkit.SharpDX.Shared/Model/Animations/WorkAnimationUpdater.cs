/*
The MIT License (MIT)
Copyright (c) 2018 Helix Toolkit contributors
*/
using SharpDX;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

#if !NETFX_CORE
namespace HelixToolkit.Wpf.SharpDX
#else
#if CORE
namespace HelixToolkit.SharpDX.Core
#else
namespace HelixToolkit.UWP
#endif
#endif
{
    namespace Animations
    {
        public class WorkAnimationUpdater : IAnimationUpdater
        {

            public string Name
            {
                set; get;
            } = string.Empty;

            public Animation Animation
            {
                get;
            }

            public float StartTime => Animation.StartTime;

            public float EndTime => Animation.EndTime;
            private bool changed = false;
            private float previousTimeElapsed = float.MinValue;
            private readonly List<Model.Scene.SceneNode> animationRoots = new List<Model.Scene.SceneNode>();

            public IList<NodeAnimation> NodeCollection
            {
                get => Animation.NodeAnimationCollection;
            }

            public AnimationRepeatMode RepeatMode
            {
                set; get;
            } = AnimationRepeatMode.Loop;

            public WorkAnimationUpdater(Animation animation)
            {
                Animation = animation;
                Name = animation.Name;
                CreateAnimationRoots();

                //NodeCollection


                orign = new Quaternion[NodeCollection.Count];
                for (int i = 0; i < NodeCollection.Count; i++) 
                {
                    orign[i] = NodeCollection[i].KeyFrames.Items[0].Rotation;
                }
            }

            private void CreateAnimationRoots()
            {
                var nodeHash = new HashSet<Model.Scene.SceneNode>(Animation.NodeAnimationCollection.Select(x => x.Node));
                var roots = new HashSet<Model.Scene.SceneNode>();
                foreach (var node in Animation.NodeAnimationCollection.Select(x => x.Node))
                {
                    var prev = node;
                    foreach (var n in node.TraverseUp())
                    {
                        if (!nodeHash.Contains(n))
                        {
                            roots.Add(prev);
                            break;
                        }
                        prev = n;
                    }
                }
                animationRoots.Clear();
                animationRoots.AddRange(roots);
            }

            public void Update(float timeStamp, long frequency)
            {
                var timeSec = timeStamp / frequency;
                if (timeSec < StartTime)
                {
                    return;
                }

                if (timeSec == StartTime)
                {
                    SetToStart();
                    return;
                }

                if (StartTime == EndTime)
                {
                    return;
                }

                var timeElapsed = timeSec - StartTime;
                if (previousTimeElapsed == timeElapsed)
                {
                    return;
                }

                if (timeElapsed > Animation.EndTime)
                {
                    switch (RepeatMode)
                    {
                        case AnimationRepeatMode.PlayOnce:
                            {
                                SetToStart();
                                return;
                            }
                        case AnimationRepeatMode.PlayOnceHold:
                            {
                                timeElapsed = Animation.EndTime;
                                break;
                            }
                        case AnimationRepeatMode.Loop:
                            {
                                timeElapsed = timeElapsed % (EndTime - StartTime) + StartTime;
                                break;
                            }
                    }
                }
                previousTimeElapsed = timeElapsed;
                UpdateNodes(timeElapsed);
                UpdateBoneSkinMesh();
            }

            private void UpdateBoneSkinMesh()
            {
                if (Animation.HasBoneSkinMeshes && changed)
                {
                    foreach (var root in animationRoots)
                    {
                        root.UpdateAllTransformMatrix();
                    }
                    foreach (var m in Animation.BoneSkinMeshes)
                    {
                        if (m.IsRenderable && !m.HasBoneGroup)// Do not update if has a bone group. Update the group only
                        {
                            var inv = m.TotalModelMatrix.Inverted();
                            var matrices = OnGetNewBoneMatrices(m.Bones.Length);
                            BoneSkinnedMeshGeometry3D.CreateNodeBasedBoneMatrices(m.Bones, ref inv, ref matrices);
                            var old = m.BoneMatrices;
                            m.BoneMatrices = matrices;
                            OnReturnOldBoneMatrices(old);
                        }
                    }
                    changed = false;
                }
            }

            /// <summary>
            /// Called when [get new bone matrices]. Override this to provide your own matices pool to avoid small object creation
            /// </summary>
            /// <param name="count">The count.</param>
            /// <returns></returns>
            protected virtual Matrix[] OnGetNewBoneMatrices(int count)
            {
                return new Matrix[count];
            }
            /// <summary>
            /// Called when [return old bone matrices]. Override this to return the old matrix array back to your own matices pool. <see cref="OnGetNewBoneMatrices(int)"/>
            /// </summary>
            /// <param name="m">The m.</param>
            protected virtual void OnReturnOldBoneMatrices(Matrix[] m)
            {

            }

            private void UpdateNodes(float timeElapsed)
            {
                for (var i = 0; i < NodeCollection.Count; ++i)
                {
                    var n = NodeCollection[i];
                    var count = n.KeyFrames.Count; // Make sure to use this count
                    var frames = n.KeyFrames.Items;
                    var idx = AnimationUtils.FindKeyFrame(timeElapsed, frames);
                    if (idx < 0)
                    {
                        n.Node.ModelMatrix = Matrix.Identity;
                        continue;
                    }
                    ref var currFrame = ref frames[idx];
                    if (currFrame.Time > timeElapsed && idx == 0)
                    {
                        continue;
                    }
                    Debug.Assert(currFrame.Time <= timeElapsed);
                    if (count == 1 || idx == frames.Length - 1)
                    {
                        n.Node.ModelMatrix = Matrix.Scaling(currFrame.Scale) *
                                Matrix.RotationQuaternion(currFrame.Rotation) *
                                Matrix.Translation(currFrame.Translation);
                        continue;
                    }
                    ref var nextFrame = ref frames[idx + 1];
                    Debug.Assert(nextFrame.Time >= timeElapsed);
                    var diff = timeElapsed - currFrame.Time;
                    var length = nextFrame.Time - currFrame.Time;
                    var amount = diff / length;
                    var transform = Matrix.Scaling(Vector3.Lerp(currFrame.Scale, nextFrame.Scale, amount)) *
                                Matrix.RotationQuaternion(Quaternion.Slerp(currFrame.Rotation, nextFrame.Rotation, amount)) *
                                Matrix.Translation(Vector3.Lerp(currFrame.Translation, nextFrame.Translation, amount));
                    n.Node.ModelMatrix = transform;
                }
                changed = true;
            }

            public void Reset()
            {
                SetToStart();
            }

            private void SetToStart()
            {
                previousTimeElapsed = float.MinValue;
                UpdateNodes(0);
                UpdateBoneSkinMesh();
            }

            public void UpdateOneStep(int select_bon, Vector3 axis, float Degrees)
            {
                UpdateNodesOneStep(select_bon, axis, Degrees);
                UpdateBoneSkinMesh();
            }

            Quaternion[] orign;

            private void UpdateNodesOneStep(int select_bon, Vector3 axis, float Degrees)
            {
                var n = NodeCollection[select_bon];
                var count = n.KeyFrames.Count; // Make sure to use this count
                var frames = n.KeyFrames.Items;

                //ref var currFrame = ref frames[select_bon];

                //var axis = new Vector3(0, 0, 1); // 旋转轴
                float angle = Degrees; // 旋转角度（度）
                Quaternion quaternion = CreateFromAxisAngle(axis, angle);

                orign[select_bon] = Quaternion.Multiply(orign[select_bon], quaternion);
                
                var transform = Matrix.Scaling(frames[0].Scale) *
                            Matrix.RotationQuaternion(orign[select_bon]) *
                            Matrix.Translation(frames[0].Translation);

                n.Node.ModelMatrix = transform;

                changed = true;
            }

            private Quaternion CreateFromAxisAngle(Vector3 axis, float angleInDegrees)
            {
                // 将角度转换为弧度
                float angleInRadians = angleInDegrees * ((float)Math.PI / 180.0f);

                // 确保旋转轴是单位向量
                axis = Vector3.Normalize(axis);

                float halfAngle = angleInRadians / 2;
                float sinHalfAngle = (float)Math.Sin(halfAngle);

                float w = (float)Math.Cos(halfAngle);
                float x = axis.X * sinHalfAngle;
                float y = axis.Y * sinHalfAngle;
                float z = axis.Z * sinHalfAngle;

                return new Quaternion(x, y, z, w);
            }



        }
    }
}

