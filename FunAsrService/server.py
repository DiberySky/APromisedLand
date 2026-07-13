from fastapi import FastAPI, UploadFile, File
from funasr import AutoModel
import soundfile as sf
import io

app = FastAPI()

# 从挂载的目录加载模型
model = AutoModel(model="/app/model")

@app.post("/transcribe")
async def transcribe(file: UploadFile = File(...)):
    data, sr = sf.read(io.BytesIO(await file.read()))
    res = model.generate(input=data)
    return {"text": res[0]["text"]}