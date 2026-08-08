import os
import io
import logging
from typing import List, Dict, Any, Optional

logger = logging.getLogger("document-parser")

def extract_text_from_file(file_bytes: bytes, filename: str, content_type: str) -> Dict[str, Any]:
    ext = os.path.splitext(filename)[1].lower()
    
    # Try Docling if available
    try:
        from docling.document_converter import DocumentConverter
        converter = DocumentConverter()
        # Save temp file for Docling input
        temp_path = f"/tmp/{filename}"
        with open(temp_path, "wb") as f:
            f.write(file_bytes)
        
        try:
            result = converter.convert(temp_path)
            doc = result.document
            markdown_text = doc.export_to_markdown()
            
            sections = []
            pages = []
            
            # Simple section extraction from markdown lines
            lines = markdown_text.splitlines()
            current_section = filename
            current_lines = []
            
            for line in lines:
                if line.startswith("#"):
                    if current_lines:
                        sections.append({
                            "title": current_section,
                            "text": "\n".join(current_lines).strip(),
                            "pageNumber": 1,
                            "headingPath": current_section
                        })
                        current_lines = []
                    current_section = line.lstrip("#").strip()
                current_lines.append(line)
                
            if current_lines:
                sections.append({
                    "title": current_section,
                    "text": "\n".join(current_lines).strip(),
                    "pageNumber": 1,
                    "headingPath": current_section
                })
                
            pages.append({"pageNumber": 1, "text": markdown_text})
            
            return {
                "title": os.path.splitext(filename)[0],
                "text": markdown_text,
                "pageCount": max(1, len(pages)),
                "sections": sections,
                "pages": pages
            }
        finally:
            if os.path.exists(temp_path):
                os.remove(temp_path)
    except Exception as e:
        logger.info(f"Docling conversion fallback triggered for '{filename}': {e}")

    # Fallback lightweight extractors (pypdf for PDF, python-docx for DOCX)
    if ext == ".pdf":
        return _extract_pdf_fallback(file_bytes, filename)
    elif ext in [".docx", ".doc"]:
        return _extract_docx_fallback(file_bytes, filename)
    elif ext in [".txt", ".md"]:
        text = file_bytes.decode("utf-8", errors="replace")
        return {
            "title": os.path.splitext(filename)[0],
            "text": text,
            "pageCount": 1,
            "sections": [{"title": "Content", "text": text, "pageNumber": 1, "headingPath": "Content"}],
            "pages": [{"pageNumber": 1, "text": text}]
        }
    else:
        raise ValueError(f"Unsupported file format: '{ext}'")

def _extract_pdf_fallback(file_bytes: bytes, filename: str) -> Dict[str, Any]:
    import pypdf
    reader = pypdf.PdfReader(io.BytesIO(file_bytes))
    page_count = len(reader.pages)
    
    full_text = []
    pages = []
    sections = []
    
    for idx, page in enumerate(reader.pages):
        page_num = idx + 1
        page_text = page.extract_text() or ""
        full_text.append(page_text)
        pages.append({"pageNumber": page_num, "text": page_text})
        sections.append({
            "title": f"Page {page_num}",
            "text": page_text,
            "pageNumber": page_num,
            "headingPath": f"{os.path.splitext(filename)[0]} > Page {page_num}"
        })
        
    combined_text = "\n\n".join(full_text)
    return {
        "title": os.path.splitext(filename)[0],
        "text": combined_text,
        "pageCount": max(1, page_count),
        "sections": sections,
        "pages": pages
    }

def _extract_docx_fallback(file_bytes: bytes, filename: str) -> Dict[str, Any]:
    import docx
    doc = docx.Document(io.BytesIO(file_bytes))
    
    paragraphs = [p.text for p in doc.paragraphs if p.text.strip()]
    full_text = "\n\n".join(paragraphs)
    
    sections = []
    for idx, p in enumerate(paragraphs):
        sections.append({
            "title": f"Paragraph {idx + 1}",
            "text": p,
            "pageNumber": 1,
            "headingPath": f"{os.path.splitext(filename)[0]} > Section {idx + 1}"
        })
        
    return {
        "title": os.path.splitext(filename)[0],
        "text": full_text,
        "pageCount": 1,
        "sections": sections,
        "pages": [{"pageNumber": 1, "text": full_text}]
    }
