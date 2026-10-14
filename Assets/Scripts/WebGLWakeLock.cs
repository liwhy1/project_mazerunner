using System.Runtime.InteropServices;
using UnityEngine;

public class WebGLWakeLock : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RequestWakeLock();

    [DllImport("__Internal")]
    private static extern void ReleaseWakeLock();
#endif

    public void Enable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        RequestWakeLock();
#endif
    }

    public void Disable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ReleaseWakeLock();
#endif
    }
}