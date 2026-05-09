using System.IO;
using System.Text;
using UnityEngine;

public sealed class ReplayFileWriter
{
    private int _flushInterval;
    private StreamWriter _framesWriter;
    private StreamWriter _eventsWriter;
    private int _pendingWrites;

    public ReplayFileWriter(int flushInterval = 60)
    {
        FlushInterval = flushInterval;
    }

    public int FlushInterval
    {
        get => _flushInterval;
        set => _flushInterval = Mathf.Max(1, value);
    }

    public string FolderPath { get; private set; }
    public string ManifestPath { get; private set; }
    public string FramesPath { get; private set; }
    public string EventsPath { get; private set; }
    public bool IsOpen => _framesWriter != null && _eventsWriter != null;

    public void Open(string folderPath)
    {
        Close();

        FolderPath = folderPath;
        Directory.CreateDirectory(FolderPath);

        ManifestPath = Path.Combine(FolderPath, "manifest.json");
        FramesPath = Path.Combine(FolderPath, "frames.jsonl");
        EventsPath = Path.Combine(FolderPath, "events.jsonl");

        _framesWriter = new StreamWriter(FramesPath, false, Encoding.UTF8);
        _eventsWriter = new StreamWriter(EventsPath, false, Encoding.UTF8);
        _pendingWrites = 0;
    }

    public void WriteManifest(ReplayManifest manifest)
    {
        if (manifest == null || string.IsNullOrEmpty(ManifestPath)) return;
        File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
    }

    public void WriteFrame(ReplayFrame frame)
    {
        if (_framesWriter == null || frame == null) return;
        _framesWriter.WriteLine(JsonUtility.ToJson(frame, false));
        FlushIfNeeded();
    }

    public void WriteEvent(ReplayEvent replayEvent)
    {
        if (_eventsWriter == null || replayEvent == null) return;
        _eventsWriter.WriteLine(JsonUtility.ToJson(replayEvent, false));
        FlushIfNeeded();
    }

    public void Flush()
    {
        _framesWriter?.Flush();
        _eventsWriter?.Flush();
        _pendingWrites = 0;
    }

    public void Close()
    {
        Flush();
        _framesWriter?.Dispose();
        _eventsWriter?.Dispose();
        _framesWriter = null;
        _eventsWriter = null;
    }

    private void FlushIfNeeded()
    {
        _pendingWrites++;
        if (_pendingWrites >= FlushInterval)
            Flush();
    }
}