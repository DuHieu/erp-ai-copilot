import logging
from fastapi import FastAPI, UploadFile, File, HTTPException, status
from fastapi.responses import JSONResponse
from app.parser import extract_text_from_file

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("document-parser")

app = FastAPI(
    title="ERP AI Copilot — Document Parser Service",
    description="Python FastAPI sidecar parsing PDF, DOCX, TXT, MD using Docling",
    version="0.2.1"
)

@app.get("/health")
def health():
    return {"status": "healthy", "service": "document-parser"}

@app.post("/parse")
async def parse_document(file: UploadFile = File(...)):
    if not file or not file.filename:
        raise HTTPException(status_code=400, detail="No file provided")
    
    filename = file.filename
    content_type = file.content_type or "application/octet-stream"
    
    logger.info(f"Parsing document '{filename}' ({content_type})")
    
    try:
        content = await file.read()
        if len(content) == 0:
            raise HTTPException(status_code=400, detail="Empty file uploaded")
            
        result = extract_text_from_file(content, filename, content_type)
        return JSONResponse(content=result)
    except ValueError as ve:
        logger.warning(f"Invalid file request for '{filename}': {ve}")
        raise HTTPException(status_code=400, detail=str(ve))
    except Exception as e:
        logger.error(f"Failed to parse document '{filename}': {e}", exc_info=True)
        raise HTTPException(status_code=500, detail=f"Document parsing failed: {str(e)}")
