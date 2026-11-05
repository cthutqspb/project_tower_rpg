using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule.Util;
using System.Collections.Generic;

public class HighlightRendererFeature : ScriptableRendererFeature
{
    public Material maskMaterial;     
    public Material outlineMaterial;  

    public class HighlightPass : ScriptableRenderPass
    {
        private Material _maskMaterial;
        private Material _outlineMaterial;

        // Список объектов, которые нужно подсветить (обновляется из HighlightManager)
        public static readonly List<Renderer> RenderersToHighlight = new List<Renderer>();

        public HighlightPass(Material maskMat, Material outlineMat)
        {
            _maskMaterial = maskMat;
            _outlineMaterial = outlineMat;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_maskMaterial == null || _outlineMaterial == null) return;
            if (RenderersToHighlight.Count == 0) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();

            // 1. Создаем текстуру-маску
            var desc = new TextureDesc(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height);
            desc.name = "HighlightMask";
            desc.clearBuffer = true;
            desc.clearColor = Color.black;
            desc.colorFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            // desc.depthBufferBits = DepthBits.Depth24; // (Убрали, так как для Color Attachment не нужно)
            
            TextureHandle maskTexture = renderGraph.CreateTexture(desc);

            // --- ПАСС 1: РИСУЕМ ОБЪЕКТЫ В МАСКУ ---
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Draw Mask", out var passData))
            {
                passData.MaskMaterial = _maskMaterial;
                passData.Renderers = RenderersToHighlight;
                
                builder.SetRenderAttachment(maskTexture, 0, AccessFlags.Write);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    foreach (var r in data.Renderers)
                    {
                        if (r == null) continue;
                        context.cmd.DrawRenderer(r, data.MaskMaterial);
                    }
                });
            }

            _outlineMaterial.SetVector("_TexelSize", new Vector4(1f / desc.width, 1f / desc.height, 0, 0));

            var blitParams = new RenderGraphUtils.BlitMaterialParameters(
                maskTexture, 
                resourceData.activeColorTexture, 
                _outlineMaterial, 
                0 
            );
            renderGraph.AddBlitPass(blitParams, "Draw Outline");
        }

        class PassData
        {
            public Material MaskMaterial;
            public List<Renderer> Renderers;
        }
    }

    private HighlightPass _pass;

    public override void Create()
    {
        _pass = new HighlightPass(maskMaterial, outlineMaterial);
        _pass.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (maskMaterial != null && outlineMaterial != null)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}
