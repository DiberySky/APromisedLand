import httpx, json

API = "http://localhost:5324/api/vllm/chat/loop"

payload = {
    "message": "用三句话解释量子纠缠",
    "continuePrompt": "承接上一轮，举例说明。",
    "maxRounds": 2,
    "maxOutputTokens": 512,
}

with httpx.stream(
        "POST", API,
        json=payload,
        headers={"Content-Type": "application/json"},
        timeout=180,
) as r:
    r.raise_for_status()

    for line in r.iter_lines():
        if not line.startswith("data: "):
            continue

        chunk = json.loads(line[6:])
        phase = chunk["phase"]

        if phase == "start":
            print(f"[共 {chunk['maxRounds']} 轮]")
        elif phase == "round_start":
            print(f"\n──── 第 {chunk['round']} 轮 ────")
        elif phase == "delta":
            print(chunk["content"], end="", flush=True)
        elif phase == "round_end":
            print()
        elif phase == "done":
            tail = " (已截断)" if chunk["truncated"] else ""
            print(f"\n[完成]{tail}")
        elif phase == "error":
            print(f"\n[错误] {chunk['content']}")