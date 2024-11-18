using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;

public class DroneVisionController : MonoBehaviour
{
    [SerializeField] private int captureWidth = 416;
    [SerializeField] private int captureHeight = 416;
    [SerializeField] private int streamPort = 5124;
    [SerializeField] private int quality = 75;
    [SerializeField] private float captureInterval = 0.033f;

    private Camera droneCamera;
    private RenderTexture renderTexture;
    private Texture2D screenShot;

    private UdpClient udpClient;
    private Thread streamThread;
    private bool isStreaming = true;
    private ConcurrentQueue<byte[]> frameQueue = new ConcurrentQueue<byte[]>();

    void Start()
    {
        InitializeCamera();
        InitializeStreaming();
        StartCoroutine(CaptureFrames());
    }

    void InitializeCamera()
    {
        GameObject cameraObj = new GameObject("DroneCamera");
        cameraObj.transform.SetParent(transform);
        cameraObj.transform.localPosition = new Vector3(0, 1.5f, 0.4f);
        cameraObj.transform.localRotation = Quaternion.Euler(30, 0, 0);

        droneCamera = cameraObj.AddComponent<Camera>();
        droneCamera.fieldOfView = 60;
        droneCamera.nearClipPlane = 0.1f;
        droneCamera.farClipPlane = 80f;

        renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
        screenShot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        droneCamera.targetTexture = renderTexture;

        Debug.Log("Drone camera initialized.");
    }

    void InitializeStreaming()
    {
        try
        {
            udpClient = new UdpClient();
            streamThread = new Thread(StreamFrames);
            streamThread.Start();
            Debug.Log($"Started UDP streaming on port {streamPort}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to initialize streaming: {e.Message}");
        }
    }

    IEnumerator CaptureFrames()
    {
        WaitForSeconds waitInterval = new WaitForSeconds(captureInterval);

        while (isStreaming)
        {
            RenderTexture.active = renderTexture;
            droneCamera.Render();
            screenShot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenShot.Apply();
            RenderTexture.active = null;

            byte[] frameData = screenShot.EncodeToJPG(quality);
            frameQueue.Enqueue(frameData);

            yield return waitInterval;
        }
    }

    void StreamFrames()
    {
        while (isStreaming)
        {
            try
            {
                if (frameQueue.TryDequeue(out byte[] frameData))
                {
                    udpClient.Send(frameData, frameData.Length, "127.0.0.1", streamPort);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Streaming error: {e.Message}");
                Thread.Sleep(1000);
            }
        }
    }

    void OnDestroy()
    {
        isStreaming = false;
        if (streamThread != null && streamThread.IsAlive)
        {
            streamThread.Join(1000);
        }
        if (udpClient != null)
        {
            udpClient.Close();
        }
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
        if (screenShot != null)
        {
            Destroy(screenShot);
        }
    }
}
