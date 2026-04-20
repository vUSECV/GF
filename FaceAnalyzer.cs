using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace StateTracker;

public class FaceAnalyzer : IDisposable
{
    private readonly CascadeClassifier _faceDetector;
    private readonly CascadeClassifier _eyeDetector;

    private int _totalBlinks = 0;
    private int _consecutiveClosedFrames = 0;
    private bool _isBlinking = false;
    private readonly Queue<DateTime> _blinkTimes = new();

    private double _lastLeftOpenness = 0.3;
    private double _lastRightOpenness = 0.3;

    public FaceAnalyzer()
    {
        _faceDetector = new CascadeClassifier("haarcascade_frontalface_default.xml");
        _eyeDetector = new CascadeClassifier("haarcascade_eye.xml");
    }

    public AnalysisResult Analyze(Mat frame)
    {
        var result = new AnalysisResult();
        if (frame.Empty()) return result;

        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        var faces = _faceDetector.DetectMultiScale(gray, 1.1, 5, HaarDetectionTypes.ScaleImage, new Size(80, 80));

        if (faces.Length == 0)
        {
            result.FaceDetected = false;
            return result;
        }

        var face = faces[0];
        result.FaceDetected = true;
        result.FaceRect = face;

        // Определяем зону поиска глаз
        int eyeTop = face.Y + face.Height / 6;
        int eyeHeight = face.Height / 3;
        var eyeAreaRect = new Rect(face.X, eyeTop, face.Width, eyeHeight);

        using var eyeRoi = new Mat(gray, eyeAreaRect);
        var eyes = _eyeDetector.DetectMultiScale(eyeRoi, 1.1, 4);

        bool eyesLost = (eyes.Length == 0); // ГЛАЗА ПОТЕРЯНЫ
        
        double left = _lastLeftOpenness;
        double right = _lastRightOpenness;

        if (!eyesLost)
        {
            var sorted = eyes.OrderBy(e => e.X).Take(2).ToArray();
            var leftEye = ConvertRect(sorted[0], eyeAreaRect);
            result.EyeRects.Add(leftEye);
            left = AnalyzeEye(gray, leftEye);

            if (sorted.Length > 1)
            {
                var rightEye = ConvertRect(sorted[1], eyeAreaRect);
                result.EyeRects.Add(rightEye);
                right = AnalyzeEye(gray, rightEye);
            }
        }

        left = Smooth(_lastLeftOpenness, left);
        right = Smooth(_lastRightOpenness, right);
        _lastLeftOpenness = left;
        _lastRightOpenness = right;

        result.LeftEyeOpenness = left;
        result.RightEyeOpenness = right;

        // --- НОВАЯ ЛОГИКА МОРГАНИЯ ---
        // Считаем закрытыми, если глаза потеряны ИЛИ если их яркость/открытость ниже порога
        bool isClosed = eyesLost || ((left + right) / 2.0 < 0.22);

        if (isClosed)
        {
            _consecutiveClosedFrames++;
            if (!_isBlinking && _consecutiveClosedFrames >= 2) // Минимум 2 кадра чтобы не было шума
            {
                _isBlinking = true;
                _totalBlinks++;
                _blinkTimes.Enqueue(DateTime.Now);
            }
        }
        else
        {
            _consecutiveClosedFrames = 0;
            _isBlinking = false;
        }

        result.IsBlinking = _isBlinking;

        // Очистка старых данных для BPM
        while (_blinkTimes.Count > 0 && (DateTime.Now - _blinkTimes.Peek()).TotalSeconds > 60)
            _blinkTimes.Dequeue();

        result.BlinksPerMinute = _blinkTimes.Count;
        result.TotalBlinks = _totalBlinks;
        result.Fatigue = EvaluateFatigue(result.BlinksPerMinute);
        result.Stress = EvaluateStress(result.BlinksPerMinute);
        result.Focus = EvaluateFocus(result);
        result.Message = GenerateMessage(result);

        return result;
    }

    private Rect ConvertRect(Rect r, Rect offset) => new Rect(r.X + offset.X, r.Y + offset.Y, r.Width, r.Height);
    private double Smooth(double prev, double current) => prev * 0.6 + current * 0.4;

    private double AnalyzeEye(Mat gray, Rect rect)
    {
        try
        {
            if (rect.Width < 5 || rect.Height < 5) return 0.3;
            using var roi = new Mat(gray, rect);
            using var blur = new Mat();
            Cv2.GaussianBlur(roi, blur, new Size(5, 5), 0);
            using var thresh = new Mat();
            Cv2.Threshold(blur, thresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
            return Math.Clamp((double)Cv2.CountNonZero(thresh) / (rect.Width * rect.Height), 0.05, 0.4);
        }
        catch { return 0.3; }
    }

    private FatigueState EvaluateFatigue(int bpm) => (bpm > 0 && bpm < 7) ? FatigueState.Tired : (bpm > 35 ? FatigueState.VeryTired : FatigueState.Normal);
    private StressState EvaluateStress(int bpm) => bpm > 35 ? StressState.Stressed : (bpm > 25 ? StressState.Nervous : StressState.Calm);
    private FocusState EvaluateFocus(AnalysisResult r) => (r.Fatigue != FatigueState.Normal || r.Stress == StressState.Stressed) ? FocusState.Distracted : FocusState.Good;

    private string GenerateMessage(AnalysisResult r)
    {
        if (r.Fatigue == FatigueState.VeryTired) return "🚨 Срочно отдохните!";
        if (r.Fatigue == FatigueState.Tired) return "😴 Похоже, вы устали";
        return "✅ Состояние в норме";
    }

    public void Dispose()
    {
        _faceDetector?.Dispose();
        _eyeDetector?.Dispose();
    }
}