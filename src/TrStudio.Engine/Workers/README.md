# TrStudio.Engine/Workers

Background workers: the in-process ONNX image worker and the ComfyUI worker control
(local MPS + RunPod serverless lifecycle, scale-to-zero — ADR-005).

**Worker ownership (Wave-0 anti-conflict seam):**

| Worker | Owning cluster |
|--------|----------------|
| Hangfire job dispatcher / queue workers | Cluster A (spine) |
| ONNX in-process image worker | Cluster F (image) |
| ComfyUI worker control (local MPS + RunPod) | ComfyUI/RunPod cluster (post-Wave-1) |

Do not edit another cluster's worker files.
