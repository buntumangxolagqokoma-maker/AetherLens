using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AetherLensManager : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Your secure Gemini Multimodal Live WebSocket endpoint URI")]
    public string geminiWebSocketUri = "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1alpha.GenerativeService.BidiGenerateContent";
    public string apiKey = "YOUR_GEMINI_API_KEY";

    [Header("References")]
    public AetherLensCore streamerCore;

    private ClientWebSocket clientWebSocket;
    private CancellationTokenSource cts;

    async void Start()
    {
        if (streamerCore == null)
        {
            streamerCore = GetComponent<AetherLensCore>();
        }

        cts = new CancellationTokenSource();
        await InitializeAetherLensSessionAsync();
    }

    async Task InitializeAetherLensSessionAsync()
    {
        clientWebSocket = new ClientWebSocket();
        
        // Append API key header or query parameter depending on endpoint requirements
        Uri uri = new Uri($"{geminiWebSocketUri}?key={apiKey}");

        try
        {
            Debug.Log("AetherLens Manager: Connecting to Gemini Multimodal Live API...");
            await clientWebSocket.ConnectAsync(uri, cts.Token);
            Debug.Log("AetherLens Manager: WebSocket Connected successfully.");

            // Send initial system instructions based on the CAPS curriculum configuration
            await SendSystemInstructionsAsync();
        }
    }

    async Task SendSystemInstructionsAsync()
    {
        if (clientWebSocket.State != WebSocketState.Open) return;

        string systemPrompt = AetherLensConfig.GetSystemInstruction();
        
        // Construct the initial configuration payload for Gemini Live API
        string jsonPayload = $@"{{
            ""setup"": {{
                ""model"": ""models/gemini-2.0-flash-exp"",
                ""generation_config"": {{
                    ""response_modalities"": [""AUDIO""]
                }},
                ""system_instruction"": {{
                    ""parts"": [
                        {{ ""text"": ""{systemPrompt}"" }}
                    ]
                }}
            }}
        }}";

        byte[] payloadBytes = Encoding.UTF8.GetBytes(jsonPayload);
        await clientWebSocket.SendAsync(
            new ArraySegment<byte>(payloadBytes),
            WebSocketMessageType.Text,
            true,
            cts.Token
        );

        Debug.Log("AetherLens Manager: CAPS Curriculum System Instructions transmitted to Gemini.");
    }

    async void OnApplicationQuit()
    {
        if (cts != null)
        {
            cts.Cancel();
        }

        if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Session ended", CancellationToken.None);
            clientWebSocket.Dispose();
        }
    }
}
