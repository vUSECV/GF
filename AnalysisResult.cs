using OpenCvSharp;

namespace StateTracker;

public class AnalysisResult
{
    public bool FaceDetected { get; set; }
    public double LeftEyeOpenness { get; set; }
    public double RightEyeOpenness { get; set; }
    public double AvgEyeOpenness => (LeftEyeOpenness + RightEyeOpenness) / 2.0;
    public bool IsBlinking { get; set; }
    public int BlinksPerMinute { get; set; }
    public int TotalBlinks { get; set; }
    public double HeadTiltAngle { get; set; }
    public FatigueState Fatigue { get; set; }
    public FocusState Focus { get; set; }
    public StressState Stress { get; set; }
    public string Message { get; set; } = "";
    public Rect FaceRect { get; set; }
    public List<Rect> EyeRects { get; set; } = new();
}

public enum FatigueState { Normal, Tired, VeryTired }
public enum FocusState { Good, Distracted, Unknown }
public enum StressState { Calm, Nervous, Stressed }