using Unity.Netcode; using UnityEngine; public class TestRTT : MonoBehaviour { void Test() { float rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(0); } }
