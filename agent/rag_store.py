"""
Vector RAG Store - Semantic Memory for Agent
Uses ChromaDB for persistent vector storage
"""
import chromadb
from chromadb.config import Settings
import hashlib
from pathlib import Path


class RagStore:
    """
    Persistent vector store for agent semantic memory.
    Stores previous executions and their outcomes for context retrieval.
    """
    
    def __init__(self, persist_directory: str):
        """
        Initialize RAG store with persistent storage.
        
        Args:
            persist_directory: Directory path for ChromaDB persistence
        """
        self.persist_dir = Path(persist_directory)
        self.persist_dir.mkdir(parents=True, exist_ok=True)
        
        # Use PersistentClient instead of deprecated Client
        self.client = chromadb.PersistentClient(
            path=str(self.persist_dir),
            settings=Settings(
                anonymized_telemetry=False,
                allow_reset=True
            )
        )
        
        # Get or create collection
        self.collection = self.client.get_or_create_collection(
            name="agent_memory",
            metadata={"description": "Agent execution memory"}
        )
    
    def add(self, text: str, metadata: dict = None):
        """
        Add a document to the vector store.
        
        Args:
            text: Text content to store
            metadata: Optional metadata dictionary
        """
        if not text or not text.strip():
            return
        
        # Generate unique ID from content hash
        doc_id = hashlib.md5(text.encode()).hexdigest()
        
        # Sanitize metadata - ChromaDB only accepts str, int, float, bool values
        clean_metadata = {}
        if metadata:
            for key, value in metadata.items():
                if value is None:
                    clean_metadata[key] = ""
                elif isinstance(value, (str, int, float, bool)):
                    clean_metadata[key] = value
                else:
                    # Convert complex objects to string
                    clean_metadata[key] = str(value)
        
        # Add to collection with try-catch for safety
        try:
            self.collection.add(
                documents=[text],
                metadatas=[clean_metadata if clean_metadata else None],
                ids=[doc_id]
            )
        except Exception as e:
            # Silently fail on metadata issues - don't break the agent
            print(f"Warning: Failed to store in RAG (metadata issue): {e}")
    
    def query(self, text: str, n_results: int = 3):
        """
        Query the vector store for similar documents.
        
        Args:
            text: Query text
            n_results: Number of results to return
            
        Returns:
            Dict with 'documents', 'metadatas', 'distances'
        """
        if not text or not text.strip():
            return None
        
        try:
            # Query the collection
            results = self.collection.query(
                query_texts=[text],
                n_results=n_results
            )
            return results
        except Exception as e:
            print(f"??  RAG query error: {e}")
            return None
    
    def reset(self):
        """Clear all documents from the store."""
        try:
            self.client.delete_collection("agent_memory")
            self.collection = self.client.get_or_create_collection(
                name="agent_memory",
                metadata={"description": "Agent execution memory"}
            )
            print("? RAG store reset")
        except Exception as e:
            print(f"??  RAG reset error: {e}")
    
    def count(self) -> int:
        """Get the number of documents in the store."""
        return self.collection.count()
