namespace B2BShop.Domain.Entities;

public class IntegrationConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }

    // Transport: "http" | "sftp" | "fileshare" | "email"
    public string Transport { get; set; } = "http";

    // Format: "json" | "xml" | "csv" | "edi"
    public string Format { get; set; } = "json";

    // Transport-specific settings stored as JSON
    public string TransportConfigJson { get; set; } = "{}";

    // Field mapping: source field -> target field JSON map
    public string FieldMappingJson { get; set; } = "{}";

    // Value translation tables as JSON
    public string ValueTranslationJson { get; set; } = "{}";

    // Scheduling: "immediate" | "batch"
    public string Schedule { get; set; } = "immediate";
    public string? BatchCronExpression { get; set; }

    public int RetryCount { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 60;

    public bool IsActive { get; set; } = true;
    public DateTime? LastTransferAt { get; set; }
    public string? LastTransferStatus { get; set; }
    public string? LastErrorMessage { get; set; }

    // Stub behavior configuration — for training scenarios
    public int? SimulatedFailurePercent { get; set; }
    public int? SimulatedDelayMs { get; set; }

    public Company? Company { get; set; }
}
