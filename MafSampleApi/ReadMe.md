docker run --rm --gpus=all -p 8000:8000 `
  --entrypoint python3 `
-e VLLM_USE_V1=0 `
  -e HF_ENDPOINT=https://hf-mirror.com `
-v vllm-hf-cache:/root/.cache/huggingface `
  vllm/vllm-openai:qwen3-tf451 `
-m vllm.entrypoints.openai.api_server `
  --model Qwen/Qwen3-4B-AWQ `
--served-model-name qwen3-4b-awq `
  --dtype float16 `
--quantization awq `
  --max-model-len 4096 `
--gpu-memory-utilization 0.85 `
--enforce-eager