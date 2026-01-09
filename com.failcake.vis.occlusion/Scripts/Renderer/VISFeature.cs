#region

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

#endregion

namespace FailCake.VIS
{
    [Serializable]
    public enum VISSampleLevels : byte
    {
        SAMPLE_8 = 8,
        SAMPLE_16 = 16,
        SAMPLE_24 = 24,
        SAMPLE_32 = 32,
        SAMPLE_64 = 64,
        SAMPLE_128 = 128,
        SAMPLE_255 = 255
    }

    public class VISRendererFeature : ScriptableRendererFeature
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        [Header("Settings")]
        public VISSampleLevels samples = VISSampleLevels.SAMPLE_32;

        [Range(-1, 1)]
        public float padding = -0.02F;

        #region PRIVATE

        private VISRenderPass _visRenderPass;

        #endregion

        public override void Create() {
            if (this._visRenderPass != null) return;

            this._visRenderPass = new VISRenderPass(this.samples, this.padding) {
                renderPassEvent = this.renderPassEvent
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            if (!VISController.Instance || renderingData.cameraData.renderType != CameraRenderType.Base) return;

            if (renderingData.cameraData.cameraType == CameraType.SceneView) return;

            Camera currentCamera = renderingData.cameraData.camera;
            bool isMainCamera = currentCamera == Camera.main;
            bool isAdditionalCamera = VISController.Instance.AdditionalCameras.Contains(currentCamera);

            if (!isMainCamera && !isAdditionalCamera) return;

            renderingData.cameraData.camera.depthTextureMode |= DepthTextureMode.Depth;
            renderer.EnqueuePass(this._visRenderPass);
        }

        protected override void Dispose(bool disposing) {
            if (!disposing) return;
            this._visRenderPass = null;
        }

        private class VISRenderPass : ScriptableRenderPass
        {
            private readonly ComputeShader _computeShader;
            private readonly int _kernelIndex;

            private Vector4 _occlusionParams = new Vector4(
                32, // Sample
                0.01f,
                0.02f, // Padding
                0.0F // TotalPortals
            );

            public VISRenderPass(VISSampleLevels samples, float padding) {
                this._occlusionParams.x = (byte)samples;
                this._occlusionParams.z = padding;

                this._computeShader = Resources.Load<ComputeShader>("Shaders/VISOcclusion");
                if (!this._computeShader) throw new UnityException("Failed to load VISOcclusion compute shader from Resources/Shaders/VISOcclusion.compute!");

                this._kernelIndex = this._computeShader.FindKernel("CullPortals");
                if (this._kernelIndex < 0) throw new UnityException("Failed to find 'CullPortals' kernel in VISOcclusion compute shader!");
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData) {
                if (!this._computeShader || !VISController.Instance) return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData == null || cameraData.camera == null) return;

                ComputeBuffer portalBuffer = VISController.Instance.GetPortalBufferForCamera(cameraData.camera);
                if (portalBuffer == null || !portalBuffer.IsValid() || portalBuffer.count == 0) return;

                IReadOnlyList<entity_vis_portal> portals = VISController.Instance.GetPortals();
                if (portals is not { Count: > 0 }) return;

                UniversalResourceData universalResources = frameData.Get<UniversalResourceData>();
                if (universalResources == null || !universalResources.cameraDepthTexture.IsValid()) return;

                this._occlusionParams.w = portals.Count;

                using (IComputeRenderGraphBuilder builder = renderGraph.AddComputePass("VIS PORTALS", out PassData passData))
                {
                    builder.AllowPassCulling(false);

                    passData.occlusionShader = this._computeShader;
                    passData.kernelIndex = this._kernelIndex;

                    passData.portalBuffer = portalBuffer;
                    passData.totalPortals = portals.Count;

                    passData.depthTexture = universalResources.cameraDepthTexture;

                    builder.UseTexture(passData.depthTexture);
                    builder.SetRenderFunc((PassData data, ComputeGraphContext context) => this.ExecutePass(data, context));
                }
                // --------------
            }

            private void ExecutePass(PassData data, ComputeGraphContext context) {
                ComputeCommandBuffer cmd = context.cmd;

                int threadGroups = Mathf.CeilToInt(data.totalPortals / 64F);

                // OCCLUSION ---
                cmd.SetComputeVectorParam(data.occlusionShader, "_OcclusionParams", this._occlusionParams);
                cmd.SetComputeBufferParam(data.occlusionShader, data.kernelIndex, "_PortalDataBuffer", data.portalBuffer);
                cmd.SetComputeTextureParam(data.occlusionShader, data.kernelIndex, "_VISDepthTexture", data.depthTexture, 0, RenderTextureSubElement.Default);

                cmd.DispatchCompute(data.occlusionShader, data.kernelIndex, threadGroups, 1, 1);
                // --------------
            }

            private class PassData
            {
                internal ComputeShader occlusionShader;
                internal int kernelIndex;

                internal ComputeBuffer portalBuffer;
                internal int totalPortals;

                internal TextureHandle depthTexture;
            }
        }
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