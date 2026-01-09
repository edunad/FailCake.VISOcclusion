#region

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace FailCake.VIS
{
    [Preserve, StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PortalComputeDataGPU
    {
        public Matrix4x4 localToWorld;
        public Vector4 data;
    }

    [Serializable]
    public enum PortalStatus : uint
    {
        PENDING = 0,

        VISIBLE = 1,
        CLOSED = 2
    }

    [Serializable]
    public enum PortalType : uint
    {
        UNKNOWN = 0,

        PLANE = 1,
        CUBE = 2
    }

    [DefaultExecutionOrder(-100)]
    public class VISController : MonoBehaviour
    {
        public static VISController Instance { get; private set; }

        [Header("Settings"), Range(0, 5)]
        public float OcclusionDelay = 1.0f;

        [Range(0, 100)]
        public float MaxDistance = 15;

        public List<Camera> AdditionalCameras = new List<Camera>();

        [Header("Debug")]
        public bool DebugMode;

        #region PRIVATE

        private readonly List<entity_vis_room> _rooms = new List<entity_vis_room>();
        private readonly List<entity_vis_portal> _portals = new List<entity_vis_portal>();

        private readonly Dictionary<entity_vis_room, float> _roomInvisibilityTimers = new Dictionary<entity_vis_room, float>();

        private readonly Dictionary<Camera, ComputeBuffer> _cameraPortalBuffers = new Dictionary<Camera, ComputeBuffer>();
        private readonly Dictionary<Camera, HashSet<int>> _cameraVisiblePortals = new Dictionary<Camera, HashSet<int>>();

        #endregion

        public void Awake() {
            if (VISController.Instance != null && VISController.Instance != this)
            {
                VISController.DestroyImmediate(this.gameObject);
                return;
            }

            VISController.Instance = this;

            // EVENTS ----
            RenderPipelineManager.beginCameraRendering += this.OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += this.OnEndCameraRendering;
            // -----------
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam) {
            if (!Camera.main || !cam || !cam.isActiveAndEnabled) return;

            bool isMainCamera = cam == Camera.main;
            bool isAdditionalCamera = this.AdditionalCameras.Contains(cam);

            switch (isMainCamera)
            {
                case false when !isAdditionalCamera:
                    return;
                case true:
                {
                    foreach (entity_vis_portal portal in this._portals)
                    {
                        if (!portal) continue;
                        portal.SetStatus(portal.IsOpen() ? PortalStatus.PENDING : PortalStatus.CLOSED);
                    }

                    break;
                }
            }

            if (!this._cameraVisiblePortals.TryGetValue(cam, out HashSet<int> visiblePortal))
                this._cameraVisiblePortals[cam] = new HashSet<int>();
            else
                visiblePortal.Clear();

            this.UpdateBuffers(cam);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam) {
            if (!Camera.main || !cam || !cam.isActiveAndEnabled) return;

            bool isMainCamera = cam == Camera.main;
            bool isAdditionalCamera = this.AdditionalCameras.Contains(cam);

            if (!isMainCamera && !isAdditionalCamera) return;

            if (!this._cameraPortalBuffers.TryGetValue(cam, out ComputeBuffer buffer) || buffer == null) return;

            AsyncGPUReadback.Request(buffer, request => this.OnCullingDataReady(request, cam));
        }

        public void OnDestroy() {
            this.ReleaseBuffers();

            // EVENTS ----
            RenderPipelineManager.beginCameraRendering -= this.OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= this.OnEndCameraRendering;
            // -----------

            // CLEANUP ----
            VISController.Instance = null;
            // ------------
        }

        #region REGISTRY

        public void RegisterRoom(entity_vis_room room) {
            if (this._rooms.Contains(room)) throw new UnityException("Room already registered!");

            this._rooms.Add(room);
            this._roomInvisibilityTimers[room] = Time.time + this.OcclusionDelay;
        }

        public void UnregisterRoom(entity_vis_room room) {
            if (!this._rooms.Contains(room)) throw new UnityException("Room not registered!");

            this._rooms.Remove(room);
            this._roomInvisibilityTimers.Remove(room);
        }

        public void RegisterPortal(entity_vis_portal portal) {
            if (this._portals.Contains(portal)) throw new UnityException("Portal already registered");

            this._portals.Add(portal);
            this.ReInitBuffers();
        }

        public void UnregisterPortal(entity_vis_portal portal) {
            if (!this._portals.Contains(portal)) throw new UnityException("Portal not registered");

            this._portals.Remove(portal);
            this.ReInitBuffers();
        }

        #endregion

        public ComputeBuffer GetPortalBufferForCamera(Camera cam) {
            return this._cameraPortalBuffers.GetValueOrDefault(cam);
        }

        public IReadOnlyList<entity_vis_portal> GetPortals() {
            return this._portals;
        }

        #region PRIVATE

        #region BUFFERS

        private void ReInitBuffers() {
            this.ReleaseBuffers();
        }

        private ComputeBuffer GetOrCreateCameraBuffer(Camera cam) {
            if (this._cameraPortalBuffers.TryGetValue(cam, out ComputeBuffer existingBuffer) && existingBuffer != null && existingBuffer.IsValid()) return existingBuffer;

            if (this._portals.Count == 0) return null;

            ComputeBuffer newBuffer = new ComputeBuffer(this._portals.Count, Marshal.SizeOf<PortalComputeDataGPU>(), ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            this._cameraPortalBuffers[cam] = newBuffer;

            return newBuffer;
        }

        private void UpdateBuffers(Camera cam) {
            if (!cam) return;

            ComputeBuffer cameraBuffer = this.GetOrCreateCameraBuffer(cam);
            if (cameraBuffer == null || !cameraBuffer.IsValid()) return;

            PortalComputeDataGPU[] cameraPortalArray = new PortalComputeDataGPU[this._portals.Count];

            Transform cameraTransform = cam.transform;
            Vector3 cameraPosition = cameraTransform.position;
            float maxDistanceSquared = this.MaxDistance * this.MaxDistance;

            for (int i = 0; i < this._portals.Count; i++)
            {
                entity_vis_portal portal = this._portals[i];
                if (!portal)
                {
                    cameraPortalArray[i] = new PortalComputeDataGPU {
                        localToWorld = Matrix4x4.identity,
                        data = new Vector4((uint)PortalType.UNKNOWN, (uint)PortalStatus.CLOSED, 0, 0)
                    };

                    continue;
                }

                Vector3 portalPos = portal.transform.position;
                float distanceSquared = (portalPos - cameraPosition).sqrMagnitude;

                PortalStatus portalStatus = portal.IsOpen() && distanceSquared < maxDistanceSquared
                    ? PortalStatus.PENDING
                    : PortalStatus.CLOSED;

                if (portalStatus == PortalStatus.PENDING)
                {
                    float3 portalToCamera = cameraPosition - portalPos;
                    float3 localPos = math.rotate(math.inverse(portal.transform.rotation), portalToCamera);

                    bool withinX = math.abs(localPos.x) < portal.size.x * 0.5f;
                    bool withinY = math.abs(localPos.y) < portal.size.y * 0.5f;
                    bool withinZ = math.abs(localPos.z) < portal.size.z * 0.5f;

                    if (withinX && withinY && withinZ) portalStatus = PortalStatus.VISIBLE;
                }

                cameraPortalArray[i] = new PortalComputeDataGPU {
                    localToWorld = Matrix4x4.TRS(
                        portal.transform.position,
                        portal.transform.rotation,
                        portal.size
                    ),
                    data = new Vector4((uint)(portal is entity_vis_portal_3d ? PortalType.CUBE : PortalType.PLANE), (float)portalStatus, 0, 0)
                };
            }

            cameraBuffer.SetData(cameraPortalArray);
        }

        private void ReleaseBuffers() {
            foreach (ComputeBuffer buffer in this._cameraPortalBuffers.Values) buffer?.Release();

            this._cameraPortalBuffers.Clear();
            this._cameraVisiblePortals.Clear();
        }

        #endregion

        private List<entity_vis_room> FindCurrentRoom(Camera cam) {
            if (!cam) return null;
            Vector3 cameraPosition = cam.transform.position;

            List<entity_vis_room> containingRooms = new List<entity_vis_room>();
            foreach (entity_vis_room room in this._rooms)
            {
                if (!room) continue;
                if (room.IsInside?.Invoke(cameraPosition) == true) containingRooms.Add(room);
            }

            return containingRooms;
        }

        private void OnCullingDataReady(AsyncGPUReadbackRequest request, Camera cam) {
            if (!request.done || request.hasError || !cam) return;
            float currentTime = Time.time;

            bool isMainCamera = cam == Camera.main;

            // Mark inside rooms -------
            List<entity_vis_room> currentRooms = this.FindCurrentRoom(cam);
            foreach (entity_vis_room room in currentRooms) this._roomInvisibilityTimers[room] = currentTime + this.OcclusionDelay;
            // ------------

            // Check portals -------
            PortalComputeDataGPU[] portalDataArray = request.GetData<PortalComputeDataGPU>().ToArray();
            if (portalDataArray.Length != this._portals.Count) return;

            for (int i = 0; i < this._portals.Count; i++)
            {
                entity_vis_portal portal = this._portals[i];
                if (!portal) continue;

                PortalStatus status = i >= portalDataArray.Length ? PortalStatus.CLOSED : (PortalStatus)portalDataArray[i].data.y;

                if (isMainCamera) portal.SetStatus(status);
                if (status != PortalStatus.VISIBLE) continue;

                if (this._cameraVisiblePortals.TryGetValue(cam, out HashSet<int> visibleSet)) visibleSet.Add(i);

                switch (portal)
                {
                    case entity_vis_portal_2d planePortal:
                    {
                        entity_vis_room roomA = planePortal.GetRoomA();
                        entity_vis_room roomB = planePortal.GetRoomB();

                        if (roomA) this._roomInvisibilityTimers[roomA] = currentTime + this.OcclusionDelay;
                        if (roomB) this._roomInvisibilityTimers[roomB] = currentTime + this.OcclusionDelay;
                        break;
                    }

                    case entity_vis_portal_3d cubePortal:
                    {
                        entity_vis_room room = cubePortal.GetRoom();
                        if (room) this._roomInvisibilityTimers[room] = currentTime + this.OcclusionDelay;
                        break;
                    }
                }
            }
            // ------------------------

            // UPDATE ALL ROOMS  --------------
            if (isMainCamera)
                foreach (entity_vis_room room in this._rooms)
                {
                    if (!room) continue;
                    float timerValue = this._roomInvisibilityTimers.GetValueOrDefault(room, 0);
                    room.OnVisibilityChanged?.Invoke(currentTime <= timerValue);
                }
            // ------------------------
        }


        #if UNITY_EDITOR

        public void OnDrawGizmos() {
            if (!Application.isPlaying || !Camera.main || !this.DebugMode) return;

            // Main camera ----
            if (Camera.main && Camera.main.isActiveAndEnabled)
            {
                Vector3 cameraPos = Camera.main.transform.position;
                Gizmos.color = Color.yellow;
                Handles.color = new Color(1f, 1f, 0f, 0.05f);
                this.DrawCameraFrustum(Camera.main);

                if (this._cameraVisiblePortals.TryGetValue(Camera.main, out HashSet<int> mainCamPortals))
                    for (int i = 0; i < this._portals.Count; i++)
                    {
                        if (!mainCamPortals.Contains(i)) continue;
                        entity_vis_portal portal = this._portals[i];
                        if (!portal) continue;

                        Gizmos.color = Color.cyan;
                        Handles.color = new Color(0f, 1f, 1f, 0.1f);
                        this.DrawFrustumThroughPortal(portal, cameraPos);
                    }
            }
            // ----------------

            // Additional cameras ----
            for (int i = 0; i < this.AdditionalCameras.Count; i++)
            {
                Camera additionalCam = this.AdditionalCameras[i];
                if (!additionalCam || !additionalCam.isActiveAndEnabled) continue;

                Vector3 cameraPos = additionalCam.transform.position;
                Color cameraColor = this.GetDebugColorForCameraIndex(i);

                Gizmos.color = cameraColor;
                Handles.color = new Color(cameraColor.r, cameraColor.g, cameraColor.b, 0.05f);

                this.DrawCameraFrustum(additionalCam);
                if (!this._cameraVisiblePortals.TryGetValue(additionalCam, out HashSet<int> camPortals)) continue;

                for (int portalIdx = 0; portalIdx < this._portals.Count; portalIdx++)
                {
                    if (!camPortals.Contains(portalIdx)) continue;
                    entity_vis_portal portal = this._portals[portalIdx];
                    if (!portal) continue;

                    Color portalColor = new Color(cameraColor.r * 0.8f, cameraColor.g * 0.8f, cameraColor.b * 1f, 1f);

                    Gizmos.color = portalColor;
                    Handles.color = new Color(portalColor.r, portalColor.g, portalColor.b, 0.1f);

                    this.DrawFrustumThroughPortal(portal, cameraPos);
                }
            }
            // -------------------------
        }

        private Color GetDebugColorForCameraIndex(int index) {
            Color[] colors = {
                new Color(1f, 0.5f, 0f),
                new Color(0.5f, 0f, 1f),
                new Color(0f, 1f, 0.5f),
                new Color(1f, 0f, 0.5f),
                new Color(0.5f, 1f, 0f)
            };

            return colors[index % colors.Length];
        }

        private void DrawCameraFrustum(Camera cam) {
            float nearClip = cam.nearClipPlane;
            float farClip = Mathf.Min(cam.farClipPlane, this.MaxDistance);

            Vector3[] nearCorners = new Vector3[4];
            Vector3[] farCorners = new Vector3[4];

            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), nearClip, Camera.MonoOrStereoscopicEye.Mono, nearCorners);
            cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1), farClip, Camera.MonoOrStereoscopicEye.Mono, farCorners);

            Transform camTransform = cam.transform;
            for (int i = 0; i < 4; i++)
            {
                nearCorners[i] = camTransform.TransformPoint(nearCorners[i]);
                farCorners[i] = camTransform.TransformPoint(farCorners[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                Gizmos.DrawLine(nearCorners[i], nearCorners[next]);
                Gizmos.DrawLine(farCorners[i], farCorners[next]);
                Gizmos.DrawLine(nearCorners[i], farCorners[i]);
            }

            Handles.DrawAAConvexPolygon(nearCorners[0], nearCorners[1], farCorners[1], farCorners[0]);
            Handles.DrawAAConvexPolygon(nearCorners[2], nearCorners[3], farCorners[3], farCorners[2]);
            Handles.DrawAAConvexPolygon(nearCorners[0], nearCorners[3], farCorners[3], farCorners[0]);
            Handles.DrawAAConvexPolygon(nearCorners[1], nearCorners[2], farCorners[2], farCorners[1]);
        }

        private void DrawFrustumThroughPortal(entity_vis_portal portal, Vector3 cameraPos) {
            Vector3 portalPos = portal.transform.position;
            Vector3 portalRight = portal.transform.right;
            Vector3 portalUp = portal.transform.up;
            Vector3 portalForward = portal.transform.forward;

            Vector3[] portalCorners = null;
            if (portal is entity_vis_portal_2d planePortal)
            {
                float halfWidth = planePortal.size.x * 0.5f;
                float halfHeight = planePortal.size.y * 0.5f;

                portalCorners = new Vector3[4] {
                    portalPos + -portalRight * halfWidth + -portalUp * halfHeight,
                    portalPos + portalRight * halfWidth + -portalUp * halfHeight,
                    portalPos + portalRight * halfWidth + portalUp * halfHeight,
                    portalPos + -portalRight * halfWidth + portalUp * halfHeight
                };
            }
            else if (portal is entity_vis_portal_3d cubePortal)
            {
                float halfWidth = cubePortal.size.x * 0.5f;
                float halfHeight = cubePortal.size.y * 0.5f;
                float halfDepth = cubePortal.size.z * 0.5f;

                portalCorners = new[] {
                    portalPos + -portalRight * halfWidth + -portalUp * halfHeight + -portalForward * halfDepth,
                    portalPos + portalRight * halfWidth + -portalUp * halfHeight + -portalForward * halfDepth,
                    portalPos + portalRight * halfWidth + portalUp * halfHeight + -portalForward * halfDepth,
                    portalPos + -portalRight * halfWidth + portalUp * halfHeight + -portalForward * halfDepth,
                    portalPos + -portalRight * halfWidth + -portalUp * halfHeight + portalForward * halfDepth,
                    portalPos + portalRight * halfWidth + -portalUp * halfHeight + portalForward * halfDepth,
                    portalPos + portalRight * halfWidth + portalUp * halfHeight + portalForward * halfDepth,
                    portalPos + -portalRight * halfWidth + portalUp * halfHeight + portalForward * halfDepth
                };
            }

            if (portalCorners == null) return;

            // SETUP CORNERS ----
            Vector3[] farCorners = new Vector3[portalCorners.Length];

            float frustumLength = 10;
            for (int i = 0; i < portalCorners.Length; i++)
            {
                Vector3 rayDirection = (portalCorners[i] - cameraPos).normalized;
                farCorners[i] = portalCorners[i] + rayDirection * frustumLength;
            }
            // ----------------

            // RENDERING ------
            if (portalCorners.Length == 4)
                for (int i = 0; i < 4; i++)
                {
                    int next = (i + 1) % 4;

                    Gizmos.DrawLine(portalCorners[i], farCorners[i]);

                    Handles.DrawAAConvexPolygon(portalCorners[i], farCorners[i], farCorners[next]);
                    Handles.DrawAAConvexPolygon(portalCorners[i], farCorners[next], portalCorners[next]);
                }
            else if (portalCorners.Length == 8)
            {
                for (int i = 0; i < 8; i++) Gizmos.DrawLine(portalCorners[i], farCorners[i]);

                // FRONT
                this.DrawFrustumFace(portalCorners[0], portalCorners[1], farCorners[0], farCorners[1]);
                this.DrawFrustumFace(portalCorners[1], portalCorners[2], farCorners[1], farCorners[2]);
                this.DrawFrustumFace(portalCorners[2], portalCorners[3], farCorners[2], farCorners[3]);
                this.DrawFrustumFace(portalCorners[3], portalCorners[0], farCorners[3], farCorners[0]);
                // -----------------

                // BACK
                this.DrawFrustumFace(portalCorners[4], portalCorners[5], farCorners[4], farCorners[5]);
                this.DrawFrustumFace(portalCorners[5], portalCorners[6], farCorners[5], farCorners[6]);
                this.DrawFrustumFace(portalCorners[6], portalCorners[7], farCorners[6], farCorners[7]);
                this.DrawFrustumFace(portalCorners[7], portalCorners[4], farCorners[7], farCorners[4]);
                // -----------------

                // SIDE
                this.DrawFrustumFace(portalCorners[0], portalCorners[4], farCorners[0], farCorners[4]);
                this.DrawFrustumFace(portalCorners[1], portalCorners[5], farCorners[1], farCorners[5]);
                this.DrawFrustumFace(portalCorners[2], portalCorners[6], farCorners[2], farCorners[6]);
                this.DrawFrustumFace(portalCorners[3], portalCorners[7], farCorners[3], farCorners[7]);
                // -----------------
            }
            // -----------------
        }

        private void DrawFrustumFace(Vector3 p1, Vector3 p2, Vector3 f1, Vector3 f2) {
            Handles.DrawAAConvexPolygon(p1, f1, f2);
            Handles.DrawAAConvexPolygon(p1, f2, p2);
        }

        #endif

        #endregion
    }
}

/*# MIT License Copyright (c) 2025 FailCake

# Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the
# "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish,
# distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to
# the following conditions:
#
# The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
# MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
# ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
# SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.*/