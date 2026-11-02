using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace MyGame
{
    public class UGSBootstrap : MonoBehaviour
    {
        private static Task<bool> s_initializationTask;

        public static bool IsInitialized { get; private set; }
        public static bool IsSignedIn =>
            UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance.IsSignedIn;

        private async void Awake()
        {
            await EnsureInitializedAsync();
        }

        public static Task<bool> EnsureInitializedAsync()
        {
            if (IsInitialized && IsSignedIn)
                return Task.FromResult(true);

            if (s_initializationTask == null || s_initializationTask.IsCompleted)
                s_initializationTask = InitializeServicesAsync();

            return s_initializationTask;
        }

        private static async Task<bool> InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsInitialized = true;
                return true;
            }
            catch (System.Exception e)
            {
                IsInitialized = false;
                Debug.LogError("UGS Initialize Failed: " + e.Message);
                return false;
            }
        }
    }
}
