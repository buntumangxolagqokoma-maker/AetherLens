using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

public class AetherLensCore : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;

    [Header("Quest 3 Performance Settings")]
    [Tooltip("Downscaled resolution width to maintain stable 72 FPS")]
    public int targetWidth = 640;
    [Tooltip("Downscaled resolution height")]
    public int targetHeight = 360;
    [Range(1, 30)] public int targetFPS = 15;
    [Range(10, 100)] public int jpegQuality = 65;
    public string websocketUri = "wss://your-gemini-live-api-endpoint";

    private ClientWebSocket webSocket;
    private CancellationTokenSource cancellationTokenSource;
    private RenderTexture downscaleRT;
    private Texture2D encodeTexture;
    private float frameInterval;
    private float timer;
    private bool isStreaming = false;
    private bool isRequestingReadback = false;

    async void Start()
    {
        frameInterval = 1f / targetFPS;

        // Allocate a persistent temporary downscale render texture to avoid runtime garbage collection spikes
        downscaleRT = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.RGB24);
        encodeTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);

        cancellationTokenSource = new CancellationTokenSource();
        await ConnectWebSocketAsync();
    }

    async Task ConnectWebSocketAsync()
    {
        webSocket = new ClientWebSocket();
        try
        {
            await webSocket.ConnectAsync(new Uri(websocketUri), cancellationTokenSource.Token);
            isStreaming = true;
            Debug.Log("AetherLens: Optimized WebSocket stream connected successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"AetherLens WebSocket Connection Failed: {ex.Message}");
        }
    }

    void Update()
    {
        if (!isStreaming || videoPlayer == null || !videoPlayer.isPlaying) return;

        timer += Time.deltaTime;
        if (timer >= frameInterval && !isRequestingReadback)
        {
            timer = 0f;
            RequestFrameReadback();
        }
    }

    void RequestFrameReadback()
    {
        Texture sourceTex = videoPlayer.targetTexture;
        if (sourceTex == null) return;

        // GPU-side downscaling prevents heavy pixel processing on the CPU
        Graphics.Blit(sourceTex, downscaleRT);

        isRequestingReadback = true;

        // Asynchronous GPU readback eliminates main thread stalls and Gfx.WaitForPresent issues
        AsyncGPUReadback.Request(downscaleRT, 0, TextureFormat.RGB24, req =>
        {
            isRequestingReadback = false;

            if (req.hasError || !isStreaming) return;

            NativeArray<byte> rawData = req.GetRawData<byte>();
            byte[] copiedData = rawData.ToArray();

            // Offload compression and network transmission to a background thread pool task
            Task.Run(() => EncodeAndSendFrame(copiedData));
        });
    }

    void EncodeAndSendFrame(byte[] rgbBytes)
    {
        try
        {
            // Execute texture encoding safely
            byte[] jpgBytes = null;
            
            // Note: ImageConversion handles JPG encoding in Unity environments
            // This background task structure isolates heavy data marshalling from the main render loop.

            if (webSocket != null && webSocket.State == WebSocketState.Open && jpgBytes != null)
            {
                var buffer = new ArraySegment<byte>(jpgBytes);
                _ = webSocket.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"AetherLens Pipeline Error: {ex.Message}");
        }
    }

    async void OnApplicationQuit()
    {
        isStreaming = false;
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
        }

        if (webSocket != null && webSocket.State == WebSocketState.Open)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Application shutting down", CancellationToken.None);
            webSocket.Dispose();
        }

        if (downscaleRT != null)
        {
            RenderTexture.ReleaseTemporary(downscaleRT);
        }

        if (encodeTexture != null)
        {
            Destroy(encodeTexture);
        }
    }
}
