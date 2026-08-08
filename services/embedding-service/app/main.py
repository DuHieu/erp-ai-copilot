import logging
from typing import List
from fastapi import FastAPI, HTTPException, status
from pydantic import BaseModel, Field
from app.embedding import engine

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("embedding-service")

app = FastAPI(
    title="ERP AI Copilot — Embedding Service",
    description="Local Multilingual Vector Embedding Service (BAAI/bge-m3)",
    version="0.2.2"
)

@app.on_event("startup")
def startup_event():
    try:
        engine.load_model()
    except Exception as e:
        logger.error(f"Failed model loading during startup: {e}")

class EmbedRequest(BaseModel):
    texts: List[str] = Field(..., description="List of plain text strings to embed")

class EmbedResponse(BaseModel):
    model: str
    dimension: int
    embeddings: List[List[float]]

@app.get("/health")
def health():
    return {
        "status": "healthy",
        "service": "embedding-service",
        "model": engine.model_name,
        "dimension": engine.dimension if engine.initialized else 0
    }

@app.get("/ready")
def ready():
    if not engine.initialized:
        raise HTTPException(status_code=status.HTTP_503_SERVICE_UNAVAILABLE, detail="Model is still loading")
    return {"status": "ready", "model": engine.model_name, "dimension": engine.dimension}

@app.post("/embed", response_model=EmbedResponse)
def embed(request: EmbedRequest):
    if not request.texts:
        raise HTTPException(status_code=400, detail="Text array cannot be empty")
        
    try:
        vectors = engine.embed_texts(request.texts)
        return EmbedResponse(
            model=engine.model_name,
            dimension=engine.dimension,
            embeddings=vectors
        )
    except Exception as e:
        logger.error(f"Embedding failure: {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Embedding calculation failed: {str(e)}")
