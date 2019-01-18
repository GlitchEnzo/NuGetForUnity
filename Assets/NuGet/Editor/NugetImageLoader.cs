using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace NugetForUnity
{
    internal class Request:IDisposable {

        public const string CachePrefix = "file:///";

        public string Url { get; private set; }
        public WWW www { get; private set; }
        private readonly List<Action<Texture2D>> _callBacks;

        internal Request(string url, Action<Texture2D> callBack) {
            this.Url = url;
            www = new WWW(url);
            _callBacks = new List<Action<Texture2D>>();
            AddCallBack(callBack);
        }

        internal void AddCallBack(Action<Texture2D> callBack) {
            _callBacks.Add(callBack);
        }

        internal void InvokeCallBacks(byte[] bytes) {
            if (bytes == null) {
                foreach (Action<Texture2D> callBack in _callBacks) {
                    if(callBack != null)
                        callBack(null);
                }
                _callBacks.Clear();
                return;
            }
            var loadedTexture = new Texture2D(0, 0, TextureFormat.ARGB32, false);
            loadedTexture.LoadImage(bytes);

            foreach (Action<Texture2D> callBack in _callBacks) {
                if(callBack!=null)
                    callBack(loadedTexture);
            }
            _callBacks.Clear();

        }


        public bool IsFromCache() {
            return Url.StartsWith(CachePrefix);
        }

        public void Dispose() {
            if(www!=null)
                www.Dispose();
        }
    }

    [InitializeOnLoad]
    public class NugetImageLoader {
        static NugetImageLoader() {
            EditorApplication.update += Update;
        }

        private static void Update() {
            foreach (var e in Enumerators.Values.ToList())
                e.MoveNext();
        }


        public static event Action OnCompleteLoad;

        private static readonly Dictionary<string, Request> Requests = new Dictionary<string, Request>();
        private static readonly Dictionary<string, IEnumerator> Enumerators = new Dictionary<string, IEnumerator>();

        public static IDisposable TryDownloadImage(string url, Action<Texture2D> callBack) {
            if (string.IsNullOrEmpty(url))
                return null;

            if (ExistsInDiskCache(url))
                url = Request.CachePrefix + GetFilePath(url);

            if (Requests.ContainsKey(url)) {
                Requests[url].AddCallBack(callBack);
                return Requests[url].www;
            }

            var request = new Request(url, callBack);
            Requests.Add(url, request);
            var enumerator = GetEnumerator(request);
            Enumerators.Add(url, enumerator);
            return request.www;
        }

        private static IEnumerator GetEnumerator(Request request) {

            var www = request.www;
            while (!www.isDone) {
                yield return www;
            }

            var url = request.Url;
            if (string.IsNullOrEmpty(www.error)) {
                if (!request.IsFromCache())
                    CacheTextureOnDisk(url, www.bytes);
                
                request.InvokeCallBacks(www.bytes);
            }
            else {
                NugetHelper.LogVerbose("Request error: " + www.error);
                request.InvokeCallBacks(null);
            }

            www.Dispose();

            Enumerators.Remove(url);
            Requests.Remove(url);

            if (OnCompleteLoad != null)
                OnCompleteLoad();
        }

        private static void CacheTextureOnDisk(string url, byte[] bytes) {
            string diskPath = GetFilePath(url);
            File.WriteAllBytes(diskPath, bytes);
        }

        private static bool ExistsInDiskCache(string url) {
            return File.Exists(GetFilePath(url));
        }

        private static string GetFilePath(string url) {
            return Path.Combine(Application.temporaryCachePath, GetHash(url));
        }

        private static string GetHash(string s) {
            if (string.IsNullOrEmpty(s))
                return null;
            MD5CryptoServiceProvider md5 = new MD5CryptoServiceProvider();
            byte[] data = md5.ComputeHash(Encoding.Default.GetBytes(s));
            StringBuilder sBuilder = new StringBuilder();
            for (int i = 0; i < data.Length; i++) {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }
    }
}
