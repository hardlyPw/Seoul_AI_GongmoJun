using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Seoul.Network.Bootstrap
{
    public class ServicesBootstrap : MonoBehaviour
    {
        [SerializeField] private string nextSceneName = "01_Title";

        private async void Start()
        {
            await InitializeServicesAsync();
        }

        private async Task InitializeServicesAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    var options = new InitializationOptions();
                    string profile = $"p_{System.Guid.NewGuid().ToString("N").Substring(0, 8)}";
                    options.SetProfile(profile);
                    await UnityServices.InitializeAsync(options);
                    Debug.Log($"Using profile: {profile}");
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                Debug.Log($"Signed in. PlayerId: {AuthenticationService.Instance.PlayerId}");

                SceneManager.LoadScene(nextSceneName);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"UGS init failed: {e.Message}");
            }
        }
    }
}
