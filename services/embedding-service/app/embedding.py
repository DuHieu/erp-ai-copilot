import os
import logging
from typing import List, Dict, Any

logger = logging.getLogger("embedding-service")

MODEL_NAME = os.getenv("EMBEDDING_MODEL", "BAAI/bge-m3")

class EmbeddingEngine:
    def __init__(self):
        self.model_name = MODEL_NAME
        self.model = None
        self.dimension = 1024
        self.initialized = False

    def load_model(self):
        if self.initialized:
            return
        
        logger.info(f"Loading embedding model '{self.model_name}'...")
        try:
            from sentence_transformers import SentenceTransformer
            self.model = SentenceTransformer(self.model_name)
            # Sample encode to determine actual dimension
            test_vec = self.model.encode("test", normalize_embeddings=True)
            self.dimension = len(test_vec)
            self.initialized = True
            logger.info(f"Embedding model '{self.model_name}' loaded successfully. Vector dimension: {self.dimension}")
        except Exception as e:
            logger.warning(f"Failed to load primary model '{self.model_name}': {e}. Falling back to 'paraphrase-multilingual-MiniLM-L12-v2'...")
            try:
                from sentence_transformers import SentenceTransformer
                self.model_name = "paraphrase-multilingual-MiniLM-L12-v2"
                self.model = SentenceTransformer(self.model_name)
                test_vec = self.model.encode("test", normalize_embeddings=True)
                self.dimension = len(test_vec)
                self.initialized = True
                logger.info(f"Fallback model '{self.model_name}' loaded successfully. Vector dimension: {self.dimension}")
            except Exception as ex:
                logger.error(f"Critical error loading fallback embedding model: {ex}", exc_info=True)
                raise

    def embed_texts(self, texts: List[str]) -> List[List[float]]:
        if not self.initialized or self.model is None:
            self.load_model()
            
        if not texts:
            return []
            
        embeddings = self.model.encode(texts, normalize_embeddings=True, show_progress_bar=False)
        return [vec.tolist() for vec in embeddings]

engine = EmbeddingEngine()
