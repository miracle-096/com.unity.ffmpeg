using System;
using System.Collections.Generic;
using System.Diagnostics;
using FFmpegCode.FFmpegHelpers;
using Standalone;
using UnityEditor;
using UnityEngine;

#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace FFmpegCode
{
    public class FFmpegUtil
    {
#if UNITY_IOS && !UNITY_EDITOR

        [System.Security.SuppressUnmanagedCodeSecurity()]
        //void* execute(char** argv, int argc, void (* callback)(const char*))
        [DllImport("__Internal")]
        static extern void execute(string[] argv, int argc, IOSCallback callback);

        delegate void IOSCallback(string msg);
		[AOT.MonoPInvokeCallback(typeof(IOSCallback))]
		static void IOSCallbacFunc(string message)
		{
			callbackMSGs.Enqueue(message);
		}

#elif UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass unityClass;
        AndroidJavaObject pluginClass;

        //------------------------------

        class AndroidCallback : AndroidJavaProxy
        {
            public AndroidCallback() : base("com.botvinev.max.unityplugin.CallbackNotifier") { }
            void onEnd(string message)
            {
                callbackMSGs.Enqueue(message);
            }
        }
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
        private static void StandaloneCallback(string message)
        {
            callbackMSGs.Enqueue(message);
        }
#endif
        private static Queue<string> callbackMSGs = new Queue<string>();

        //------------------------------
        [InitializeOnLoadMethod]
        private static void Init()
        {
#if UNITY_IOS && !UNITY_EDITOR
			//IOS implementation doesn't need initialization
#elif UNITY_ANDROID && !UNITY_EDITOR
            unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            pluginClass = new AndroidJavaObject("com.botvinev.max.unityplugin.VideoProcessing");
            pluginClass.Call(
                "Begin",
                unityClass.GetStatic<AndroidJavaObject>("currentActivity"), //Context
                new AndroidCallback());
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
            StandaloneProxy.Begin(StandaloneCallback);
            EditorApplication.update -= Update; 
            EditorApplication.update += Update;
#else
            Debug.LogWarning("FFmpeg is not implemented for " + Application.platform);
#endif
		}

        public static void Execute(Action<bool> onFinishAction,params string[] cmd)
        {
#if UNITY_IOS && !UNITY_EDITOR
            execute(cmd, cmd.Length, IOSCallbacFunc);
#elif UNITY_ANDROID && !UNITY_EDITOR
            pluginClass.Call(
                "Process",
                unityClass.GetStatic<AndroidJavaObject>("currentActivity"),  //Context
                cmd,
                new AndroidCallback());
#elif UNITY_STANDALONE_OSX || UNITY_STANDALONE_WIN || UNITY_EDITOR
            StandaloneProxy.Execute(string.Join(" ", cmd),onFinishAction);
#else
            Debug.LogWarning("FFmpeg is not implemented for " + Application.platform);
#endif
        }

        private static void Update()
        {
            if (callbackMSGs.Count > 0)
            {
                FFmpegParser.Handle(callbackMSGs.Dequeue());
            }
        }
    }
}