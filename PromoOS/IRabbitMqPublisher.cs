namespace PromoOS
{
    public interface IRabbitMqPublisher
    {
        void PublishTaskCompleted(TaskItem task);
    }
}