# TrStudio.Engine/Providers

`IGenerationProvider` implementations (the seam is defined in
`TrStudio.Models/Providers/IGenerationProvider.cs` — do not redeclare it here).

**Provider ownership (Wave-0 anti-conflict seam):**

| Provider | Owning cluster |
|----------|----------------|
| `OnnxLocalProvider` (local image path, Fast tier) | Cluster F (image) |
| `ComfyUIProvider` (local MPS / RunPod, Fast+High tiers) | ComfyUI/RunPod cluster (post-Wave-1) |
| `HeyGenProvider` (Premium avatar) | Premium/closed-API cluster (post-Wave-1) |
| `ClosedApiProvider` (aggregator) | Premium/closed-API cluster (post-Wave-1) |

One provider class per file; do not edit another cluster's provider.
