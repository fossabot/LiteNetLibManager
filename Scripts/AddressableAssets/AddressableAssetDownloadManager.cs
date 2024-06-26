using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace LiteNetLibManager
{
    public partial class AddressableAssetDownloadManager : MonoBehaviour
    {
        public AssetReferenceDownloadManagerSettings settingsAssetReference;
        [Header("Events")]
        public UnityEvent onStart = new UnityEvent();
        public UnityEvent onEnd = new UnityEvent();
        public UnityEvent onFileSizeRetrieving = new UnityEvent();
        public AddressableAssetFileSizeEvent onFileSizeRetrieved = new AddressableAssetFileSizeEvent();
        public AddressableAssetTotalProgressEvent onDepsDownloading = new AddressableAssetTotalProgressEvent();
        public AddressableAssetTotalProgressEvent onDepsDownloaded = new AddressableAssetTotalProgressEvent();
        public AddressableAssetDownloadProgressEvent onDepsFileDownloading = new AddressableAssetDownloadProgressEvent();
        public UnityEvent onDownloadedAll = new UnityEvent();

        public long FileSize { get; protected set; } = 0;
        public int LoadedCount { get; protected set; } = 0;
        public int TotalCount { get; protected set; } = 0;

        private async void Start()
        {
            onStart?.Invoke();
            AsyncOperationHandle<AddressableAssetDownloadManagerSettings> settingsAsyncOp = settingsAssetReference.LoadAssetAsync();
            await settingsAsyncOp.Task;
            AddressableAssetDownloadManagerSettings settings = settingsAsyncOp.Result;
            TotalCount = settings.PrepareObjects.Count + settings.InitialObjects.Count;

            // Downloads
            for (int i = 0; i < settings.PrepareObjects.Count; ++i)
            {
                if (settings.PrepareObjects[i] == null ||
                    !settings.PrepareObjects[i].IsDataValid())
                {
                    // Invalid data
                    continue;
                }
                try
                {
                    await Download(
                        settings.PrepareObjects[i],
                        OnFileSizeRetrieving,
                        OnFileSizeRetrieved,
                        OnDepsDownloading,
                        OnDepsFileDownloading,
                        OnDepsDownloaded);
                }
                catch (System.Exception ex)
                {
                    Logging.LogException(ex);
                }
                LoadedCount++;
            }
            for (int i = 0; i < settings.InitialObjects.Count; ++i)
            {
                if (settings.InitialObjects[i] == null ||
                    !settings.InitialObjects[i].IsDataValid())
                {
                    // Invalid data
                    continue;
                }
                try
                {
                    await Download(
                        settings.InitialObjects[i],
                        OnFileSizeRetrieving,
                        OnFileSizeRetrieved,
                        OnDepsDownloading,
                        OnDepsFileDownloading,
                        OnDepsDownloaded);
                }
                catch (System.Exception ex)
                {
                    Logging.LogException(ex);
                }
                LoadedCount++;
            }
            onDownloadedAll?.Invoke();
            // Instantiates
            for (int i = 0; i < settings.InitialObjects.Count; ++i)
            {
                try
                {
                    AsyncOperationHandle<GameObject> getSizeOp = Addressables.InstantiateAsync(settings.InitialObjects[i].RuntimeKey);
                    await getSizeOp.Task;
                    Logging.Log($"Initialized {getSizeOp.Result.name}");
                }
                catch (System.Exception ex)
                {
                    Logging.LogException(ex);
                }
            }

            onEnd?.Invoke();
        }

        private void OnDestroy()
        {
            onStart?.RemoveAllListeners();
            onStart = null;
            onEnd?.RemoveAllListeners();
            onEnd = null;
            onFileSizeRetrieving?.RemoveAllListeners();
            onFileSizeRetrieving = null;
            onFileSizeRetrieved?.RemoveAllListeners();
            onFileSizeRetrieved = null;
            onDepsDownloading?.RemoveAllListeners();
            onDepsDownloading = null;
            onDepsFileDownloading?.RemoveAllListeners();
            onDepsFileDownloading = null;
            onDepsDownloaded?.RemoveAllListeners();
            onDepsDownloaded = null;
            onDownloadedAll?.RemoveAllListeners();
            onDownloadedAll = null;
        }

        protected virtual void OnFileSizeRetrieving()
        {
            FileSize = 0;
            onFileSizeRetrieving?.Invoke();
        }

        protected virtual void OnFileSizeRetrieved(long fileSize)
        {
            FileSize = fileSize;
            onFileSizeRetrieved?.Invoke(fileSize);
        }

        protected virtual void OnDepsDownloading()
        {
            onDepsDownloading?.Invoke(LoadedCount, TotalCount);
        }

        protected virtual void OnDepsFileDownloading(long downloadSize, long fileSize, float percentComplete)
        {
            onDepsFileDownloading?.Invoke(downloadSize, fileSize, percentComplete);
        }

        protected virtual void OnDepsDownloaded()
        {
            onDepsDownloaded?.Invoke(LoadedCount, TotalCount);
        }

        public static async Task<SceneInstance> DownloadAndLoadScene(
            object runtimeKey,
            LoadSceneParameters loadSceneParameters,
            System.Action onFileSizeRetrieving,
            AddressableAssetFileSizeDelegate onFileSizeRetrieved,
            System.Action onDepsDownloading,
            AddressableAssetDownloadProgressDelegate onDepsFileDownloading,
            System.Action onDepsDownloaded)
        {
            await Download(runtimeKey, onFileSizeRetrieving, onFileSizeRetrieved, onDepsDownloading, onDepsFileDownloading, onDepsDownloaded);
            AsyncOperationHandle<SceneInstance> getSizeOp = Addressables.LoadSceneAsync(runtimeKey, loadSceneParameters);
            while (!getSizeOp.IsDone)
            {
                await Task.Yield();
            }
            return getSizeOp.Result;
        }

        public static async Task<GameObject> DownloadAndInstantiate(
            object runtimeKey,
            System.Action onFileSizeRetrieving,
            AddressableAssetFileSizeDelegate onFileSizeRetrieved,
            System.Action onDepsDownloading,
            AddressableAssetDownloadProgressDelegate onDepsFileDownloading,
            System.Action onDepsDownloaded)
        {
            await Download(runtimeKey, onFileSizeRetrieving, onFileSizeRetrieved, onDepsDownloading, onDepsFileDownloading, onDepsDownloaded);
            AsyncOperationHandle<GameObject> getSizeOp = Addressables.InstantiateAsync(runtimeKey);
            while (!getSizeOp.IsDone)
            {
                await Task.Yield();
            }
            return getSizeOp.Result;
        }

        public static async Task Download(
            object runtimeKey,
            System.Action onFileSizeRetrieving,
            AddressableAssetFileSizeDelegate onFileSizeRetrieved,
            System.Action onDepsDownloading,
            AddressableAssetDownloadProgressDelegate onDepsFileDownloading,
            System.Action onDepsDownloaded)
        {
            // Get download size
            AsyncOperationHandle<long> getSizeOp = Addressables.GetDownloadSizeAsync(runtimeKey);
            onFileSizeRetrieving?.Invoke();
            while (!getSizeOp.IsDone)
            {
                await Task.Yield();
            }
            long fileSize = getSizeOp.Result;
            onFileSizeRetrieved.Invoke(fileSize);
            // Download dependencies
            if (fileSize > 0)
            {
                AsyncOperationHandle downloadOp = Addressables.DownloadDependenciesAsync(runtimeKey);
                onDepsDownloading?.Invoke();
                while (!downloadOp.IsDone)
                {
                    onDepsFileDownloading?.Invoke((long)(downloadOp.PercentComplete * fileSize), fileSize, downloadOp.PercentComplete);
                    await Task.Yield();
                }
                onDepsDownloaded?.Invoke();
                Addressables.ReleaseInstance(downloadOp);
            }
            else
            {
                onDepsDownloading?.Invoke();
                onDepsFileDownloading?.Invoke(0, 0, 1);
                onDepsDownloaded?.Invoke();
            }
        }
    }
}
