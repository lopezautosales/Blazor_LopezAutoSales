using System;

namespace LopezAutoSales.Server.Models
{
    // Durable, append-only record of money-affecting admin actions. The console/Serilog
    // log is short-lived on Railway, and a payment-edit/delete Reason is [NotMapped] on
    // Payment (never persisted). This table keeps that history in the DB, so it is also
    // captured by the database backups.
    public class AuditLog
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string User { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }
}
