using TMPro;
using UnityEngine;

public class PerformanceStatsDisplay : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Text field that displays the current FPS.")]
    public TextMeshProUGUI fpsText;

    [Tooltip("Text field that displays the average frame time in milliseconds.")]
    public TextMeshProUGUI frameTimeText;

    [Tooltip("Text field that displays the accumulated dropped frame count.")]
    public TextMeshProUGUI droppedFramesText;

    [Header("Sampling")]
    [Tooltip("How often (seconds) the UI text should be refreshed.")]
    [Range(0.1f, 5f)]
    public float updateInterval = 0.5f;

    [Tooltip("Window (seconds) used to compute running averages. Leave <=0 to match update interval.")]
    [Range(0f, 10f)]
    public float averagingWindow = 0f;

    private float _timer;
    private float _accumulatedDelta;
    private int _frameCounter;
    private int _framesSinceLastUpdate;
    private double _droppedFramesTotal;
    private double _droppedFramesInterval;
    private float _targetFrameDelta;
    private float _avgWindowTimer;

    private void Awake()
    {
        // Determine the expected frame interval from target frame rate or display refresh.
        if (Application.targetFrameRate > 0)
        {
            _targetFrameDelta = 1f / Application.targetFrameRate;
        }
        else
        {
            // Fallback to the current display refresh rate if no explicit target is set.
            var refresh = Screen.currentResolution.refreshRate;
            _targetFrameDelta = refresh > 0 ? 1f / refresh : 0f;
        }

        if (averagingWindow <= 0f)
        {
            averagingWindow = updateInterval;
        }
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        _timer += deltaTime;
        _avgWindowTimer += deltaTime;
        _accumulatedDelta += deltaTime;
    _frameCounter++;
    _framesSinceLastUpdate++;

        // Estimate dropped frames whenever we know the expected cadence.
        if (_targetFrameDelta > 0f)
        {
            int extraFrames = Mathf.Max(0, Mathf.FloorToInt(deltaTime / _targetFrameDelta) - 1);
            if (extraFrames > 0)
            {
                _droppedFramesTotal += extraFrames;
                _droppedFramesInterval += extraFrames;
            }
        }

        // Slide averaging window so the numbers stay responsive even with longer update intervals.
        if (_avgWindowTimer > averagingWindow)
        {
            float excess = _avgWindowTimer - averagingWindow;
            // Remove a proportional share from the accumulators to keep the window size roughly constant.
            if (_avgWindowTimer > 0f)
            {
                float ratio = Mathf.Clamp01(excess / _avgWindowTimer);
                _accumulatedDelta *= 1f - ratio;
                _frameCounter = Mathf.Max(1, Mathf.RoundToInt(_frameCounter * (1f - ratio)));
                _avgWindowTimer = averagingWindow;
            }
        }

        if (_timer < updateInterval)
            return;

        float averageDelta = _frameCounter > 0 ? _accumulatedDelta / _frameCounter : 0f;
        float fps = averageDelta > 0f ? 1f / averageDelta : 0f;
        float frameTimeMs = averageDelta * 1000f;

        if (fpsText)
        {
            fpsText.text = fps > 0f ? $"FPS: {fps:0.0}" : "FPS: --";
        }

        if (frameTimeText)
        {
            frameTimeText.text = frameTimeMs > 0f ? $"Frame Time: {frameTimeMs:0.0} ms" : "Frame Time: --";
        }

        if (droppedFramesText)
        {
            if (_targetFrameDelta > 0f)
            {
                double totalFrames = Time.unscaledTime / _targetFrameDelta;
                double totalPercent = totalFrames > 0.0 ? (_droppedFramesTotal / totalFrames) * 100.0 : 0.0;
                double intervalExpected = _framesSinceLastUpdate + _droppedFramesInterval;
                double intervalPercent = intervalExpected > 0.0 ? (_droppedFramesInterval / intervalExpected) * 100.0 : 0.0;
                droppedFramesText.text =
                    $"Dropped Frames Total: {_droppedFramesTotal:0} ({totalPercent:0.0}%)\n" +
                    $"Dropped Frames Interval: {_droppedFramesInterval:0} ({intervalPercent:0.0}%)";
            }
            else
            {
                droppedFramesText.text =
                    $"Dropped Frames Total: {_droppedFramesTotal:0}\n" +
                    $"Dropped Frames Interval: {_droppedFramesInterval:0}";
            }
        }

        _timer = 0f;
        _accumulatedDelta = 0f;
        _frameCounter = 0;
        _framesSinceLastUpdate = 0;
        _droppedFramesInterval = 0;
        _avgWindowTimer = 0f;
    }
}
