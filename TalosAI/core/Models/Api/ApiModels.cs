using Newtonsoft.Json;

namespace TalosAI.core.Models.Api
{
    /// <summary>
    /// Base response model for API responses
    /// </summary>
    public class ApiResponse<T>
    {
        [JsonProperty("data")]
        public T? Data { get; set; }

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Paginated response model
    /// </summary>
    public class PaginatedResponse<T>
    {
        [JsonProperty("data")]
        public List<T>? Data { get; set; }

        [JsonProperty("totalRecords")]
        public int TotalRecords { get; set; }

        [JsonProperty("currentPage")]
        public int CurrentPage { get; set; }

        [JsonProperty("pageSize")]
        public int PageSize { get; set; }

        [JsonProperty("totalPages")]
        public int TotalPages { get; set; }
    }

    /// <summary>
    /// Financial Project summary response
    /// </summary>
    public class FinancialProjectSummary
    {
        [JsonProperty("totalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("processed")]
        public int Processed { get; set; }

        [JsonProperty("reprocessed")]
        public int Reprocessed { get; set; }

        [JsonProperty("actionRequired")]
        public int ActionRequired { get; set; }

        [JsonProperty("period")]
        public string? Period { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }
    }

    /// <summary>
    /// Chart API Response - matches actual API structure
    /// </summary>
    public class ChartApiResponse
    {
        [JsonProperty("TotalCount")]
        public int TotalCount { get; set; }

        [JsonProperty("Processed")]
        public int Processed { get; set; }

        [JsonProperty("Reprocessed")]
        public int Reprocessed { get; set; }

        [JsonProperty("FailureCount")]
        public int FailureCount { get; set; }

        [JsonProperty("StartDate")]
        public DateTime? StartDate { get; set; }

        [JsonProperty("EndDate")]
        public DateTime? EndDate { get; set; }

        [JsonProperty("SourceSystems")]
        public string? SourceSystems { get; set; }

        [JsonProperty("ProjectType")]
        public string? ProjectType { get; set; }
    }

    /// <summary>
    /// Financial Project model
    /// </summary>
    public class FinancialProject
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("projectId")]
        public string? ProjectId { get; set; }

        [JsonProperty("projectName")]
        public string? ProjectName { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("amount")]
        public decimal Amount { get; set; }

        [JsonProperty("createdDate")]
        public DateTime CreatedDate { get; set; }

        [JsonProperty("updatedDate")]
        public DateTime? UpdatedDate { get; set; }
    }

    /// <summary>
    /// Customer Order model
    /// </summary>
    public class CustomerOrder
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("orderId")]
        public string? OrderId { get; set; }

        [JsonProperty("customerName")]
        public string? CustomerName { get; set; }

        [JsonProperty("orderDate")]
        public DateTime OrderDate { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }

        [JsonProperty("status")]
        public string? Status { get; set; }

        [JsonProperty("totalAmount")]
        public decimal TotalAmount { get; set; }

        [JsonProperty("currency")]
        public string? Currency { get; set; }

        [JsonProperty("billingStatus")]
        public string? BillingStatus { get; set; }

        [JsonProperty("billingDate")]
        public DateTime? BillingDate { get; set; }

        [JsonProperty("invoiceNumber")]
        public string? InvoiceNumber { get; set; }
    }

    /// <summary>
    /// Invoice Reconciliation model
    /// </summary>
    public class InvoiceReconciliation
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("invoiceId")]
        public string? InvoiceId { get; set; }

        [JsonProperty("reconciliationStatus")]
        public string? ReconciliationStatus { get; set; }

        [JsonProperty("discrepancyAmount")]
        public decimal DiscrepancyAmount { get; set; }

        [JsonProperty("reconciliationDate")]
        public DateTime? ReconciliationDate { get; set; }

        [JsonProperty("source")]
        public string? Source { get; set; }
    }

    /// <summary>
    /// Error response model
    /// </summary>
    public class ErrorResponse
    {
        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("statusCode")]
        public int StatusCode { get; set; }

        [JsonProperty("details")]
        public Dictionary<string, string>? Details { get; set; }
    }
}
