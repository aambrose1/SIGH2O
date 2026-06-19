namespace app.Models;

public class SensorLogs
{
    public int Id { get; set; }
    public double MoistureValue { get; set; }
    public DateTime TimestampUtc { get; set; }
}