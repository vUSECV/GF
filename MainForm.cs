using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace StateTracker;

public class MainForm : Form
{
    private VideoCapture? _camera;
    private FaceAnalyzer? _analyzer;
    private Thread? _cameraThread;
    private volatile bool _isRunning;
    
    private PictureBox _videoBox = null!;
    private Label _bpmValue = null!;
    private Label _fatigueValue = null!;
    private Label _focusValue = null!;
    private Label _stressValue = null!;
    private Label _totalBlinksValue = null!;
    private Label _statusLabel = null!;
    private Button _startButton = null!;
    private Button _stopButton = null!;
    private CheckBox _debugCheckbox = null!;
    
    private int _frameCount = 0;
    private int _currentFps = 0;
    private System.Windows.Forms.Timer _fpsTimer = null!;
    
    public MainForm()
    {
        InitializeForm();
        SetupUI();
        SetupFpsTimer();
    }
    
    private void InitializeForm()
    {
        this.Text = "StateTracker - Мониторинг состояния";
        this.Size = new Size(1280, 720);
        this.BackColor = Color.FromArgb(15, 15, 20);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormClosing += (s, e) => StopCamera();
    }
    
    private void SetupUI()
    {
        _videoBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(8, 8, 12)
        };
        
        var rightPanel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 320,
            BackColor = Color.FromArgb(20, 20, 28),
            Padding = new Padding(20)
        };
        
        var title = new Label
        {
            Text = "STATE TRACKER",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 200, 80),
            Dock = DockStyle.Top,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter
        };
        rightPanel.Controls.Add(title);
        
        int y = 80;
        
        var bpmLabel = CreateLabel("МОРГАНИЙ/МИН", y);
        _bpmValue = CreateValue("0", y + 30);
        rightPanel.Controls.Add(bpmLabel);
        rightPanel.Controls.Add(_bpmValue);
        y += 80;
        
        var fatigueLabel = CreateLabel("УСТАЛОСТЬ", y);
        _fatigueValue = CreateValue("Норма", y + 30);
        rightPanel.Controls.Add(fatigueLabel);
        rightPanel.Controls.Add(_fatigueValue);
        y += 80;
        
        var focusLabel = CreateLabel("ФОКУС", y);
        _focusValue = CreateValue("Хороший", y + 30);
        rightPanel.Controls.Add(focusLabel);
        rightPanel.Controls.Add(_focusValue);
        y += 80;
        
        var stressLabel = CreateLabel("СТРЕСС", y);
        _stressValue = CreateValue("Низкий", y + 30);
        rightPanel.Controls.Add(stressLabel);
        rightPanel.Controls.Add(_stressValue);
        y += 80;
        
        var totalLabel = CreateLabel("ВСЕГО МОРГАНИЙ", y);
        _totalBlinksValue = CreateValue("0", y + 30);
        rightPanel.Controls.Add(totalLabel);
        rightPanel.Controls.Add(_totalBlinksValue);
        y += 90;
        
        _statusLabel = new Label
        {
            Text = "⚡ Нажмите Запустить",
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.FromArgb(150, 150, 160),
            Location = new Point(20, y),
            Size = new Size(280, 50),
            TextAlign = ContentAlignment.MiddleLeft
        };
        rightPanel.Controls.Add(_statusLabel);
        y += 60;
        
        _startButton = new Button
        {
            Text = "▶ ЗАПУСТИТЬ",
            BackColor = Color.FromArgb(40, 140, 40),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Location = new Point(20, y),
            Size = new Size(130, 45),
            Cursor = Cursors.Hand
        };
        _startButton.FlatAppearance.BorderSize = 0;
        _startButton.Click += (s, e) => StartCamera();
        
        _stopButton = new Button
        {
            Text = "⏹ ОСТАНОВИТЬ",
            BackColor = Color.FromArgb(100, 40, 40),
            ForeColor = Color.FromArgb(150, 150, 150),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Location = new Point(165, y),
            Size = new Size(130, 45),
            Enabled = false,
            Cursor = Cursors.Hand
        };
        _stopButton.FlatAppearance.BorderSize = 0;
        _stopButton.Click += (s, e) => StopCamera();
        
        rightPanel.Controls.Add(_startButton);
        rightPanel.Controls.Add(_stopButton);
        
        _debugCheckbox = new CheckBox
        {
            Text = "Показывать зоны глаз",
            ForeColor = Color.White,
            Location = new Point(20, y + 55),
            Size = new Size(200, 25),
            Checked = true
        };
        rightPanel.Controls.Add(_debugCheckbox);
        
        this.Controls.Add(_videoBox);
        this.Controls.Add(rightPanel);
    }
    
    private Label CreateLabel(string text, int y)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Color.FromArgb(120, 120, 140),
            Location = new Point(20, y),
            Size = new Size(280, 25)
        };
    }
    
    private Label CreateValue(string text, int y)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, y),
            Size = new Size(280, 40)
        };
    }
    
    private void SetupFpsTimer()
    {
        _fpsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _fpsTimer.Tick += (s, e) => { _currentFps = _frameCount; _frameCount = 0; };
    }
    
    private void StartCamera()
    {
        try
        {
            _camera = new VideoCapture(0);
            if (!_camera.IsOpened())
            {
                MessageBox.Show("Не удалось открыть камеру!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            _analyzer = new FaceAnalyzer();
            _isRunning = true;
            
            _startButton.Enabled = false;
            _stopButton.Enabled = true;
            _stopButton.ForeColor = Color.White;
            _stopButton.BackColor = Color.FromArgb(180, 50, 50);
            
            _fpsTimer.Start();
            
            _cameraThread = new Thread(CameraLoop) { IsBackground = true };
            _cameraThread.Start();
            
            _statusLabel.Text = "🟢 Мониторинг запущен. Поморгайте для калибровки";
            _statusLabel.ForeColor = Color.FromArgb(80, 200, 80);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
    
    private void StopCamera()
    {
        _isRunning = false;
        _cameraThread?.Join(1000);
        
        _camera?.Release();
        _camera?.Dispose();
        _camera = null;
        
        _analyzer?.Dispose();
        _analyzer = null;
        
        _fpsTimer.Stop();
        
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        _stopButton.ForeColor = Color.FromArgb(150, 150, 150);
        _stopButton.BackColor = Color.FromArgb(100, 40, 40);
        
        _statusLabel.Text = "⏹ Остановлен";
        _statusLabel.ForeColor = Color.FromArgb(150, 150, 160);
        
        if (_videoBox.Image != null)
        {
            _videoBox.Image.Dispose();
            _videoBox.Image = null;
        }
    }
    
    private void CameraLoop()
    {
        using var frame = new Mat();
        
        while (_isRunning && _camera != null)
        {
            if (!_camera.Read(frame) || frame.Empty())
            {
                Thread.Sleep(10);
                continue;
            }
            
            _frameCount++;
            
            var result = _analyzer!.Analyze(frame);
            DrawOverlay(frame, result);
            
            if (this.IsHandleCreated)
            {
                var uiFrame = frame.Clone();
                this.BeginInvoke(() => 
                {
                    UpdateUI(uiFrame, result);
                    uiFrame.Dispose();
                });
            }
            
            Thread.Sleep(33);
        }
    }
    
    private void DrawOverlay(Mat frame, AnalysisResult result)
    {
        try
        {
            if (result.FaceDetected)
            {
                Cv2.Rectangle(frame, result.FaceRect, new Scalar(0, 255, 0), 2);
                
                if (_debugCheckbox.Checked && result.EyeRects.Count >= 2)
                {
                    foreach (var eye in result.EyeRects)
                    {
                        Cv2.Rectangle(frame, eye, new Scalar(255, 100, 100), 2);
                    }
                }
                
                string leftText = $"L: {result.LeftEyeOpenness:F2}";
                string rightText = $"R: {result.RightEyeOpenness:F2}";
                
                if (result.EyeRects.Count >= 2)
                {
                    Cv2.PutText(frame, leftText, 
                        new OpenCvSharp.Point(result.EyeRects[0].X, result.EyeRects[0].Y - 5),
                        HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 0), 1);
                    
                    Cv2.PutText(frame, rightText, 
                        new OpenCvSharp.Point(result.EyeRects[1].X, result.EyeRects[1].Y - 5),
                        HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 0), 1);
                }
                
                if (result.IsBlinking)
                {
                    Cv2.PutText(frame, "BLINK DETECTED!", 
                        new OpenCvSharp.Point(result.FaceRect.X + result.FaceRect.Width / 2 - 80, 
                            result.FaceRect.Y - 20),
                        HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 255, 255), 2);
                }
                
                if (result.Fatigue == FatigueState.VeryTired)
                {
                    Cv2.PutText(frame, "HIGH FATIGUE!", 
                        new OpenCvSharp.Point(20, 50),
                        HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 0, 255), 2);
                }
                else if (result.Fatigue == FatigueState.Tired)
                {
                    Cv2.PutText(frame, "TIRED", 
                        new OpenCvSharp.Point(20, 50),
                        HersheyFonts.HersheySimplex, 0.6, new Scalar(0, 150, 255), 2);
                }
                
                Cv2.PutText(frame, $"{_currentFps} FPS", 
                    new OpenCvSharp.Point(frame.Width - 70, 30),
                    HersheyFonts.HersheySimplex, 0.5, new Scalar(200, 200, 200), 1);
                
                if (result.TotalBlinks == 0)
                {
                    Cv2.PutText(frame, "Blink a few times to calibrate", 
                        new OpenCvSharp.Point(20, frame.Height - 20),
                        HersheyFonts.HersheySimplex, 0.5, new Scalar(200, 200, 100), 1);
                }
            }
            else
            {
                Cv2.PutText(frame, "No face detected", 
                    new OpenCvSharp.Point(20, 50),
                    HersheyFonts.HersheySimplex, 0.7, new Scalar(0, 0, 255), 2);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Draw error: {ex.Message}");
        }
    }
    
    private void UpdateUI(Mat frame, AnalysisResult result)
    {
        try
        {
            var bmp = BitmapConverter.ToBitmap(frame);
            
            var old = _videoBox.Image;
            _videoBox.Image = bmp;
            old?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UI error: {ex.Message}");
        }
        
        if (result.FaceDetected)
        {
            _statusLabel.Text = $"👁️ L:{result.LeftEyeOpenness:F2} R:{result.RightEyeOpenness:F2} | BPM:{result.BlinksPerMinute} | {result.Message}";
            
            _bpmValue.Text = result.BlinksPerMinute.ToString();
            if (result.BlinksPerMinute < 5 && result.TotalBlinks > 0)
                _bpmValue.ForeColor = Color.Red;
            else if (result.BlinksPerMinute < 10 && result.TotalBlinks > 0)
                _bpmValue.ForeColor = Color.Orange;
            else if (result.BlinksPerMinute > 30)
                _bpmValue.ForeColor = Color.Red;
            else
                _bpmValue.ForeColor = Color.LightGreen;
            
            _totalBlinksValue.Text = result.TotalBlinks.ToString();
            _totalBlinksValue.ForeColor = Color.White;
            
            switch (result.Fatigue)
            {
                case FatigueState.VeryTired:
                    _fatigueValue.Text = "🔴 ВЫСОКАЯ";
                    _fatigueValue.ForeColor = Color.Red;
                    break;
                case FatigueState.Tired:
                    _fatigueValue.Text = "🟡 СРЕДНЯЯ";
                    _fatigueValue.ForeColor = Color.Orange;
                    break;
                default:
                    _fatigueValue.Text = "🟢 НОРМА";
                    _fatigueValue.ForeColor = Color.LightGreen;
                    break;
            }
            
            switch (result.Focus)
            {
                case FocusState.Distracted:
                    _focusValue.Text = "🔴 РАССЕЯН";
                    _focusValue.ForeColor = Color.Red;
                    break;
                case FocusState.Unknown:
                    _focusValue.Text = "🟡 НЕИЗВЕСТНО";
                    _focusValue.ForeColor = Color.Orange;
                    break;
                default:
                    _focusValue.Text = "🟢 ХОРОШИЙ";
                    _focusValue.ForeColor = Color.LightGreen;
                    break;
            }
            
            switch (result.Stress)
            {
                case StressState.Stressed:
                    _stressValue.Text = "🔴 ВЫСОКИЙ";
                    _stressValue.ForeColor = Color.Red;
                    break;
                case StressState.Nervous:
                    _stressValue.Text = "🟡 СРЕДНИЙ";
                    _stressValue.ForeColor = Color.Orange;
                    break;
                default:
                    _stressValue.Text = "🟢 НИЗКИЙ";
                    _stressValue.ForeColor = Color.LightGreen;
                    break;
            }
        }
        else
        {
            _bpmValue.Text = "--";
            _fatigueValue.Text = "--";
            _focusValue.Text = "--";
            _stressValue.Text = "--";
            _totalBlinksValue.Text = "--";
            _statusLabel.Text = "🔴 Лицо не обнаружено. Посмотрите в камеру";
            _statusLabel.ForeColor = Color.Red;
        }
    }
}