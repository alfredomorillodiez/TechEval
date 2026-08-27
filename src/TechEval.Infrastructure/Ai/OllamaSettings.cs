namespace TechEval.Infrastructure.Ai;

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen2.5-coder:14b";
    public double Temperature { get; set; } = 0.6;
    public double RepeatPenalty { get; set; } = 1.3;
    public int NumCtx { get; set; } = 8192;
    public int NumPredict { get; set; } = 2048;
}
