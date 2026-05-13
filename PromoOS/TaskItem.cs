namespace PromoOS
{
    public enum Priority
    {
        Low,
        Medium,
        High
    }

    public class TaskItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; }
        public bool IsCompleted { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public Priority Priority { get; set; }
        public byte[]? RowVersion { get; set; }
    }
}