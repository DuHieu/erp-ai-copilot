namespace ERP.AI.Knowledge.Enums;

public enum DocumentStatus
{
    Uploaded,
    Queued,
    Processing,
    Processed,
    Failed,
    Deleted
}

public enum ProcessingStage
{
    Validation,
    Storage,
    Parsing,
    Normalization,
    Chunking,
    Persistence,
    Completed,
    Failed
}

public enum EmbeddingStatus
{
    NotIndexed,
    Queued,
    Indexing,
    Indexed,
    Failed,
    Outdated
}
